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
                //Item = vm;
                //View.SetViewModelValue(vm);
                //View.RefreshBindings();

                //see if we can notice a change
                View.Data.Add("IsBeingRecycled", true);
                View.ChangeInBatch(() =>
                {
                    Item = vm;
                    View.SetViewModelValue(vm);
                    View.RefreshBindings();
                });
                View.Data.Remove("IsBeingRecycled");
                Debug.WriteLine($"{LocalTime.Now}: Recycled");
            }
        }
    }
}