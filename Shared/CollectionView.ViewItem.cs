using Olive;
using System.Diagnostics;

namespace Zebble
{
    partial class CollectionView<TSource>
    {
        class ViewItem
        {
            public View View;
            public TSource Item;
            public bool IsInUse;

            public ViewItem(View view)
            {
                View = view;
                Item = (TSource)view.GetViewModelValue();
            }

            internal void Load(TSource vm)
            {
                View.ChangeInBatch(() =>
                {
                    Item = vm;
                    View.SetViewModelValue(vm);
                    View.RefreshBindings();
                });
            }
        }
    }
}