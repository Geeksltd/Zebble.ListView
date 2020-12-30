using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Olive;

namespace Zebble
{
    public class GeneralRecyclerListView : GeneralListView<object, GeneralRecyclerListViewItem>
    {
        static AsyncLock RenderLock = new AsyncLock();
        bool IsProcessingLazyLoading;
        float TopOfScreen => Scroller.ScrollY - ActualY;
        float BottomOfScreen => TopOfScreen + Scroller.ActualHeight;
        Dictionary<int, float> Offsets = new Dictionary<int, float>();

        /// <summary>
        /// This event will be fired when all data source items are rendered and added to the list. 
        /// </summary>
        public readonly AsyncEvent LazyLoadEnded = new AsyncEvent();
        public float Offset { get; set; } = 0;
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
                var scroller = FindParent<ScrollView>();
                if (scroller == null) return; // rare threading issue.

                scroller.UserScrolledVertically.HandleOn(Thread.UI, () => OnUserScrolledVertically());
                scroller.ScrollEnded.HandleOn(Thread.UI, () => OnUserScrolledVertically());
                await CreateInitialItems();
            });
        }

        protected override Task CreateInitialItems()
        {
            Offsets.Clear();
            this.Height(CalculateHeight());
            return RenderItems();
        }

        protected override GeneralRecyclerListViewItem CreateItem(object data)
        {
            var templateType = GetTemplateOfType(data.GetType());
            var template = (GeneralRecyclerListViewItem)Activator.CreateInstance(templateType);
            template.Item.Set(data);
            template.Y.Set(GetOffset(data));

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
            return ItemViews.Where(x => x.GetType() == templateType);
        }

        protected float CalculateHeight()
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

        async Task RenderItems()
        {
            using (await RenderLock.Lock())
            {
                CalculateOffsets();

                var top = TopOfScreen - Offset;
                var bottom = BottomOfScreen + Offset;

                var itemsInScreenIndexes = Offsets.
                    Where(x => top <= x.Value + GetTemplateHeight(DataSource.ElementAt(x.Key).GetType()) && x.Value <= bottom).
                    Select(x => x.Key).ToList();

                foreach (var index in itemsInScreenIndexes)
                {
                    var item = DataSource.ElementAt(index);
                    var position = GetOffset(item);
                    if (position > bottom) return;

                    if (ItemViews.None(x => x.ActualY == position))
                    {
                        var recycle = GetAllTemplatesOfType(item).
                            Where(x => top > x.ActualY + GetTemplateHeight(x.Item.Value.GetType()) || x.ActualY > bottom).
                            WithMin(x => x.ActualY);

                        if (recycle != null)
                            recycle.Y(position).Item.Set(item);
                        else
                            await UIWorkBatch.Run(() => Add(CreateItem(item), false));
                    }
                }
            }
        }

        void CalculateOffsets()
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
            if (!(child is GeneralRecyclerListViewItem)) return await base.Add(child, awaitNative);

            await base.Add(child, awaitNative);

            return child;
        }

        public override async Task Remove(View child, bool awaitNative = false)
        {
            if (child is GeneralRecyclerListViewItem) await child.IgnoredAsync();
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

            return Offsets.GetOrAdd(index, () => offset);
        }

        public override Task UpdateSource(IEnumerable<object> source, bool reRenderItems = true) =>
           UIWorkBatch.Run(() => base.UpdateSource(source, reRenderItems));
    }
}