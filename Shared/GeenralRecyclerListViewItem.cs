using System;

namespace Zebble
{
    public interface IGeneralRecyclerListViewItem : IListViewItem<object>
    {
        new Bindable<object> Item { get; }
    }

    public class GeneralRecyclerListViewItem : ListViewItem<object>, IGeneralRecyclerListViewItem
    {
        public new Bindable<object> Item { get; } = new Bindable<object>();

        object IListViewItem<object>.Item
        {
            get => Item.Value;
            set => Item.Set(value);
        }
    }
}