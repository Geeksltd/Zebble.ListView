using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Zebble
{
    partial class CollectionView<TSource>
    {
        ConcurrentDictionary<Type, float> TemplateHeightCache = new ConcurrentDictionary<Type, float>();
        Dictionary<int, float> ItemYPositions;

        async Task<float> HeightOf(TSource item)
        {
            var viewType = GetViewType(item);

            if (TemplateHeightCache.TryGetValue(viewType, out var result)) return result;
            return TemplateHeightCache[viewType] = await GetHeight(item);
        }

        /// <summary>
        /// This is only called once per view model type.
        /// </summary>
        protected virtual async Task<float> GetHeight(TSource exampleModel)
        {
            // Default implementation is to render an invisible item and see what its height is going to be.
            var temp = CreateItemView(exampleModel);
            await Add(temp);
            return temp.ActualHeight + temp.Margin.Top() + temp.Margin.Bottom();
        }

        protected virtual async Task MeasureItems()
        {
            if (source.None())
            {
                Height.Set(FindEmptyTemplate()?.ActualHeight ?? 0);
                return;
            }

            var total = Padding.Top();
            var counter = 0;
            ItemYPositions = new Dictionary<int, float>(source.Count());

            foreach (var item in source)
            {
                ItemYPositions[counter] = total;

                total += await HeightOf(item);
                counter++;
            }

            total += Padding.Bottom();
            Height.Set(total);
        }
    }
}
