using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Zebble.Device;

namespace Zebble
{
    public class RecyclerListView<TSource, TRowTemplate> : ListView<TSource, TRowTemplate>
        where TRowTemplate : View, IRecyclerListViewItem<TSource>, new()
    {
        ScrollView scroller;
        bool IsProcessingLazyLoading;
        float ItemHeight = 0;

        public RecyclerListView() => PseudoCssState = "lazy-loaded";

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

        TSource GetNextItemToLoad()
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
                        await UIWorkBatch.Run(() => Add(CreateItem(dataItem)));
                }
            }
            finally
            {
                IsProcessingLazyLoading = false;
            }
        }

        public override TRowTemplate[] ItemViews => this.AllChildren<TRowTemplate>().Except(v => v.Ignored).ToArray();

        protected override float CalculateContentAutoHeight()
        {
            var lastItem = ItemViews.LastOrDefault();

            if (lastItem == null) return emptyTemplate?.ActualHeight ?? 0;

            if (lastItem.Native == null) lastItem.ApplyCssToBranch().Wait();
            if (lastItem.Height.AutoOption.HasValue || lastItem.Height.PercentageValue.HasValue)
                Log.Error("Items in a lazy loaded list view must have an explicit height value.");

            ItemHeight = lastItem.CalculateTotalHeight();

            return Padding.Vertical() + (DataSource.Count() + 1) * ItemHeight;
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

        bool ShouldRecycleUp() => Scroller.ScrollY < ItemViews.MinOrDefault(c => c.ActualY) + ActualY;

        bool ShouldLoadMore()
            => Scroller.ScrollY + Scroller.ActualHeight >= LowestItemBottom + ActualY;

        float LowestItemBottom => ItemViews.MaxOrDefault(c => c.ActualBottom);

        TSource LowestShownItem => ItemViews.None() ? default(TSource) : ItemViews.WithMax(c => c.ActualY).Item.Value;

        async Task<bool> LazyLoadMore()
        {
            var next = GetNextItemToLoad();

            if (next == null)
            {
                await LazyLoadEnded.Raise();
                return false;
            }

            if (!RecycleDown(next)) await Add(CreateItem(next));
            return true;
        }

        bool RecycleUp()
        {
            var topItem = ItemViews.WithMin(v => v.ActualY);

            TSource item;

            lock (DataSourceSyncLock)
                if (topItem == null) item = DataSource.FirstOrDefault();
                else item = DataSource.ElementAtOrDefault(DataSource.IndexOf(topItem.Item) - 1);

            if (item == null) return false;

            var recycle = ItemViews.WithMax(x => x.ActualY);

            recycle.Y(topItem.ActualY - recycle.ActualHeight);
            recycle.Item.Set(item);
            // In case the height is changed
            Thread.UI.Post(() => recycle.Y(topItem.ActualY - recycle.ActualHeight));

            return true;
        }

        bool RecycleDown(TSource item)
        {
            TRowTemplate recycled;

            var firstChild = ItemViews.WithMin(x => x.ActualY);
            if (firstChild != null && firstChild.ActualBottom + ActualY < Scroller.ScrollY)
            {
                recycled = firstChild;
                recycled.Y(LowestItemBottom).Item.Set(item);
                return true;
            }
            else
            {
                recycled = Recycle(item);
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
            var row = child as TRowTemplate;
            if (row == null) return await base.Add(child, awaitNative);

            var position = LowestItemBottom;
            Log.Message("Adding to " + LowestItemBottom + " -> " + row.Item.Value);
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
            if (child is TRowTemplate row) child.Ignored = true;
            else await base.Remove(child, awaitNative);
        }

        TRowTemplate Recycle(TSource data)
        {
            var result = AllChildren.OfType<TRowTemplate>().FirstOrDefault(v => v.Ignored);
            if (result == null) return null;

            result.Y(LowestItemBottom).Ignored(false);
            result.Item.Set(data);
            return result;
        }
    }
}