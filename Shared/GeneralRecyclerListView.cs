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
        bool WasScrollingDown = true;
        object CurrentItem;
        List<GeneralRecyclerListViewItem> SeparatedItemViews = new List<GeneralRecyclerListViewItem>();

        /// <summary>
        /// This event will be fired when all datasource items are rendered and added to the list. 
        /// </summary>
        public readonly AsyncEvent LazyLoadEnded = new AsyncEvent();

        public GeneralRecyclerListView() => PseudoCssState = "lazy-loaded";

        public Func<Type, Type> GetTemplateMapping;

        ScrollView scroller;
        ScrollView Scroller => scroller ?? (scroller = FindParent<ScrollView>())
           ?? throw new Exception("Lazy loaded list view must be inside a scroll view");

        float ScrollerPosition => Scroller.ScrollY - ActualY;

        public override GeneralRecyclerListViewItem[] ItemViews => this.AllChildren<GeneralRecyclerListViewItem>().Except(v => v.Ignored).ToArray();

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

        protected override async Task CreateInitialItems()
        {
            if (IsProcessingLazyLoading) return;
            IsProcessingLazyLoading = true;

            try
            {
                var visibleHeight = Scroller?.ActualHeight ?? Page?.ActualHeight ?? Device.Screen.Height;

                while (LowestItemBottom < visibleHeight)
                {
                    var dataItem = GetNextVisibleItemToLoad();
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

        object GetNextVisibleItemToLoad()
        {
            lock (DataSourceSyncLock)
            {
                if (ItemViews.None()) return DataSource.FirstOrDefault();
                return DataSource.FirstOrDefault(x => GetOffset(x) > Math.Max(ScrollerPosition, GetOffset(LowestShownItem)));
            }
        }

        object GetPreviousVisibleItemToLoad()
        {
            lock (DataSourceSyncLock)
            {
                if (ItemViews.None() || (ScrollerPosition == 0 && TopestShownItem != DataSource.FirstOrDefault())) return DataSource.FirstOrDefault();
                return DataSource.LastOrDefault(x => GetOffset(x) < Math.Min(ScrollerPosition, GetOffset(TopestShownItem)));
            }
        }

        protected override GeneralRecyclerListViewItem CreateItem(object data)
        {
            var templateType = GetTemplateOfType(data.GetType());
            var template = (GeneralRecyclerListViewItem)Activator.CreateInstance(templateType);
            template.Item.Value = data;

            return template;
        }

        protected virtual Type GetTemplateOfType(Type dataType)
        {
            if (GetTemplateMapping == null)
                throw new Exception("You need to add View Templates mapping for all the types you want to render to the ListView.");

            return GetTemplateMapping.Invoke(dataType);
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
            var start = DateTime.Now;
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
        float ToppestItemTop => ItemViews.MinOrDefault(c => c.ActualY - c.ActualHeight);

        object LowestShownItem => ItemViews.None() ? default(object) : ItemViews.WithMax(c => c.ActualY).Item.Value;
        object TopestShownItem => ItemViews.None() ? default(object) : ItemViews.WithMin(c => c.ActualY).Item.Value;

        async Task<bool> LazyLoadMore()
        {
            var next = GetNextVisibleItemToLoad();

            Log.Warning("======== Next: " + next);

            if (next == null)
            {
                await LazyLoadEnded.Raise();
                return false;
            }

            if (!RecycleDown(next)) await Add(CreateItem(next), false);
            return true;
        }

        bool ShouldLoadMore() => ScrollerPosition + Scroller.ActualHeight >= ItemViews.Except(SeparatedItemViews).MaxOrDefault(c => c.ActualBottom) + ActualY;

        bool ShouldRecycleUp() => ScrollerPosition < ItemViews.Except(SeparatedItemViews).MinOrDefault(c => c.ActualY) + ActualY;

        bool RecycleUp()
        {
            if (WasScrollingDown)
            {
                SeparatedItemViews.Do(x => x.Y(LowestItemBottom));
                SeparatedItemViews.Clear();
                WasScrollingDown = false;
            }

            var item = GetPreviousVisibleItemToLoad();

            Log.Error("---------- Prev: " + item);


            if (item == null) return false;

            var recycle = GetAllTemplatesOfType(item).WithMax(x => x.ActualY);

            if (recycle != ItemViews.WithMax(x => x.ActualY))
            {
                ItemViews.Where(x => x.ActualY > recycle.ActualY).Except(SeparatedItemViews).Do(x => SeparatedItemViews.Add(x));
            }

            //if(GetOffset(item) + recycle.ActualHeight < ItemViews.Except(SeparatedItemViews).Min(x=> x.ActualY - x.ActualHeight))
            //{
            //    ItemViews.Except(recycle).Except(SeparatedItemViews).Do(x => SeparatedItemViews.Add(x));
            //}

            recycle.Y(GetOffset(item));
            recycle.Item.Set(item);

            SeparatedItemViews.Remove(recycle);

            // In case the height is changed
            Thread.UI.Post(() => recycle.Y(GetOffset(item)));

            return true;
        }

        bool RecycleDown(object item)
        {
            if (!WasScrollingDown)
            {
                SeparatedItemViews.Do(x => x.Y(ToppestItemTop - x.ActualHeight));
                SeparatedItemViews.Clear();
                WasScrollingDown = true;
            }

            GeneralRecyclerListViewItem recycled;

            var firstChild = GetAllTemplatesOfType(item).WithMin(x => x.ActualY);

            if (firstChild != ItemViews.WithMin(x => x.ActualY))
            {
                ItemViews.Where(x => x.ActualY < firstChild.ActualY).Except(x => SeparatedItemViews.Contains(x)).Do(x => SeparatedItemViews.Add(x));
            }

            if (firstChild != null && firstChild.ActualBottom < ScrollerPosition)
            {
                recycled = firstChild;
                recycled.Y(GetOffset(item)).Item.Set(item);

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

            await base.Add(child, awaitNative);
            child.Y(GetOffset(row.Item));

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

        GeneralRecyclerListViewItem Recycle(object data)
        {
            var result = GetAllTemplatesOfType(data).FirstOrDefault(v => v.Ignored);
            if (result == null) return null;

            result.Y(GetOffset(data)).Ignored(false);
            result.Item.Set(data);
            return result;
        }

        public async Task<bool> ScrollToItem(object data, bool animate = false)
        {
            var newPosition = GetOffset(data) + ActualY;
            await Scroller.ScrollTo(newPosition > ScrollerPosition ? newPosition - 1 : newPosition + 1, 0, animate);
            return true;
        }

        float GetOffset(object data)
        {
            float offset;
            var item = DataSource.FirstOrDefault(x => x.Equals(data));
            if (item == null) return 0;

            offset = 0;
            foreach (var type in DataSource.GetElementsBefore(item).Select(x => x.GetType()).Distinct())
            {
                var lastItem = ItemViews.LastOrDefault(x => x.Item.Value.GetType() == type) ?? (GeneralRecyclerListViewItem)Activator.CreateInstance(GetTemplateMapping(type));
                offset += DataSource.GetElementsBefore(item).Count(x => x.GetType() == type) * lastItem.CalculateTotalHeight();
            }

            return offset;
        }
    }
}