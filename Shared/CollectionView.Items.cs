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

        public IEnumerable<TSource> Source
        {
            get => source;
            set
            {
                if (source == value) return;
                value = value ?? new TSource[0];

                (source as CollectionViewModel).Changed -= OnSourceChanged;

                if (source != null)
                {
                    source = value;
                    OnSourceChanged();
                }
                else
                {
                    source = value;
                    if (source is CollectionViewModel cl) cl.Changed += OnSourceChanged;
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
