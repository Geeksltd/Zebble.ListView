using System;
using System.Linq;
using System.Collections.Generic;
using Zebble.Mvvm;
using Olive;

namespace Zebble
{
    public interface ISelectionView { }

    public class SelectionView<TSource, TView> : CollectionView<TSource, TView>, ISelectionView
        where TSource : class
        where TView : ITemplate<TSource>, new()
    {
        protected IEnumerable<TSource> selected;

        public IEnumerable<TSource> Selected
        {
            get => selected;
            set
            {
                if (selected == value) return;
                value ??= new TSource[0];

                if (selected is ISelectionViewModel<TSource> oldVm)
                    oldVm.SelectionChanged -= OnSelectionChanged;

                if (selected != null)
                {
                    selected = value;
                    OnSelectionChanged();
                }
                else
                {
                    selected = value;
                    if (value is ISelectionViewModel<TSource> newVm)
                        newVm.SelectionChanged += OnSelectionChanged;
                }
            }
        }

        protected virtual void OnSelectionChanged() { }

        protected override View CreateItemView(TSource viewModel)
        {
            //var type = GetViewType(viewModel);
            //if (!type.IsA<ISelectableTemplate<TSource>>())
            //    throw new Exception(type.GetProgrammingName() + " does not implement ISelectableTemplate.");

            var result = base.CreateItemView(viewModel);

            //((ISelectableTemplate<TSource>)result).ToggleSelection += OnToggleSelection;

            // TODO: how to unbind when View is gone?

            return result;
        }

        void OnToggleSelection(TSource item)
        {
            if (Selected.Contains(item)) Selected = Selected.Except(item);
            else Selected = Selected.Concat(item);
        }
    }
}
