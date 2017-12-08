namespace Zebble
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public abstract class ListView : Stack
    {
        public readonly TextView EmptyTextLabel = new TextView { Id = "EmptyTextLabel" };

        public string EmptyText
        {
            get => EmptyTextLabel.Text;
            set => EmptyTextLabel.Text = value;
        }
    }

    public partial class ListView<TSource, TRowTemplate> : ListView
        where TRowTemplate : View, IListViewItem<TSource>, new()
    {
        ConcurrentList<TSource> dataSource = new ConcurrentList<TSource>();
        object DataSourceSyncLock = new object();

        public ListView() : base() { Shown.Handle(OnShown); }

        TRowTemplate CreateItem(TSource data) => new TRowTemplate { Item = data }.CssClass("list-item");

        protected override string GetStringSpecifier() => typeof(TSource).Name;

        public Task<TRowTemplate> AddItem(TSource item)
        {
            dataSource.Insert(dataSource.Count, item);
            return Add(CreateItem(item));
        }

        public Task Remove(TSource item, bool awaitNative = true)
        {
            lock (DataSourceSyncLock)
            {
                var index = dataSource.IndexOf(item);
                if (index == -1)
                {
                    Device.Log.Error("Invalid ListView.Remove() attempted for item '" + item + "': Item does not exist in the data source.");
                    return Task.CompletedTask;
                }

                dataSource.RemoveAt(index);
                return RemoveAt(index, awaitNative);
            }
        }

        public override async Task OnInitializing()
        {
            await base.OnInitializing();
            await Add(EmptyTextLabel.Ignored(dataSource.Any()));
        }

        public IEnumerable<TRowTemplate> ItemViews => this.AllChildren<TRowTemplate>() /* for concurrency */ .ToArray();

        public IEnumerable<TSource> DataSource
        {
            get
            {
                lock (DataSourceSyncLock) return dataSource?.ToArray() ?? Enumerable.Empty<TSource>();
            }
            set
            {
                if (parent == null)
                    Initialized.Handle(() => UpdateSource(value));
                else
                {
                    Device.Log.Warning("To change a list view's data source at runtime, invoke UpdateSource() instead of setting DataSource property.");
                    UpdateSource(value).RunInParallel();
                }
            }
        }

        public async Task UpdateSource(IEnumerable<TSource> source)
        {
            lock (DataSourceSyncLock) dataSource = new ConcurrentList<TSource>(source ?? Enumerable.Empty<TSource>());

            foreach (var item in AllChildren.Except(EmptyTextLabel).Reverse().ToArray())
                await Remove(item);

            LazyRenderedItemsTotalHeight = 0;
            VisibleItems = 0;

            EmptyTextLabel.Style.Ignored = dataSource.Any();

            if (LazyLoad)
            {
                if (IsShown) await LazyLoadInitialItems();
            }
            else foreach (var item in dataSource.ToArray()) await Add(CreateItem(item));

            EmptyTextLabel.Ignored(dataSource.Any());
        }

        public Task Insert(int index, TSource item)
        {
            dataSource.Insert(index, item);
            return AddAt(index, CreateItem(item));
        }
    }

    public interface IListViewItem<TSource> { TSource Item { get; set; } }
}