namespace Zebble
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    partial class ListView<TSource, TRowTemplate>
    {
        int VisibleItems = 0;
        float LazyRenderedItemsTotalHeight = 0;
        bool IsLazyLoadingMore;
        bool lazyLoad;
        AsyncLock LazyLoadingSyncLock = new AsyncLock();

        public bool LazyLoad
        {
            get => lazyLoad;
            set { lazyLoad = value; SetPseudoCssState("lazy-loaded", value).RunInParallel(); }
        }

        async Task OnShown()
        {
            if (LazyLoad)
            {
                var scroller = FindParent<ScrollView>();
                scroller?.UserScrolledVertically.HandleOn(Device.ThreadPool, () => OnUserScrolledVertically(scroller));

                await LazyLoadInitialItems();
            }
        }

        async Task LazyLoadInitialItems()
        {
            await UIWorkBatch.Run(DoLazyLoadInitialItems);
            await OnLazyVisibleItemsChanged();
        }

        async Task DoLazyLoadInitialItems()
        {
            using (await LazyLoadingSyncLock.LockAsync())
            {
                var visibleHeight = FindParent<ScrollView>()?.ActualHeight ?? Page?.ActualHeight ?? Device.Screen.Height;
                visibleHeight -= ActualY;

                while (LazyRenderedItemsTotalHeight < visibleHeight && VisibleItems < dataSource.Count())
                {
                    var item = CreateItem(dataSource[VisibleItems]);

                    await ChangeInBatch(() => Add(item));

                    LazyRenderedItemsTotalHeight += item.ActualHeight;

                    VisibleItems++;
                    await OnLazyVisibleItemsChanged();
                };
            }
        }

        Task OnLazyVisibleItemsChanged() => (this as IAutoContentHeightProvider).Changed.Raise();

        protected override float CalculateContentAutoHeight()
        {
            if (!LazyLoad) return base.CalculateContentAutoHeight();

            var lastItem = ItemViews.LastOrDefault();

            if (lastItem == null) return 0;

            if (lastItem.Native == null)
                lastItem.ApplyCssToBranch().Wait();

            if (lastItem.Height.AutoOption.HasValue || lastItem.Height.PercentageValue.HasValue)
                Device.Log.Error("Items in a lazy loaded list view must have an explicit height value.");

            var itemHeight = lastItem.CalculateTotalHeight();

            var logicalRows = Math.Max(VisibleItems, (int)(Root.ActualHeight / itemHeight)) + 5;
            logicalRows = logicalRows.LimitMax(dataSource.Count);

            return Padding.Vertical() + logicalRows * itemHeight;
        }

        async Task OnUserScrolledVertically(ScrollView scroller)
        {
            while (IsLazyLoadingMore) await Task.Delay(100);
            IsLazyLoadingMore = true;

            var staticallyVisible = scroller.ActualHeight - ActualY;

            var shouldShowUpto = scroller.ScrollY + staticallyVisible + 100 /* Margin to ensure something is there */;

            while (shouldShowUpto >= LazyRenderedItemsTotalHeight)
            {
                if (!await LazyLoadMore()) break;
                if (Device.Platform == DevicePlatform.IOS) await Task.Delay(Animation.OneFrame);
            }

            IsLazyLoadingMore = false;
        }

        async Task<bool> LazyLoadMore()
        {
            TSource next;

            using (await LazyLoadingSyncLock.LockAsync())
            {
                lock (DataSourceSyncLock) next = dataSource.Skip(VisibleItems).FirstOrDefault();

                if (next == null) return false;

                TRowTemplate item = null;
                await ChangeInBatch(() => UIWorkBatch.Run(async () =>
                {
                    VisibleItems++;
                    item = await AddItem(next);
                }));

                LazyRenderedItemsTotalHeight += item.ActualHeight;

                await OnLazyVisibleItemsChanged();
            }

            return true;
        }
    }
}