using System;
using System.Threading.Tasks;
using System.Linq;
using Olive;

namespace Zebble
{
    internal interface ICollectionView { }

    partial class CollectionView<TSource> : ICollectionView
    {
        DateTime LastUpdateInvokedAt;

        ScrollView scroller;
        ScrollView Scroller
        {
            get
            {
                scroller ??= FindParent<ScrollView>();
                if (scroller != null)
                    return scroller.IsDisposing ? null : scroller;

                if (IsDisposing || Parent == null) return null;
                throw new Exception("Lazy loaded list view must be inside a scroll view");
            }
        }

        bool IsNested() => GetAllParents().OfType<ICollectionView>().Any();

        protected virtual void HandleScrolling()
        {
            if (Scroller == null) 
                return;

            Scroller.ApiScrolledTo.Event += async () => await OnApiScrolledTo();
            Scroller.UserScrolledVertically.Event += async () => await OnUserScrolled();
            Scroller.UserScrolledHorizontally.Event += async () => await OnUserScrolled();
            Scroller.ScrollEnded.Event += async () => await OnUserScrolled();
        }

        async Task OnApiScrolledTo()
        {
            if ((Scroller.ScrollY == 0 && Scroller.ScrollX == 0) ||
                (Scroller.ScrollY == 0 && Scroller.ScrollX.AlmostEquals(Scroller.CalculateContentSize().Width)))
                await OnUserScrolled();

            else
                await OnUserScrolled();
        }

        bool IsArraging;

        async Task OnUserScrolled()
        {
            Scrolling = true;

            if (IsDisposing)
                return;

            var localLastUpdateInvokedAt = LastUpdateInvokedAt = DateTime.UtcNow;

            if (!IsArraging)
            {
                IsArraging = true;

                await BatchArrange(LayoutVersion);

                IsArraging = false;
                Scrolling = false;

                return;
            }

            await Task.Delay(40);
            if (LastUpdateInvokedAt > localLastUpdateInvokedAt)
                return;

            IsArraging = true;

            await BatchArrange(LayoutVersion);

            IsArraging = false;
            Scrolling = false;
        }

        public async Task<bool> ScrollToItem(TSource viewModel, bool animate = false)
        {
            if (Scroller == null) 
                return false;

            if (source is null) 
                return false;

            var index = OnSource(x => x.OrEmpty().IndexOf(viewModel));
            if (index == -1) 
                return false;

            var offset = ItemPositionOffsets.GetOrDefault(index);
            if (offset == null) 
                return false;

            if (Horizontal)
                await Scroller.ScrollTo(0, offset.From, animate);
            else
                await Scroller.ScrollTo(offset.From, 0, animate);

            await Arrange(LayoutVersion);
            return true;
        }

        public async Task ScrollToPosition(float offset, bool shouldScrollEnded = false, bool animate = false)
        {
            if (Scroller == null) 
                return;

            if (Horizontal)
                await Scroller.ScrollTo(0, offset, animate);
            else
            {
                if (Scroller.IsContentHeightShorterThanActualHeight())
                    offset = 0;
                await Scroller.ScrollTo(offset, 0, animate);
            }

            if (shouldScrollEnded)
                Thread.UI.Post(async () => await OnUserScrolled());
        }
    }
}
