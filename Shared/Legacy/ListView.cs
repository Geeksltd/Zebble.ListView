namespace Zebble
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Olive;

    public abstract class ListView : Stack
    {
        public readonly AsyncEvent<EmptyTemplateChangedArg> EmptyTemplateChanged = new AsyncEvent<EmptyTemplateChangedArg>();

        protected View emptyTemplate => FindDescendent<EmptyTemplate>();

        public class EmptyTemplate : Canvas { }

        public class EmptyTemplateChangedArg
        {
            public EmptyTemplateChangedArg(View oldView, View newView)
            {
                OldView = oldView;
                NewView = newView;
            }

            public View OldView { get; set; }
            public View NewView { get; set; }
        };
    }

    public partial class ListView<TSource, TRowTemplate> : ListView
        where TRowTemplate : View, IListViewItem<TSource>, new()
    {
        ConcurrentList<TSource> dataSource = new ConcurrentList<TSource>();
        protected readonly object DataSourceSyncLock = new object();

        public ListView() : base() { EmptyTemplateChanged.Handle(OnEmptyTemplateChanged); }

        protected virtual TRowTemplate CreateItem(TSource data) => new TRowTemplate { Item = data }.CssClass("list-item");

        protected override string GetStringSpecifier() => typeof(TSource).Name;

        /// <summary>
        /// Adds a new Item to the List and also adds it to the DataSource
        /// </summary> 
        public Task<TRowTemplate> Add(TSource item)
        {
            dataSource.Insert(dataSource.Count, item);
            return Add(CreateItem(item));
        }

        /// <summary>
        /// Removes an Items from the list and its DataSource
        /// </summary>
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
            await (emptyTemplate?.IgnoredAsync(dataSource.Any()) ?? Task.CompletedTask);
        }

        protected virtual async Task OnEmptyTemplateChanged(EmptyTemplateChangedArg args)
        {
            if (!AllChildren.Contains(args.OldView)) return;

            await Remove(args.OldView);
            await args.NewView.IgnoredAsync(dataSource.Any());
            await Add(args.NewView);
        }

        public virtual TRowTemplate[] ItemViews => this.AllChildren<TRowTemplate>() /* for concurrency */ .ToArray();

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

        public virtual async Task UpdateSource(IEnumerable<TSource> source, bool reRenderItems = true)
        {
            lock (DataSourceSyncLock)
                dataSource = new ConcurrentList<TSource>(source ?? Enumerable.Empty<TSource>());

            if (!reRenderItems) return;

            foreach (var item in ItemViews.Reverse().ToArray())
                await Remove(item);

            await (emptyTemplate?.IgnoredAsync(dataSource.Any()) ?? Task.CompletedTask);

            await CreateInitialItems();
        }

        protected virtual async Task CreateInitialItems()
        {
            foreach (var item in dataSource.ToArray())
                await Add(CreateItem(item));
        }

        public Task Insert(int index, TSource item)
        {
            dataSource.Insert(index, item);
            return AddAt(index, CreateItem(item));
        }
    }

    public interface IListViewItem<TSource> { TSource Item { get; set; } }
}