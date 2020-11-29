using System;
using Zebble.Mvvm;

namespace Zebble
{
    public partial class CollectionView<TSource, TView> : CollectionView<TSource>
        where TSource : class
        where TView : ITemplate<TSource>, new()
    {
        public override Type GetViewType(TSource viewModel) => typeof(TView);
    }
}
