namespace Zebble
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    public class ListViewItem<TSource> : ListViewItem, IListViewItem<TSource>
    {
        public TSource Item { get; set; }
    }

    public class ListViewItem : Canvas
    {
        bool SlideVisible = false;
        float OriginalContentX;
        public TimeSpan SwipeAnimationDuration = Animation.DefaultListItemSlideDuration;
        public readonly Stack Content = new Stack { Direction = RepeatDirection.Horizontal, Id = "Content" };

        public readonly Stack SlideIn = new Stack
        {
            Direction = RepeatDirection.Horizontal,
            Id = "SlideIn"
        }.Absolute();

        public override async Task OnInitializing()
        {
            await base.OnInitializing();
            await base.AddAt(0, Content);
        }

        protected override async Task ApplyDefaultFlash()
        {
            using (Stylesheet.Preserve(this, x => x.BackgroundColor))
            {
                this.Background(color: "#eeeeee");
                await base.ApplyDefaultFlash();
            }
        }

        async Task Swipped(SwipedEventArgs args)
        {
            if (args.Direction == Direction.Left) await ShowSlideInContent();
            else if (args.Direction == Direction.Right) await HideSlideInContent();
        }

        public override async Task OnPreRender()
        {
            await base.OnPreRender();
            if (SlideIn.AllChildren.Any()) Swiped.Handle(Swipped);
        }

        async Task ShowSlideInContent()
        {
            if (SlideVisible) return;
            else SlideVisible = true;

            if (SlideIn.parent == null)
            {
                SlideIn.Size(100.Percent());
                await base.AddAt(1, SlideIn, awaitNative: true); // Lazy loaded the first time only.
            }

            var slideInWidth = SlideIn.CurrentChildren.Sum(c => c.ActualWidth);
            OriginalContentX = Content.ActualX;

            await Task.WhenAll(
                Content.Animate(SwipeAnimationDuration, x => x.X(-slideInWidth)),
                SlideIn.Animate(SwipeAnimationDuration, x => x.X((CalculateAbsoluteX() + ActualWidth - slideInWidth) - Padding.Right()))
            );
        }

        async Task HideSlideInContent()
        {
            if (!SlideVisible) return;
            else SlideVisible = false;


            await Task.WhenAll(
                Content.Animate(SwipeAnimationDuration, x => x.X(OriginalContentX)),
                SlideIn.Animate(SwipeAnimationDuration, x => x.X(Device.Screen.Width))
            );
        }

        public override async Task<TView> Add<TView>(TView child, bool awaitNative = false) => await Content.Add(child, awaitNative);

        public override async Task<TView> AddAt<TView>(int index, TView child, bool awaitNative = false)
        {
            index--; // Content
            if (SlideIn.parent != null) index--; // SlideIn
            return await Content.AddAt(index, child, awaitNative).DropContext();
        }

        public override bool CanCancelTouches => SlideVisible;

        public override void RaiseTapped(TouchEventArgs args)
        {
            if (SlideVisible) HideSlideInContent().RunInParallel();
            else base.RaiseTapped(args);
        }

        public override void Dispose()
        {
            if (SlideIn?.IsDisposed == false)
                SlideIn?.Dispose();

            base.Dispose();
        }
    }
}