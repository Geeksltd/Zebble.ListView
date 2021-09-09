using System;
using System.Threading.Tasks;
using System.Linq;
using Olive;

namespace Zebble
{
    internal interface ICollectionView { }

    partial class CollectionView<TSource> : ICollectionView
    {
        bool IsProcessingLazyLoading;
        DateTime nextScrollSchedule;
        ScrollView scroller;
        ScrollView Scroller
        {
            get
            {
                scroller = scroller ?? FindParent<ScrollView>();
                if (scroller != null)
                    return scroller.IsDisposing ? null : scroller;

                if (IsDisposing || Parent == null) return null;
                throw new Exception("Lazy loaded list view must be inside a scroll view");
            }
        }

        bool IsNested() => GetAllParents().OfType<ICollectionView>().Any();

        protected virtual void HandleScrolling()
        {
            if (Scroller == null) return;

            Scroller.ApiScrolledTo.Event += async () => await OnApiScrolledTo();
            Scroller.UserScrolledVertically.Event += async () => await OnUserScrolled();
            Scroller.UserScrolledHorizontally.Event += async () => await OnUserScrolled();
            Scroller.ScrollEnded.Event += async () => await OnUserScrolled(mandatory: true);
        }

        async Task OnApiScrolledTo()
        {
            if ((Scroller.ScrollY == 0 && Scroller.ScrollX == 0) ||
                (Scroller.ScrollY == 0 && Scroller.ScrollX.AlmostEquals(Scroller.CalculateContentSize().Width)))
                await OnUserScrolled(mandatory: true);
            else
                await OnUserScrolled();
        }

        async Task OnUserScrolled(bool mandatory = false, bool shouldWait = false)
        {
            if (LocalTime.Now < nextScrollSchedule)
                return;

            nextScrollSchedule = LocalTime.Now.AddMilliseconds(16);
            await Task.Delay(16);

            if (LocalTime.Now < nextScrollSchedule)
                return;

            if (mandatory)
                LayoutVersion = Guid.NewGuid();

            if (IsProcessingLazyLoading)
            {
                if (!mandatory) return;
                while (IsProcessingLazyLoading)
                    await Task.Delay(Animation.OneFrame);
            }

            if (IsDisposing) return;

            IsProcessingLazyLoading = true;

            if (!shouldWait)
                UpdateLayoutWhileScrollChanged().RunInParallel();
            else
                await UpdateLayoutWhileScrollChanged();
        }

        Task UpdateLayoutWhileScrollChanged()
        {
            return Task.Run(() => BatchArrange(LayoutVersion, "From OnUserScrolled"))
            .WithTimeout(1.Seconds(), timeoutAction: () => IsProcessingLazyLoading = false)
            .ContinueWith((t) => IsProcessingLazyLoading = false);
        }

        public async Task<bool> ScrollToItem(TSource viewModel, bool animate = false)
        {
            if (Scroller == null) return false;
            if (source is null) return false;

            var index = OnSource(x => x.OrEmpty().IndexOf(viewModel));
            if (index == -1) return false;

            var offset = ItemPositionOffsets.GetOrDefault(index);
            if (offset == null) return false;

            if (Horizontal)
                await Scroller.ScrollTo(0, offset.From, animate);
            else
                await Scroller.ScrollTo(offset.From, 0, animate);

            await Arrange(LayoutVersion);
            return true;
        }

        public async Task ScrollToPosition(float offset, bool shouldScrollEnded = false, bool animate = false)
        {
            if (Scroller == null) return;

            if (Horizontal)
                await Scroller.ScrollTo(0, offset, animate);
            else
                await Scroller.ScrollTo(offset, 0, animate);

            if (shouldScrollEnded)
                Thread.UI.Post(async () => await OnUserScrolled(mandatory: true, shouldWait: true));
        }

        public void RefreshLayout() => Thread.UI.Post(async () => await UpdateLayoutWhileScrollChanged());
    }
}
