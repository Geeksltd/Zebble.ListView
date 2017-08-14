namespace Zebble
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    partial class ListView<TSource, TRowTemplate>
    {
        int VisibleItems = 0;
        float ListHeight = 0;
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

        Task LazyLoadInitialItems() => UIWorkBatch.Run(DoLazyLoadInitialItems);

        async Task DoLazyLoadInitialItems()
        {
            using (await LazyLoadingSyncLock.LockAsync())
            {
                var visibleHeight = FindParent<ScrollView>()?.ActualHeight ?? Page?.ActualHeight ?? Device.Screen.Height;
                visibleHeight -= ActualY;

                while (ListHeight < visibleHeight && VisibleItems < dataSource.Count())
                {
                    var item = CreateItem(dataSource[VisibleItems]);
                    await Add(item);
                    ListHeight += item.ActualHeight;

                    VisibleItems++;
                };
            }
        }

        protected override float CalculateContentAutoHeight()
        {
            if (!LazyLoad) return base.CalculateContentAutoHeight();

            var lastItem = ItemViews.LastOrDefault();

            if (lastItem == null) return 0;

            lastItem.ApplyCssToBranch().Wait();

            if (lastItem.Height.AutoOption.HasValue || lastItem.Height.PercentageValue.HasValue)
                Device.Log.Error("Items in a lazy loaded list view must have an explicit height value.");

            return Padding.Vertical() + dataSource.Count * lastItem.CalculateTotalHeight();
        }

        async Task OnUserScrolledVertically(ScrollView scroller)
        {
            while (IsLazyLoadingMore) await Task.Delay(100);
            IsLazyLoadingMore = true;

            var staticallyVisible = scroller.ActualHeight - ActualY;

            var shouldShowUpto = scroller.ScrollY + staticallyVisible + 10 /* Margin to ensure something is there */;

            while (shouldShowUpto >= ListHeight)
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

                VisibleItems++;

                var item = await AddItem(next);
                ListHeight += item.ActualHeight;
            }

            return true;
        }
    }
}