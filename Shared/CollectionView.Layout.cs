﻿using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Zebble
{
    public static class TempExtensions
    {
        public static T GetValue<T>(this IBindable @this)
        {
            return (T)@this.GetType().GetProperty("Value").GetValue(@this);
        }
    }

    partial class CollectionView<TSource>
    {
        public class EmptyTemplate : Canvas { }

        protected virtual View CreateItemView(TSource viewModel)
        {
            var type = GetViewType(viewModel);
            if (!type.IsA<ITemplate>())
                throw new Exception(type.GetProgrammingName() + " does not implement ITemplate.");

            var result = type.CreateInstance<View>();

            GetModel(result).SetValue(viewModel);

            result.Ignored = true;

            return result;
            // .CssClass("list-item");
        }

        IBindable GetModel(View view)
        {
            var type = view.GetType();
            var modelProperty = type.GetPropertyOrField("Model")
              ?? throw new Exception(type.GetProgrammingName() + " does not have a field or property named Model");

            if (!modelProperty.GetPropertyOrFieldType().IsA<IBindable>())
                throw new Exception(type.GetProgrammingName() + ".Model is not IBindable.");

            return (IBindable)modelProperty.GetValue(view);
        }

        protected virtual View[] ViewItems() => AllChildren.Except(FindEmptyTemplate()).ToArray();

        public override async Task OnRendered()
        {
            await base.OnRendered();
            await (FindEmptyTemplate()?.IgnoredAsync(!source.None()) ?? Task.CompletedTask);

            await WhenShown(async () =>
            {
                await MeasureItems();
                await UIWorkBatch.Run(Arrange);
            });
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

            var visibleFrom = (Scroller.ScrollY - OverRenderBuffer()).LimitMin(0);
            var visibleTo = Scroller.ScrollY + Scroller.ActualHeight + OverRenderBuffer();

            var counter = -1;
            foreach (var vm in source)
            {
                counter++;
                var yPosition = ItemYPositions.GetOrDefault(counter);
                if (yPosition > visibleTo) break;
                if (yPosition < visibleFrom) continue;

                var item = mapping.FirstOrDefault(v => v.Item == vm);
                if (item == null)
                {
                    var wantedType = GetViewType(vm);
                    item = mapping.FirstOrDefault(x => !x.IsInUse && x.View.GetType() == wantedType);

                    if (item == null)
                        item = new ViewItem(CreateItemView(vm));

                    item.Load(vm);
                }

                item.IsInUse = true;
                item.View.Y.Set(yPosition);

                await item.View.IgnoredAsync(false);
                if (item.View.Parent == null) await Add(item.View);
            }

            foreach (var item in mapping.Except(x => x.IsInUse).ToArray())
                await item.View.IgnoredAsync();
        }

        protected virtual float OverRenderBuffer() => 50;

        protected virtual EmptyTemplate FindEmptyTemplate() => FindDescendent<EmptyTemplate>();
    }
}
// partial class CollectionView<TViewModel, TView>
// {
//    //public virtual TView[] ItemViews => this.AllChildren<TView>() /* for concurrency */ .ToArray();

// }