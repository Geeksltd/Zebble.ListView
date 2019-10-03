using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Zebble.Device;
using System.Reflection;

namespace Zebble
{
    public class GeneralRecyclerListView : ListView<object, GeneralRecyclerListViewItem>
    {
        bool IsProcessingLazyLoading;
        float TopOfScreen => Scroller.ScrollY - ActualY;
        float BottomOfScreen => TopOfScreen + Scroller.ActualHeight;
        Dictionary<int, float> Offsets = new Dictionary<int, float>();

        /// <summary>
        /// This event will be fired when all datasource items are rendered and added to the list. 
        /// </summary>
        public readonly AsyncEvent LazyLoadEnded = new AsyncEvent();
        public float Offset { get; set; } = 300;
        public GeneralRecyclerListView() => PseudoCssState = "lazy-loaded";
        public Func<Type, Type> GetTemplateMapping;
        public Func<Type, float> GetTemplateHeight;

        ScrollView scroller;
        ScrollView Scroller => scroller ?? (scroller = FindParent<ScrollView>())
           ?? throw new Exception("Lazy loaded list view must be inside a scroll view");

        public override GeneralRecyclerListViewItem[] ItemViews => this.AllChildren<GeneralRecyclerListViewItem>().Except(v => v.Ignored).ToArray();

        public override async Task OnInitializing()
        {
            await base.OnInitializing();

            Thread.Pool.RunAction(CalculateOffsets);

            await WhenShown(async () =>
            {
                Scroller.UserScrolledVertically.HandleOn(Thread.UI, () => OnUserScrolledVertically());
                Scroller.ScrollEnded.HandleOn(Thread.UI, () => OnUserScrolledVertically());
                await CreateInitialItems();
            });
        }

        protected override Task CreateInitialItems()
        {
            Offsets.Clear();
            return RenderItems();
        }

        protected override GeneralRecyclerListViewItem CreateItem(object data)
        {
            var templateType = GetTemplateOfType(data.GetType());
            var template = (GeneralRecyclerListViewItem)Activator.CreateInstance(templateType);
            template.Item.Set(data);
            template.Y(GetOffset(data));

            return template;
        }

        protected virtual Type GetTemplateOfType(Type dataType)
        {
            if (GetTemplateMapping == null)
                throw new Exception("You need to add View Templates mapping for all the types you want to render to the ListView.");

            return GetTemplateMapping.Invoke(dataType);
        }

        protected virtual float GetTemplateHeightOfType(Type dataType)
        {
            if (GetTemplateHeight == null)
                throw new Exception("You need to add View Templates height mapping for all the types you want to render to the ListView.");

            return GetTemplateHeight.Invoke(dataType);
        }

        protected virtual IEnumerable<GeneralRecyclerListViewItem> GetAllTemplatesOfType(object data)
        {
            var templateType = GetTemplateOfType(data.GetType());
            return AllChildren.Where(x => x.GetType() == templateType).Cast<GeneralRecyclerListViewItem>();
        }

        protected override float CalculateContentAutoHeight()
        {
            float height = 0;

            if (DataSource.Count() == 0)
                return emptyTemplate?.ActualHeight ?? 0;

            foreach (var type in DataSource.Select(x => x.GetType()).Distinct())
                height += DataSource.Count(x => x.GetType() == type) * GetTemplateHeightOfType(type);

            var totalHeight = Padding.Vertical() + height;
            Scroller.CalculateContentSize();
            return totalHeight;
        }

        async Task OnUserScrolledVertically()
        {
            if (IsProcessingLazyLoading) return;
            IsProcessingLazyLoading = true;

            try
            {
                await RenderItems();
            }
            finally
            {
                IsProcessingLazyLoading = false;
            }
        }

        private async Task RenderItems()
        {
            CalculateOffsets();

            var top = TopOfScreen - Offset;
            var bottom = BottomOfScreen + Offset;

            var itemsInScreen = Offsets.Where(x => top < x.Value && x.Value < bottom).Select(x => x.Key).ToList();
            foreach (var index in itemsInScreen)
            {
                var item = DataSource.ElementAt(index);
                var position = GetOffset(item);
                if (position > bottom)
                    break;

                if (ItemViews.None(x => x.ActualY == position))
                {
                    var recycle = GetAllTemplatesOfType(item).Where(x => top > x.ActualY || x.ActualY > bottom).WithMin(x => x.ActualY);

                    if (recycle != null)
                        recycle.Y(position).Item.Set(item);
                    else
                        await Add(CreateItem(item), false);
                }
            }
        }

        private void CalculateOffsets()
        {
            if (Offsets.Count != DataSource.Count())
                DataSource.Do(x => GetOffset(x));
        }

        protected override async Task OnEmptyTemplateChanged(EmptyTemplateChangedArg args)
        {
            await base.OnEmptyTemplateChanged(args);
            await (this as IAutoContentHeightProvider).Changed.Raise();
        }

        public override async Task<TView> Add<TView>(TView child, bool awaitNative = false)
        {
            var row = child as GeneralRecyclerListViewItem;
            if (row == null) return await base.Add(child, awaitNative);

            await base.Add(child, awaitNative);

            // Hardcode on the same value to get rid of the dependencies.
            foreach (var item in ItemViews)
            {
                item.Y.Changed.ClearHandlers();
                item.Y(GetOffset((item as GeneralRecyclerListViewItem).Item.Value));
                item.IgnoredChanged.ClearHandlers();
            }

            return child;
        }

        public override async Task Remove(View child, bool awaitNative = false)
        {
            if (child is GeneralRecyclerListViewItem row) child.Ignored = true;
            else await base.Remove(child, awaitNative);
        }

        public async Task<bool> ScrollToItem(object data, bool animate = false)
        {
            var newPosition = GetOffset(data) + ActualY;
            await Scroller.ScrollTo(newPosition, 0, animate);
            await RenderItems();
            return true;
        }

        float GetOffset(object data)
        {
            var index = DataSource.IndexOf(data);
            if (Offsets.ContainsKey(index)) return Offsets[index];

            float offset = 0;
            var item = DataSource.FirstOrDefault(x => x.Equals(data));
            if (item == null) throw new Exception("Item is not in Datasource.");

            foreach (var type in DataSource.GetElementsBefore(item).Select(x => x.GetType()).Distinct())
                offset += DataSource.GetElementsBefore(item).Count(x => x.GetType() == type) * GetTemplateHeightOfType(type);

            Offsets.Add(index, offset);

            return offset;
        }
    }
}