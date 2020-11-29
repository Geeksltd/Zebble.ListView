namespace Zebble
{
    public interface IRecyclerListViewItem<TSource> : IListViewItem<TSource>
    {
        new Bindable<TSource> Item { get; }
    }

    public class RecyclerListViewItem<TSource> : ListViewItem<TSource>, IRecyclerListViewItem<TSource>
    {
        public new Bindable<TSource> Item { get; } = new Bindable<TSource>();

        TSource IListViewItem<TSource>.Item
        {
            get => Item.Value;
            set => Item.Set(value);
        }
    }
}