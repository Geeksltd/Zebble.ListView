using System;
using System.Linq;
using System.Threading.Tasks;
using Olive;

namespace Zebble
{
    partial class CollectionView<TSource>
    {
        Guid LayoutVersion;

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

            (Direction == RepeatDirection.Horizontal ? result.Width : result.Height)
                .Changed.Handle(ReLayoutIfShown);

            return result;
        }

        protected virtual View[] ViewItems() => AllChildren.Except(FindEmptyTemplate()).ToArray();

        public override async Task OnRendered()
        {
            await base.OnRendered();
            await (FindEmptyTemplate()?.IgnoredAsync(!source.None()) ?? Task.CompletedTask);

            await WhenShown(async () =>
            {
                HandleScrolling();
                await UpdateLayout();
            });
        }

        internal Task ReLayoutIfShown() => IsShown ? UpdateLayout() : Task.CompletedTask;

        async Task UpdateLayout()
        {
            var layoutVersion = LayoutVersion = Guid.NewGuid();

            if (source.None())
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
                    await UIWorkBatch.Run(() => Arrange(layoutVersion));
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
            if (source.None())
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

        async Task Arrange(ViewItem[] mapping, Guid layoutVersion)
        {
            var visibleFrom = 0f;
            var visibleTo = float.MaxValue;

            if (!IsNested() && Scroller != null)
            {
                if (Horizontal)
                {
                    visibleFrom = Scroller.ScrollX - (ActualX - Scroller.ActualX);
                    visibleTo = visibleFrom + Scroller.ActualWidth;
                }
                else
                {
                    visibleFrom = Scroller.ScrollY - (ActualY - Scroller.ActualY);
                    visibleTo = visibleFrom + Scroller.ActualHeight;
                }

                visibleFrom = (visibleFrom - OverRenderBuffer()).LimitMin(0);
                visibleTo += OverRenderBuffer();
            }

            mapping.Do(x => x.IsInUse = false);

            var counter = -1;
            foreach (var vm in source)
            {
                counter++;
                var position = ItemPositionOffsets.GetOrDefault(counter);
                lock (position)
                {
                    if (position == null && ItemPositionOffsets.Any())
                    {
                        var firstItem = ItemPositionOffsets.FirstOrDefault();
                        position = new Range<float>(firstItem.Value.From, firstItem.Value.To);
                    }
                    if (position.From > visibleTo) break;
                    if (position.To < visibleFrom) continue;
                }
                var item = mapping.FirstOrDefault(v => v.Item == vm);
                if (item == null)
                {
                    var requiredType = GetViewType(vm);
                    item = mapping.FirstOrDefault(x => !x.IsInUse && x.View.GetType() == requiredType);

                    if (item == null)
                        item = new ViewItem(CreateItemView(vm));

                    item.Load(vm);
                }
                item.IsInUse = true;
                if (Horizontal) item.View.X.Set(position.From);
                else item.View.Y.Set(position.From);

                await item.View.IgnoredAsync(false);
                if (item.View.Parent == null)
                {
                    await Add(item.View);
                    if (layoutVersion != LayoutVersion) return;
                }
            }
        }

        protected virtual float OverRenderBuffer() => 50;

        protected virtual EmptyTemplate FindEmptyTemplate() => FindDescendent<EmptyTemplate>();
    }
}