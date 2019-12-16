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
        bool RightSlideVisible, LeftSlideVisible, ShouldRelease, ShouldTapBegin;
        float? OriginalContentX;

        public TimeSpan SwipeAnimationDuration = Animation.DefaultListItemSlideDuration;
        public readonly Stack Content = new Stack { Direction = RepeatDirection.Horizontal, Id = "Content" };

        public readonly Stack RightSlideIn = new Stack
        {
            Direction = RepeatDirection.Horizontal,
            Id = "RightSlideIn"
        }.Absolute();

        public readonly Stack LeftSlideIn = new Stack
        {
            Direction = RepeatDirection.Horizontal,
            Id = "LeftSlideIn"
        }.Absolute();

        public override async Task OnInitializing()
        {
            await base.OnInitializing();
            await base.AddAt(0, Content);
        }

        protected override async Task ApplyDefaultFlash()
        {
            var effectiveBackground = WithAllParents().Select(x => x.BackgroundColor).Except(x => x.IsTransparent()).FirstOrDefault();
            if (effectiveBackground == null) return;

            using (Stylesheet.Preserve(this, x => x.BackgroundColor))
            {
                this.Background(effectiveBackground.Darken(20));
                await base.ApplyDefaultFlash();
            }
        }

        async Task OnPanning(PannedEventArgs args)
        {
            var direction = await CheckDirection(args.Velocity);
            if (direction == null) return;

            var distance = args.From.X - args.To.X;
            if (direction == Direction.Left) await ShowRightSlideInContent(distance);
            else if (direction == Direction.Right) await ShowLeftSlideInContent(distance);
        }

        async Task OnPanFinished(PannedEventArgs args)
        {
            if (ShouldTapBegin)
            {
                if (RightSlideVisible)
                {
                    RightSlideIn.AllChildren.First().RaiseTapped();
                }
                else if (LeftSlideVisible)
                {
                    LeftSlideIn.AllChildren.First().RaiseTapped();
                }

                ShouldTapBegin = false;
            }

            if (ShouldRelease)
            {
                RightSlideVisible = LeftSlideVisible = true;
                await HideSlideInContent();
            }
        }

        Task<Direction?> CheckDirection(Point velocity)
        {
            Direction? result;
            if (velocity.X > 0) result = Direction.Right;
            else if (velocity.X < 0) result = Direction.Left;
            else result = null;

            return Task.FromResult(result);
        }

        public override async Task OnPreRender()
        {
            await base.OnPreRender();

            Panning.Handle(OnPanning);
            PanFinished.Handle(OnPanFinished);
        }

        async Task ShowRightSlideInContent(double distance)
        {
            if (RightSlideVisible) return;
            else if (LeftSlideVisible) { await HideSlideInContent(); return; }

            if (!RightSlideIn.AllChildren.Any()) return;

            if (RightSlideIn.AllChildren.Count == 1)
            {
                if (Content.X.CurrentValue <= -(Content.ActualWidth / 2))
                {
                    ShouldTapBegin = RightSlideVisible = true;
                    return;
                }
                else ShouldRelease = true;
            }
            else if (RightSlideIn.AllChildren.Count > 1 && RightSlideIn.parent != null)
            {
                var slideInWidth = RightSlideIn.CurrentChildren.Sum(c => c.ActualWidth);
                if (RightSlideIn.X.CurrentValue <= (CalculateAbsoluteX() + ActualWidth - slideInWidth) - Padding.Right())
                {
                    RightSlideVisible = true;
                    ShouldRelease = false;
                    return;
                }
                else ShouldRelease = true;
            }

            // First time only.
            if (RightSlideIn.parent == null)
            {
                RightSlideIn.Size(100.Percent()).X(CalculateAbsoluteX() + ActualWidth);
                await base.AddAt(AllChildren.Count, RightSlideIn, awaitNative: true); // Lazy loaded the first time only.
                if (OriginalContentX == null) OriginalContentX = Content.ActualX;
            }

            Content.X(Content.X.CurrentValue - distance);
            RightSlideIn.X(RightSlideIn.X.CurrentValue - distance);
        }

        async Task ShowLeftSlideInContent(double distance)
        {
            if (LeftSlideVisible) return;
            else if (RightSlideVisible) { await HideSlideInContent(); return; }

            if (!LeftSlideIn.AllChildren.Any()) return;
            float slideInWidth;

            if (LeftSlideIn.AllChildren.Count == 1)
            {
                if (Content.X.CurrentValue >= Content.ActualWidth / 2)
                {
                    ShouldTapBegin = LeftSlideVisible = true;
                    return;
                }
                else ShouldRelease = true;
            }
            else if (LeftSlideIn.AllChildren.Count > 1 && LeftSlideIn.parent != null)
            {
                slideInWidth = LeftSlideIn.CurrentChildren.Sum(c => c.ActualWidth);
                if (LeftSlideIn.X.CurrentValue >= 0)
                {
                    LeftSlideVisible = true;
                    ShouldRelease = false;
                    return;
                }
                else ShouldRelease = true;
            }

            // First time only.
            if (LeftSlideIn.parent == null)
            {
                await base.AddAt(AllChildren.Count, LeftSlideIn, awaitNative: true); // Lazy loaded the first time only.
                if (OriginalContentX == null) OriginalContentX = Content.ActualX;

                slideInWidth = LeftSlideIn.CurrentChildren.Sum(c => c.ActualWidth);
                LeftSlideIn.Size(100.Percent()).X(-(slideInWidth + Padding.Left()));
            }

            Content.X(Content.X.CurrentValue - distance);
            LeftSlideIn.X(LeftSlideIn.X.CurrentValue - distance);
        }

        async Task HideSlideInContent()
        {
            if (LeftSlideVisible && RightSlideVisible) await Task.WhenAll(HideLeftSlideInContent(), HideRightSlideInContent());
            if (LeftSlideVisible) await HideLeftSlideInContent();
            else if (RightSlideVisible) await HideRightSlideInContent();

            ShouldRelease = RightSlideVisible = LeftSlideVisible = false;
        }

        async Task HideRightSlideInContent()
        {
            await Task.WhenAll(
                Content.Animate(SwipeAnimationDuration, x => x.X(OriginalContentX)),
                RightSlideIn.Animate(SwipeAnimationDuration, x => x.X(CalculateAbsoluteX() + ActualWidth)));
        }

        async Task HideLeftSlideInContent()
        {
            var slideInWidth = LeftSlideIn.CurrentChildren.Sum(c => c.ActualWidth);

            await Task.WhenAll(
            Content.Animate(SwipeAnimationDuration, x => x.X(OriginalContentX)),
            LeftSlideIn.Animate(SwipeAnimationDuration, x => x.X(-slideInWidth)));
        }

        public override async Task<TView> Add<TView>(TView child, bool awaitNative = false) => await Content.Add(child, awaitNative);

        public override async Task<TView> AddAt<TView>(int index, TView child, bool awaitNative = false)
        {
            index--; // Content
            if (RightSlideIn.parent != null || LeftSlideIn.parent != null) index--; // SlideIn
            return await Content.AddAt(index, child, awaitNative).DropContext();
        }

        public override bool CanCancelTouches => RightSlideVisible || LeftSlideVisible;

        public override void RaiseTapped(TouchEventArgs args)
        {
            if (RightSlideVisible || LeftSlideVisible) HideSlideInContent().RunInParallel();
            else base.RaiseTapped(args);
        }

        public override void Dispose()
        {
            if (RightSlideIn?.IsDisposed == false)
                RightSlideIn?.Dispose();

            if (LeftSlideIn?.IsDisposed == false)
                LeftSlideIn?.Dispose();

            base.Dispose();
        }
    }
}