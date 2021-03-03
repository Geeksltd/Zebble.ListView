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

            Scroller.UserScrolledVertically.Event += () => OnUserScrolled().RunInParallel();
            Scroller.UserScrolledHorizontally.Event += () => OnUserScrolled().RunInParallel();
            Scroller.ScrollEnded.Event += () => OnUserScrolled(mandatory: true).RunInParallel();
        }

        async Task OnUserScrolled(bool mandatory = false)
        {
            if (IsProcessingLazyLoading)
            {
                if (!mandatory) return;
                while (IsProcessingLazyLoading)
                    await Task.Delay(Animation.OneFrame);
            }

            if (IsDisposing) return;

            IsProcessingLazyLoading = true;

            try
            {
                await UIWorkBatch.Run(() => Arrange(LayoutVersion));
            }
            finally
            {
                IsProcessingLazyLoading = false;
            }
        }

        public async Task<bool> ScrollToItem(TSource viewModel, bool animate = false)
        {
            if (source is null) return false;
            var index = OnSource(x => x.OrEmpty().IndexOf(viewModel));
            if (index == -1) return false;

            var offset = ItemPositionOffsets.GetOrDefault(index);

            if (Horizontal)
                await Scroller.ScrollTo(0, offset.From, animate);
            else
                await Scroller.ScrollTo(offset.From, 0, animate);

            await Arrange(LayoutVersion);
            return true;
        }
    }
}
