using System;
using System.Collections.Generic;
using System.Linq;
using Olive;

namespace Zebble
{
    public abstract partial class CollectionView<TSource> : Canvas
        where TSource : class
    {
        protected IEnumerable<TSource> source;
        RepeatDirection direction = RepeatDirection.Vertical;

        public readonly AsyncEvent UpdateLayoutCompleted = new AsyncEvent();
        public readonly AsyncEvent LayoutChanged = new AsyncEvent();

        TResult OnSource<TResult>(Func<IEnumerable<TSource>, TResult> query)
        {
            if (source is null) return default;
            lock (source)
                return query(source);
        }

        protected CollectionView() => ClipChildren = false;

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
                value ??= new TSource[0];

                if (source is IBindableCollection oldVm)
                    oldVm.Changed -= OnSourceChanged;
                
                if (value is IBindableCollection newVm)
                {
                    source = value;
                    newVm.Changed += OnSourceChanged;
                }
                else if (value is TSource[])
                    source = value as TSource[];
                else source = value.ToArray();

                OnSourceChanged();
            }
        }

        protected virtual void OnSourceChanged() => ReLayoutIfShown("SourceChanged").GetAwaiter().GetResult();

        /// <summary>
        /// Gets the type of the view to render or recycle for the specified view model item.
        /// The returned type must implement ITemplate.
        /// </summary> 
        public abstract Type GetViewType(TSource viewModel);
    }
}
