using System;
using System.Collections.Generic;
using System.Linq;
using Zebble.Mvvm;

namespace Zebble
{
    public abstract partial class CollectionView<TSource> : Canvas
        where TSource : class
    {
        protected IEnumerable<TSource> source;
        RepeatDirection direction = RepeatDirection.Vertical;

        public CollectionView() => ClipChildren = false;

        public RepeatDirection Direction
        {
            get => direction;
            set
            {
                if (value == direction) return;
                if (IsRendered()) throw new InvalidOperationException("Direction can only be set once and before rendering.");
                direction = value;
                if (Horizontal)
                    this.Width(new Length.AutoLengthRequest(Length.AutoStrategy.Content));
            }
        }

        bool Horizontal => direction == RepeatDirection.Horizontal;

        public IEnumerable<TSource> Source
        {
            get => source;
            set
            {
                if (source == value) return;
                value = value ?? new TSource[0];

                if (source is CollectionViewModel oldVm)
                    oldVm.Changed -= OnSourceChanged;

                if (source != null)
                {
                    source = value;
                    OnSourceChanged();
                }
                else
                {
                    source = value;
                    if (value is CollectionViewModel newVm)
                        newVm.Changed += OnSourceChanged;
                }
            }
        }

        protected virtual void OnSourceChanged()
        {
            if (!IsShown) return;
            MeasureItems().ContinueWith(x => UIWorkBatch.Run(Arrange));
        }

        /// <summary>
        /// Gets the type of the view to render or recycle for the specified view model item.
        /// The returned type must implement ITemplate.
        /// </summary> 
        public abstract Type GetViewType(TSource viewModel);
    }
}
