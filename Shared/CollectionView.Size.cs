using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Olive;

namespace Zebble
{
    partial class CollectionView<TSource>
    {
        ConcurrentDictionary<Type, float> TemplateWithOrHeightCache = new ConcurrentDictionary<Type, float>();
        ConcurrentDictionary<Type, Gap> TemplateMarginCache = new ConcurrentDictionary<Type, Gap>();
        Dictionary<int, Range<float>> ItemPositionOffsets;

        async Task<float> SizeOf(TSource item)
        {
            var existing = ViewItems().FirstOrDefault(x => x.GetViewModelValue() == item);
            if (existing != null) return SizeOf(existing);

            var viewType = GetViewType(item);

            if (TemplateWithOrHeightCache.TryGetValue(viewType, out var result)) return result;
            return TemplateWithOrHeightCache[viewType] = await GetSize(item);
        }
        async Task<Gap> MarginOf(TSource item)
        {
            var existing = ViewItems().FirstOrDefault(x => x.GetViewModelValue() == item);
            if (existing != null) return MarginOf(existing);

            var viewType = GetViewType(item);

            if (TemplateMarginCache.TryGetValue(viewType, out var result)) return result;
            return TemplateMarginCache[viewType] = await GetMargin(item);
        }

        /// <summary>
        /// This is only called once per view model type.
        /// </summary>
        protected virtual async Task<Gap> GetMargin(TSource exampleModel)
        {
            // Default implementation is to render an invisible item and see what its height is going to be.
            var temp = CreateItemView(exampleModel);
            await Add(temp);
            return MarginOf(temp);
        }
        Gap MarginOf(View item)
        {
            return item.Margin;
        }

        /// <summary>
        /// This is only called once per view model type.
        /// </summary>
        protected virtual async Task<float> GetSize(TSource exampleModel)
        {
            // Default implementation is to render an invisible item and see what its height is going to be.
            var temp = CreateItemView(exampleModel);
            await Add(temp);
            return SizeOf(temp);
        }

        float SizeOf(View view)
        {
            if (Horizontal)
                return view.ActualWidth + view.Margin.Left() + view.Margin.Right();
            else
                return view.ActualHeight + view.Margin.Top() + view.Margin.Bottom();
        }

        protected virtual async Task MeasureItems()
        {
            if (source.None())
            {
                if (Horizontal) Width.Set(FindEmptyTemplate()?.ActualWidth ?? 0);
                else Height.Set(FindEmptyTemplate()?.ActualHeight ?? 0);
                return;
            }

            var total = Horizontal ? Padding.Left() : (Padding.Top() + Margin.Top());
            var firstItem = source.FirstOrDefault();
            if (firstItem != null)
                total += (await MarginOf(firstItem)).Top();
            var counter = 0;
            ItemPositionOffsets = new Dictionary<int, Range<float>>(source.Count());

            foreach (var item in source)
            {
                var size = await SizeOf(item);
                ItemPositionOffsets[counter] = new Range<float>(total, total + size);
                total += size;
                counter++;
            }

            if (Horizontal)
            {
                total += Padding.Right();
                Width.Set(total);
            }
            else
            {
                total += Padding.Bottom();
                Height.Set(total);
            }
        }
    }
}
