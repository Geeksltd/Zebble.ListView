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
        ScrollView scroller;
        bool IsProcessingLazyLoading;
        bool WasScrollingDown = true;
        List<GeneralRecyclerListViewItem> SeparatedItemViews = new List<GeneralRecyclerListViewItem>();

        public GeneralRecyclerListView() => PseudoCssState = "lazy-loaded";

        public Func<Type, Type> GetTemplateMapping;

        public override async Task OnInitializing()
        {
            await base.OnInitializing();

            await WhenShown(async () =>
            {
                Scroller.UserScrolledVertically.HandleOn(Thread.UI, () => OnUserScrolledVertically());
                Scroller.ScrollEnded.HandleOn(Thread.UI, () => OnUserScrolledVertically());
                await CreateInitialItems();
            });
        }

        /// <summary>
        /// This event will be fired when all datasource items are rendered and added to the list. 
        /// </summary>
        public readonly AsyncEvent LazyLoadEnded = new AsyncEvent();

        ScrollView Scroller => scroller ?? (scroller = FindParent<ScrollView>())
            ?? throw new Exception("Lazy loaded list view must be inside a scroll view");

        object GetNextItemToLoad()
        {
            lock (DataSourceSyncLock)
                return DataSource.ElementAtOrDefault(DataSource.IndexOf(LowestShownItem) + 1);
        }

        protected override async Task CreateInitialItems()
        {
            if (IsProcessingLazyLoading) return;
            IsProcessingLazyLoading = true;

            try
            {
                var visibleHeight = Scroller?.ActualHeight ?? Page?.ActualHeight ?? Device.Screen.Height;
                visibleHeight -= ActualY;

                while (LowestItemBottom < visibleHeight)
                {
                    var dataItem = GetNextItemToLoad();
                    if (dataItem == null) break;

                    var recycled = Recycle(dataItem);
                    if (recycled == null)
                        await UIWorkBatch.Run(() => Add(CreateItem(dataItem), false));
                }
            }
            finally
            {
                IsProcessingLazyLoading = false;
            }
        }

        protected override GeneralRecyclerListViewItem CreateItem(object data)
        {
            var templateType = GetTemplateType(data.GetType());
            var template = (GeneralRecyclerListViewItem)Activator.CreateInstance(templateType);
            template.Item.Value = data;

            return template;
        }

        protected virtual IEnumerable<GeneralRecyclerListViewItem> GetAllTemplatesOfType(object data)
        {
            var templateType = GetTemplateType(data.GetType());
            return AllChildren.Where(x => x.GetType() == templateType).Cast<GeneralRecyclerListViewItem>();
        }

        protected virtual Type GetTemplateType(Type dataType)
        {
            if (GetTemplateMapping == null)
                throw new Exception("You need to add View Templates mapping for all the types you want to render to the ListView");

            return GetTemplateMapping.Invoke(dataType);
        }

        public override GeneralRecyclerListViewItem[] ItemViews => this.AllChildren<GeneralRecyclerListViewItem>().Except(v => v.Ignored).ToArray();

        protected override float CalculateContentAutoHeight()
        {
            float height = 0;

            if (DataSource.Count() == 0)
                return emptyTemplate?.ActualHeight ?? 0;

            foreach (var type in DataSource.Select(x => x.GetType()).Distinct())
            {
                var lastItem = ItemViews.LastOrDefault(x => x.Item.Value.GetType() == type);

                if (lastItem == null) return emptyTemplate?.ActualHeight ?? 0;

                if (lastItem.Native == null) lastItem.ApplyCssToBranch().Wait();
                if (lastItem.Height.AutoOption.HasValue || lastItem.Height.PercentageValue.HasValue)
                    Log.Error("Items in a lazy loaded list view must have an explicit height value.");

                height += DataSource.Count(x => x.GetType() == type) * lastItem.CalculateTotalHeight();
            }

            return Padding.Vertical() + height;
        }

        async Task OnUserScrolledVertically()
        {
            if (IsProcessingLazyLoading) return;
            IsProcessingLazyLoading = true;

            try
            {
                while (ShouldLoadMore() && await LazyLoadMore()) ;
                while (ShouldRecycleUp() && RecycleUp()) ;
            }
            finally { IsProcessingLazyLoading = false; }
        }

        float LowestItemBottom => ItemViews.MaxOrDefault(c => c.ActualBottom);

        float HighestItemTop => ItemViews.MinOrDefault(c => c.ActualY - c.ActualHeight);

        object LowestShownItem => ItemViews.None() ? default(object) : ItemViews.WithMax(c => c.ActualY).Item.Value;

        async Task<bool> LazyLoadMore()
        {
            var next = GetNextItemToLoad();

            if (next == null)
            {
                await LazyLoadEnded.Raise();
                return false;
            }

            if (!RecycleDown(next)) await Add(CreateItem(next), false);
            return true;
        }

        bool ShouldLoadMore() => Scroller.ScrollY + Scroller.ActualHeight >= ItemViews.Except(SeparatedItemViews).MaxOrDefault(c => c.ActualBottom) + ActualY;

        bool ShouldRecycleUp() => Scroller.ScrollY < ItemViews.Except(SeparatedItemViews).MinOrDefault(c => c.ActualY) + ActualY;

        bool RecycleUp()
        {
            if (WasScrollingDown)
            {
                SeparatedItemViews.Do(x => x.Y(LowestItemBottom));
                SeparatedItemViews.Clear();
                WasScrollingDown = false;
            }

            var topItem = ItemViews.WithMin(v => v.ActualY);

            object item;

            lock (DataSourceSyncLock)
                if (topItem == null) item = DataSource.FirstOrDefault();
                else item = DataSource.ElementAtOrDefault(DataSource.IndexOf(topItem.Item.Value) - 1);

            if (item == null) return false;

            var recycle = GetAllTemplatesOfType(item).WithMax(x => x.ActualY);

            if (recycle != ItemViews.WithMax(x => x.ActualY))
            {
                ItemViews.Where(x => x.ActualY > recycle.ActualY).Except(x => SeparatedItemViews.Contains(x)).Do(x => SeparatedItemViews.Add(x));
            }


            recycle.Y(topItem.ActualY - recycle.ActualHeight);
            recycle.Item.Set(item);

            SeparatedItemViews.Remove(recycle);

            // In case the height is changed
            Thread.UI.Post(() => recycle.Y(topItem.ActualY - recycle.ActualHeight));



            return true;
        }

        bool RecycleDown(object item)
        {
            if (!WasScrollingDown)
            {
                SeparatedItemViews.Do(x => x.Y(HighestItemTop - x.ActualHeight));
                SeparatedItemViews.Clear();
                WasScrollingDown = true;
            }

            GeneralRecyclerListViewItem recycled;

            var firstChild = GetAllTemplatesOfType(item).WithMin(x => x.ActualY);

            if (firstChild != ItemViews.WithMin(x => x.ActualY))
            {
                ItemViews.Where(x => x.ActualY < firstChild.ActualY).Except(x => SeparatedItemViews.Contains(x)).Do(x => SeparatedItemViews.Add(x));
            }

            if (firstChild != null && firstChild.ActualBottom + ActualY < Scroller.ScrollY)
            {
                recycled = firstChild;
                recycled.Y(LowestItemBottom).Item.Set(item);

                SeparatedItemViews.Remove(recycled);

                return true;
            }
            else
            {
                recycled = Recycle(item);

                SeparatedItemViews.Remove(recycled);

                return recycled != null;
            }
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

            var position = LowestItemBottom;
            await base.Add(child, awaitNative);
            child.Y(position);

            // Hardcode on the same value to get rid of the dependencies.
            foreach (var item in ItemViews)
            {
                item.Y.Changed.ClearHandlers();
                item.Y(item.ActualY);
                item.IgnoredChanged.ClearHandlers();
            }

            return child;
        }

        public override async Task Remove(View child, bool awaitNative = false)
        {
            if (child is GeneralRecyclerListViewItem row) child.Ignored = true;
            else await base.Remove(child, awaitNative);
        }

        GeneralRecyclerListViewItem Recycle(object data)
        {
            var result = GetAllTemplatesOfType(data).FirstOrDefault(v => v.Ignored);
            if (result == null) return null;

            result.Y(LowestItemBottom).Ignored(false);
            result.Item.Set(data);
            return result;
        }

        public async Task<bool> ScrollToItem(object data, bool animate = true)
        {
            var item = DataSource.FirstOrDefault(x => x.Equals(data));
            if (item == null) return false;

            float height = 0;
            foreach (var type in DataSource.GetElementsBefore(item).Select(x => x.GetType()).Distinct())
            {
                var lastItem = ItemViews.LastOrDefault(x => x.Item.Value.GetType() == type);
                height += DataSource.GetElementsBefore(item).Count(x => x.GetType() == type) * lastItem.CalculateTotalHeight();
            }

            await Scroller.ScrollTo(height,0, animate);
            return true;
        }
    }
}