using System;
using System.Threading.Tasks;

namespace Zebble
{
    partial class CollectionView<TSource>
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

        protected virtual void HandleScrolling()
        {
            if (Scroller == null) return;

            //Scroller.UserScrolledVertically.HandleOn(Thread.UI, () => OnUserScrolledVertically());
            Scroller.UserScrolledHorizontally.Handle(() => OnUserScrolledVertically());
            Scroller.ScrollEnded.HandleOn(Thread.UI, () => OnUserScrolledVertically(mandatory: true));
        }

        async Task OnUserScrolledVertically(bool mandatory = false)
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
                await UIWorkBatch.Run(Arrange);
            }
            finally
            {
                IsProcessingLazyLoading = false;
            }
        }

        public async Task<bool> ScrollToItem(TSource viewModel, bool animate = false)
        {
            var index = source.OrEmpty().IndexOf(viewModel);
            if (index == -1) return false;

            await Scroller.ScrollTo(ItemYPositions.GetOrDefault(index), 0, animate);
            await Arrange();
            return true;
        }
    }
}
