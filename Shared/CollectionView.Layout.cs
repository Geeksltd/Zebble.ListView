using System;
using System.Linq;
using System.Threading.Tasks;
using Olive;

namespace Zebble
{
    partial class CollectionView<TSource>
    {
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
                await Layout();
                AllChildren.Do(x => x.Height.Changed.Handle(Layout));
            });
        }

        public async Task Layout()
        {
            if (IsShown)
            {
                await MeasureItems();
                await UIWorkBatch.Run(Arrange);
            }
        }

        async Task LoadEmptyTemplate()
        {
            await (FindEmptyTemplate()?.IgnoredAsync(false) ?? Task.CompletedTask);
            foreach (var item in CurrentChildren.Except(FindEmptyTemplate()))
                await item.IgnoredAsync();
        }

        protected virtual async Task Arrange()
        {
            if (source.None())
            {
                await LoadEmptyTemplate();
                return;
            }

            var mapping = ViewItems().Select(v => new ViewItem(v)).ToArray();
            await Arrange(mapping);

            foreach (var item in mapping.Except(x => x.IsInUse).ToArray())
                await item.View.IgnoredAsync();
        }

        async Task Arrange(ViewItem[] mapping)
        {
            var visibleFrom = 0f;
            var visibleTo = float.MaxValue;

            if (!IsNested() && Scroller != null)
            {
                if (Horizontal)
                {
                    visibleFrom = Scroller.ScrollX;
                    visibleTo = Scroller.ScrollX + Scroller.ActualWidth;
                }
                else
                {
                    visibleFrom = Scroller.ScrollY;
                    visibleTo = Scroller.ScrollY + Scroller.ActualHeight;
                }

                visibleFrom = (visibleFrom - OverRenderBuffer()).LimitMin(0);
                visibleTo += OverRenderBuffer();
            }

            var counter = -1;
            foreach (var vm in source)
            {
                counter++;
                var position = ItemPositionOffsets.GetOrDefault(counter);
                if (position.From > visibleTo) break;
                if (position.To < visibleFrom) continue;

                var item = mapping.FirstOrDefault(v => v.Item == vm);
                if (item == null)
                {
                    var wantedType = GetViewType(vm);
                    item = mapping.FirstOrDefault(x => !x.IsInUse && x.View.GetType() == wantedType);

                    if (item == null)
                    {
                        item = new ViewItem(CreateItemView(vm));
                    }

                    item.Load(vm);
                }

                item.IsInUse = true;
                if (Horizontal) item.View.X.Set(position.From);
                else item.View.Y.Set(position.From);

                await item.View.IgnoredAsync(false);
                if (item.View.Parent == null)
                {
                    await Add(item.View);
                    if (IsShown) item.View.Height.Changed.Handle(Layout);
                }
            }
        }

        protected virtual float OverRenderBuffer() => 50;

        protected virtual EmptyTemplate FindEmptyTemplate() => FindDescendent<EmptyTemplate>();
    }
}