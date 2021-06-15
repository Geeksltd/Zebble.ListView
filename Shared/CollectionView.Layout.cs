using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Olive;

namespace Zebble
{
    partial class CollectionView<TSource>
    {
        Guid LayoutVersion;

        EmptyTemplate emptyTemplate;
        DateTime nextLayoutSchedule;

        public class EmptyTemplate : Canvas { }

        protected virtual View CreateItemView(TSource viewModel)
        {
            var type = GetViewType(viewModel);
            if (!type.IsA<ITemplate>())
                throw new Exception(type.GetProgrammingName() + " does not implement ITemplate.");

            var result = type.CreateInstance<View>();

            result.SetViewModelValue(viewModel);

            result.Ignored = true;

            if (result.Css.Width is Length.AutoLengthRequest auto && auto.Strategy == Length.AutoStrategy.Container)
                result.Css.Width(new Length.AutoLengthRequest(Length.AutoStrategy.Content));

            if (Horizontal)
            {
                result.Width.Changed.Handle(() => ReLayoutIfShown("Width Changed", result));
                result.Margin.Left.Changed.Handle(() => ReLayoutIfShown("Margin Left Changed", result));
                result.Margin.Right.Changed.Handle(() => ReLayoutIfShown("Margin Right Changed", result));
            }
            else
            {
                result.Height.Changed.Handle(() => ReLayoutIfShown("Height Changed", result));
                result.Margin.Top.Changed.Handle(() => ReLayoutIfShown("Margin Top Changed", result));
                result.Margin.Bottom.Changed.Handle(() => ReLayoutIfShown("Margin Bottom Changed", result));
            }

            return result;
        }

        protected virtual View[] ViewItems() => AllChildren.Except(FindEmptyTemplate()).ToArray();

        public override async Task OnRendered()
        {
            await base.OnRendered();
            await (FindEmptyTemplate()?.IgnoredAsync(OnSource(x => x.Any())) ?? Task.CompletedTask);

            await WhenShown(async () =>
            {
                HandleScrolling();
                await UpdateLayout();
            });
        }

        internal async Task ReLayoutIfShown(string origin, View view = null)
        {
            if (!IsShown) return;
            if (LocalTime.Now < nextLayoutSchedule)
                return;

            nextLayoutSchedule = LocalTime.Now.AddMilliseconds(16);
            await Task.Delay(16);

            if (LocalTime.Now < nextLayoutSchedule)
                return;

            await UpdateLayout();

        }

        //Add a wrapper method to log origins of this
        internal Task ReLayoutIfShown() => IsShown ? UpdateLayout() : Task.CompletedTask;

        async Task UpdateLayout()
        {
            if (IsCreatingItem)
                return;

            var layoutVersion = LayoutVersion = Guid.NewGuid();

            if (OnSource(x => x.None()))
            {
                await LoadEmptyTemplate(layoutVersion);
                if (layoutVersion == LayoutVersion)
                    ResizeToEmptyTemplate();
            }
            else
            {
                await MeasureOffsets(layoutVersion);
                if (layoutVersion == LayoutVersion)
                {
                    (Horizontal ? Width : Height).Set(GetTotalSize());

                    await BatchArrange(layoutVersion, "From UpdateLayout");
                }
            }
        }

        float GetTotalSize()
        {
            var lastItem = ItemPositionOffsets.LastOrDefault();
            var result = lastItem.Value?.To ?? (Horizontal ? Padding.Left() : Padding.Top());
            result += Horizontal ? Padding.Right() : Padding.Bottom();
            return result;
        }

        async Task LoadEmptyTemplate(Guid layoutVersion)
        {
            var template = FindEmptyTemplate();

            if (template != null) await template.IgnoredAsync(false);

            foreach (var item in CurrentChildren.Except(template))
            {
                if (LayoutVersion != layoutVersion) return;
                await item.IgnoredAsync();
            }
        }

        protected virtual async Task Arrange(Guid layoutVersion)
        {
            Debug.WriteLine($"Arrange method calls: {layoutVersion}");

            if (OnSource(x => x.None()))
            {
                await LoadEmptyTemplate(layoutVersion);
            }
            else
            {
                var mapping = ViewItems().Select(v => new ViewItem(v)).ToArray();
                await Arrange(mapping, layoutVersion);

                foreach (var item in mapping.Except(x => x.IsInUse).ToArray())
                {
                    if (LayoutVersion != layoutVersion) return;
                    await item.View.IgnoredAsync();
                }
            }
        }

        Range<float> GetVisibleRange()
        {
            var from = 0f;
            var to = float.MaxValue;

            if (!IsNested() && Scroller != null)
            {
                if (Horizontal)
                {
                    from = Scroller.ScrollX - (ActualX - Scroller.ActualX);
                    to = from + Scroller.ActualWidth;
                }
                else
                {
                    from = Scroller.ScrollY - (ActualY - Scroller.ActualY);
                    to = from + Scroller.ActualHeight;
                }

                from = (from - OverRenderBuffer()).LimitMin(0);
                to += OverRenderBuffer();
            }

            return new Range<float>(from, to);
        }

        //bool ShowLog;

        async Task Arrange(ViewItem[] mapping, Guid layoutVersion)
        {
            Debug.WriteLine($"Arrange mapping method calls: {layoutVersion}");

            if (layoutVersion != LayoutVersion) return;

            var visibleRange = GetVisibleRange();

            //if (ShowLog)
            //{
            //    if (layoutVersion != LayoutVersion) return;
            //    Debug.WriteLine($"visibleRange.From={visibleRange.From}, visibleRange.To={visibleRange.To}");
            //    foreach (var item in ItemPositionOffsets.ToArray())
            //    {
            //        var isInRange = item.Value.From <= visibleRange.To && item.Value.To >= visibleRange.From;

            //        Debug.WriteLine($"ItemPositionOffsets[{item.Key.ToString().PadLeft(2, '0')}]=" + item.Value + "*".OnlyWhen(isInRange));
            //    }
            //}

            mapping.Do(x => x.IsInUse = false);

            var counter = -1;
            foreach (var vm in OnSource(x => x.ToArray()))
            {
                //if (layoutVersion != LayoutVersion) return;

                counter++;

                var position = ItemPositionOffsets.GetOrDefault(counter);

                if (position is null)
                {
                    if (ItemPositionOffsets.Any())
                    {
                        var firstItem = ItemPositionOffsets.FirstOrDefault();
                        position = new Range<float>(firstItem.Value.From, firstItem.Value.To);
                    }

                    //if (position is null) break;
                }

                var from = position.From;
                var to = position.To;

                if (position.From > visibleRange.To) break;
                if (position.To < visibleRange.From) continue;

                var item = mapping.FirstOrDefault(v => v.Item == vm);
                if (item is null)
                {
                    var requiredType = GetViewType(vm);
                    item = mapping.FirstOrDefault(x => !x.IsInUse && x.View.GetType() == requiredType);

                    item ??= new ViewItem(CreateItemView(vm));
                    if (item.Item != vm)
                        item.Load(vm);

                    //await item.View.ReusedInCollectionView.Raise();
                }


                item.IsInUse = true;
                if (Horizontal) item.View.X.Set(position.From);
                else item.View.Y.Set(position.From);

                Debug.WriteLine($"Set vertical align: {position.From}");


                await item.View.IgnoredAsync(false);


                if (item.View.Parent == null)
                    await Add(item.View);
            }
        }

        protected virtual float OverRenderBuffer() => 50;

        protected virtual EmptyTemplate FindEmptyTemplate() => emptyTemplate ??= FindDescendent<EmptyTemplate>();
    }
}