using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Zebble
{
    partial class CollectionView<TSource>
    {
        ConcurrentDictionary<Type, float> TemplateWithOrHeightCache = new ConcurrentDictionary<Type, float>();
        Dictionary<int, float> ItemPositionOffsets;

        async Task<float> SizeOf(TSource item)
        {
            var viewType = GetViewType(item);

            if (TemplateWithOrHeightCache.TryGetValue(viewType, out var result)) return result;
            return TemplateWithOrHeightCache[viewType] = await GetSize(item);
        }

        /// <summary>
        /// This is only called once per view model type.
        /// </summary>
        protected virtual async Task<float> GetSize(TSource exampleModel)
        {
            // Default implementation is to render an invisible item and see what its height is going to be.
            var temp = CreateItemView(exampleModel);            
            await Add(temp);

            if (Horizontal)
                return temp.ActualWidth + temp.Margin.Left() + temp.Margin.Right();
            else
                return temp.ActualHeight + temp.Margin.Top() + temp.Margin.Bottom();
        }

        protected virtual async Task MeasureItems()
        {
            if (source.None())
            {
                if (Horizontal) Width.Set(FindEmptyTemplate()?.ActualWidth ?? 0);
                else Height.Set(FindEmptyTemplate()?.ActualHeight ?? 0);
                return;
            }

            var total = Horizontal ? Padding.Left() : Padding.Top();
            var counter = 0;
            ItemPositionOffsets = new Dictionary<int, float>(source.Count());

            foreach (var item in source)
            {
                ItemPositionOffsets[counter] = total;

                total += await SizeOf(item);
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
