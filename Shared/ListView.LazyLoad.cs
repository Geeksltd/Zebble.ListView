namespace Zebble
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Zebble.Device;

    partial class ListView<TSource, TRowTemplate>
    {
        int VisibleItems;
        float LazyRenderedItemsTotalHeight = 0;
        bool IsLazyLoadingMore;
        bool lazyLoad;
        float ItemHeight = 0;
        AsyncLock LazyLoadingSyncLock = new AsyncLock();

        /// <summary>
        /// This event will be fired when all datasource items are rendered and added to the list. 
        /// </summary>
        public readonly AsyncEvent LazyLoadEnded = new AsyncEvent();

        /// <summary>
        /// Number of extra items to be loaded outside of visible screen. 
        /// </summary>
        public int LazyLoadOffset = 10;

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
                scroller?.UserScrolledVertically.HandleOn(Thread.Pool, () => OnUserScrolledVertically(scroller));

                await LazyLoadInitialItems();
            }
        }

        async Task LazyLoadInitialItems()
        {
            await DoLazyLoadInitialItems();
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

                    await UIWorkBatch.Run(() => Add(item));

                    LazyRenderedItemsTotalHeight += item.ActualHeight;

                    VisibleItems++;
                    await OnLazyVisibleItemsChanged();
                }

                AddOffsetItem().RunInParallel();
            }
        }

        async Task AddOffsetItem()
        {
            for (int i = 0; i < LazyLoadOffset; i++)
                if (!await LazyLoadMore()) break;
        }

        Task OnLazyVisibleItemsChanged()
        {
            var provider = (this as IAutoContentHeightProvider);
            if (provider != null)
            {
                if (!provider.Changed.IsHandled())
                    Height.Set(Length.AutoStartegy.Content);

                return provider.Changed.Raise();
            }

            return Task.CompletedTask;
        }

        protected override float CalculateContentAutoHeight()
        {
            if (!LazyLoad) return base.CalculateContentAutoHeight();

            var lastItem = ItemViews.LastOrDefault();

            if (lastItem == null) return 0;

            if (lastItem.Native == null)
                lastItem.ApplyCssToBranch().Wait();

            if (lastItem.Height.AutoOption.HasValue || lastItem.Height.PercentageValue.HasValue)
                Log.Error("Items in a lazy loaded list view must have an explicit height value.");

            ItemHeight = lastItem.CalculateTotalHeight();

            var logicalRows = Math.Max(VisibleItems, (int)(Root.ActualHeight / ItemHeight)) + 5;
            logicalRows = logicalRows.LimitMax(dataSource.Count);

            return Padding.Vertical() + logicalRows * ItemHeight;
        }

        async Task OnUserScrolledVertically(ScrollView scroller)
        {
            if (IsLazyLoadingMore) return;
            IsLazyLoadingMore = true;

            try
            {
                float shouldShowUpto() => LazyLoadOffset * ItemHeight + scroller.ScrollY + (scroller.ActualHeight - ActualY)
                    + 100 /* Margin to ensure something is there */;

                await UIWorkBatch.Run(async () =>
                {
                    while (shouldShowUpto() >= LazyRenderedItemsTotalHeight)
                    {
                        if (!await LazyLoadMore()) break;
                        if (OS.Platform.IsIOS()) await Task.Delay(Animation.OneFrame);
                    }
                });
            }
            finally { IsLazyLoadingMore = false; }
        }

        async Task<bool> LazyLoadMore()
        {
            var before = DateTime.UtcNow;

            TSource next;

            using (await LazyLoadingSyncLock.LockAsync())
            {
                lock (DataSourceSyncLock) next = dataSource.Skip(VisibleItems).FirstOrDefault();

                if (next == null)
                {
                    await LazyLoadEnded.Raise();
                    return false;
                }

                VisibleItems++;
                var item = await AddItem(next);
                LazyRenderedItemsTotalHeight += item.ActualHeight;

                await OnLazyVisibleItemsChanged();
            }

            return true;
        }
    }
}