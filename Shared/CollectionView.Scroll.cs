using System;
using System.Threading.Tasks;
using System.Linq;
using Olive;
using System.Diagnostics;
using System.Threading;

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

            Scroller.ApiScrolledTo.Event += () => OnUserScrolled();
            Scroller.UserScrolledVertically.Event += () => OnUserScrolled();
            Scroller.UserScrolledHorizontally.Event += () => OnUserScrolled();
            Scroller.ScrollEnded.Event += () => OnUserScrolled(mandatory: true);
        }

        async void OnUserScrolled(bool mandatory = false)
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

            Task.Run(() => BatchArrange(LayoutVersion, "From OnUserScrolled"))
            .WithTimeout(TimeSpan.FromSeconds(1), timeoutAction: () => IsProcessingLazyLoading = false)
            .ContinueWith((t) => IsProcessingLazyLoading = false)
            .RunInParallel();
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
    }
}
