namespace Zebble
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public abstract class ListView : Stack
    {
        public readonly AsyncEvent<EmptyTemplateChangedArg> EmptyTemplateChanged = new AsyncEvent<EmptyTemplateChangedArg>();

        public string EmptyText
        {
            get
            {
                var emptyTextView = emptyTemplate as TextView;
                if (emptyTextView == null) return string.Empty;
                else return emptyTextView.Text;
            }
            set
            {
                var emptyTextView = emptyTemplate as TextView;
                if (emptyTextView == null) return;
                else emptyTextView.Text = value;
            }
        }

        protected View emptyTemplate = new TextView { Id = "EmptyTextLabel" };
        public View EmptyTemplate
        {
            get => emptyTemplate;
            set
            {
                EmptyTemplateChanged.Raise(new EmptyTemplateChangedArg(emptyTemplate, value));
                emptyTemplate = value;
            }
        }

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
        object DataSourceSyncLock = new object();

        public ListView() : base() { Shown.Handle(OnShown); EmptyTemplateChanged.Handle(OnEmptyTemplateChanged); }

        TRowTemplate CreateItem(TSource data) => new TRowTemplate { Item = data }.CssClass("list-item");

        protected override string GetStringSpecifier() => typeof(TSource).Name;

        /// <summary>
        /// Adds a new Item to the List and also adds it to the DataSource
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public Task<TRowTemplate> Add(TSource item)
        {
            dataSource.Insert(dataSource.Count, item);
            return AddItem(item);
        }

        Task<TRowTemplate> AddItem(TSource item) => Add(CreateItem(item));

        /// <summary>
        /// Removes an Items from the list and its DataSource
        /// </summary>
        /// <param name="item"></param>
        /// <param name="awaitNative"></param>
        /// <returns></returns>
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
            await Add(emptyTemplate.Ignored(dataSource.Any()));
        }

        async Task OnEmptyTemplateChanged(EmptyTemplateChangedArg args)
        {
            if (!AllChildren.Contains(args.OldView)) return;

            await Remove(args.OldView);
            await Add(args.NewView.Ignored(dataSource.Any()));

            await UpdateEmptyViewHeight();
        }

        async Task UpdateEmptyViewHeight()
        {
            if (dataSource.Any()) return;

            if (LazyLoad)
            {
                this.Height(Height.CurrentValue + emptyTemplate.ActualHeight);
                await (this as IAutoContentHeightProvider).Changed.Raise();
            }
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

        public async Task UpdateSource(IEnumerable<TSource> source, bool reRenderItems = true)
        {
            lock (DataSourceSyncLock) dataSource = new ConcurrentList<TSource>(source ?? Enumerable.Empty<TSource>());

            if (!reRenderItems) return;

            foreach (var item in AllChildren.Except(emptyTemplate).Reverse().ToArray())
                await Remove(item);

            LazyRenderedItemsTotalHeight = 0;
            VisibleItems = 0;

            emptyTemplate.Ignored(dataSource.Any());

            if (LazyLoad)
            {
                if (IsShown) await LazyLoadInitialItems();
            }
            else foreach (var item in dataSource.ToArray()) await Add(CreateItem(item));

            emptyTemplate.Ignored(dataSource.Any());

            await UpdateEmptyViewHeight();
        }

        public Task Insert(int index, TSource item)
        {
            dataSource.Insert(index, item);
            return AddAt(index, CreateItem(item));
        }
    }

    public interface IListViewItem<TSource> { TSource Item { get; set; } }
}