namespace Zebble
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Olive;

    public class Rotator<TSource, TTemplate> : View, IAutoContentHeightProvider
        where TTemplate : View, IListViewItem<TSource>, new()
        where TSource : class, new()
    {
        TTemplate SelectedRow;
        readonly AsyncEvent AutoContentHeightChanged = new AsyncEvent();
        public int ItemsToDisplay = 3;

        public readonly AsyncEvent SelectionChanged = new AsyncEvent(ConcurrentEventRaisePolicy.Queue);

        public readonly ScrollView Scroller = new ScrollView();
        public readonly ListView<TSource, TTemplate> List = new ListView<TSource, TTemplate>();

        public override async Task OnInitializing()
        {
            await base.OnInitializing();

            await Add(Scroller);
            await Scroller.Add(List);

            Scroller.UserScrolledVertically.HandleWith(HighlightMiddleItem);
            Scroller.ScrollEnded.HandleWith(async () =>
            {
                await SetScrollPosition();
                await SelectionChanged.Raise();
            });
        }

        public TSource SelectedItem => SelectedRow?.Item;

        public Task SetSource(IEnumerable<TSource> dataSource)
        {
            return UIWorkBatch.Run(() =>
            {
                var source = dataSource.ToList();

                Enumerable.Range(0, PlaceHoldersCount).Do(x => source.Insert(0, Activator.CreateInstance<TSource>()));
                Enumerable.Range(0, PlaceHoldersCount).Do(x => source.Add(Activator.CreateInstance<TSource>()));
                return List.UpdateSource(source);
            });
        }

        public Task Append(TSource item)
        {
            var index = List.DataSource.Count() - PlaceHoldersCount;
            return List.Insert(index, item);
        }

        int PlaceHoldersCount => (int)Math.Floor(ItemsToDisplay / 2.0);

        void HighlightMiddleItem()
        {
            var middle = FindMiddleItem();
            if (middle != null && SelectedRow != middle) HighlightItem(middle);
        }

        TTemplate FindMiddleItem()
        {
            var scrollMiddle = Scroller.ScrollY + ActualHeight / 2;
            return List.ItemViews.WithMin(x => Math.Abs((x.ActualY + x.ActualHeight / 2) - scrollMiddle));
        }

        void HighlightItem(TTemplate item)
        {
            SelectedRow = item;

            List.ItemViews.Except(item).Do(x => x.UnsetPseudoCssState("active"));
            item.Perform(x => x.SetPseudoCssState("active").RunInParallel());
        }

        public override async Task OnPreRender()
        {
            await base.OnPreRender();
            var item = List.ItemViews.FirstOrDefault();
            if (item != null)
                Scroller.Height.BindTo(item.Height, x => item.CalculateTotalHeight() * ItemsToDisplay);
        }

        public Task PreSelect(Func<TSource, bool> selectedCriteria)
        {
            var item = List.ItemViews.FirstOrDefault(x => selectedCriteria(x.Item));

            if (item == null)
                Log.For(this).Warning("The selected item was not found in the list of items.");

            return PreSelect(item);
        }

        Task PreSelect(TTemplate item)
        {
            if (item == null) return Task.CompletedTask;

            HighlightItem(item);
            return SetScrollPosition();
        }

        Task SetScrollPosition()
        {
            var index = List.ItemViews.IndexOf(SelectedRow);
            if (index == -1)
            {
                Log.For(this).Error("Item '" + SelectedItem + "' does not exist in this rotator's list of items.");
                return Task.CompletedTask;
            }

            index -= PlaceHoldersCount;
            return Scroller.ScrollTo(yOffset: index * ItemHeight, animate: true);
        }

        float ItemHeight => List.ItemViews.FirstOrDefault()?.ActualHeight ?? 0;

        public IEnumerable<TSource> Source => List.DataSource.Take(PlaceHoldersCount, 1 + List.DataSource.Count() - 2 * PlaceHoldersCount);

        AsyncEvent IAutoContentHeightProvider.Changed => AutoContentHeightChanged;

        float IAutoContentHeightProvider.Calculate() => ItemHeight * ItemsToDisplay;

        bool IAutoContentHeightProvider.DependsOnChildren() => false;

        public override void Dispose()
        {
            AutoContentHeightChanged?.Dispose();
            SelectionChanged?.Dispose();
            Scroller.ScrollEnded?.Dispose();
            Scroller.UserScrolledVertically?.Dispose();

            base.Dispose();
        }
    }
}