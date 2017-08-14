namespace Zebble
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class Rotator<TSource, TTemplate> : View, IAutoContentHeightProvider
        where TTemplate : View, IListViewItem<TSource>, new()
        where TSource : class, new()
    {
        readonly AsyncEvent AutoContentHeightChanged = new AsyncEvent();
        public int ItemsToDisplay = 3;

        public readonly AsyncEvent SelectionChanged = new AsyncEvent(ConcurrentEventRaisePolicy.Queue);

        public readonly ScrollView Scroller = new ScrollView { PartialPagingEnabled = true, PagingEnabled = true };
        public readonly ListView<TSource, TTemplate> List = new ListView<TSource, TTemplate>();

        public override async Task OnInitializing()
        {
            await base.OnInitializing();

            await Add(Scroller);
            await Scroller.Add(List);

            Scroller.UserScrolledVertically.HandleWith(SelectTheMiddleItem);
            Scroller.ScrollEnded.HandleWith(SelectTheMiddleItem);
        }

        public TSource SelectedItem { get; private set; }

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

        void SelectTheMiddleItem()
        {
            var middle = FindMiddleItem();
            if (middle == null) return;
            if (SelectedItem == middle.Item) return;

            HighlightItem(middle);

            SelectedItem = middle.Item;
            SelectionChanged.Raise();
        }

        TTemplate FindMiddleItem()
        {
            var scrollMiddle = Scroller.ScrollY + ActualHeight / 2;

            return List.ItemViews.WithMin(x => Math.Abs((x.ActualY + x.ActualHeight / 2) - scrollMiddle));
        }

        void HighlightItem(TTemplate item)
        {
            List.ItemViews.Except(item).Do(x => x.UnsetPseudoCssState("active"));
            item.Perform(x => x.SetPseudoCssState("active").RunInParallel());
        }

        public override async Task OnPreRender()
        {
            await base.OnPreRender();
            var item = List.ItemViews.FirstOrDefault();
            if (item != null)
            {
                Scroller.Height.BindTo(item.Height, x =>
                {
                    Scroller.PartialPagingSize = item.CalculateTotalHeight();
                    return Scroller.PartialPagingSize * ItemsToDisplay;
                });
            }
        }

        public void PreSelect(Func<TSource, bool> selectedCriteria)
        {
            PreSelect(List.ItemViews.FirstOrDefault(x => selectedCriteria(x.Item)));
        }

        void PreSelect(TTemplate item)
        {
            if (item == null) return;

            SelectedItem = item.Item;

            var index = List.ItemViews.IndexOf(item);
            if (index == -1)
            {
                Device.Log.Error("Item '" + item + "' does not exist in this rotator's list of items.");
                return;
            }

            index -= PlaceHoldersCount;

            Scroller.ScrollY = index * ItemHeight;
            HighlightItem(item);
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