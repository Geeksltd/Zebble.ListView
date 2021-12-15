using System;
using System.Threading.Tasks;

namespace Zebble
{
    partial class CollectionView<TSource> : ICollectionView
    {
        async Task BatchArrange(Guid layoutVersion)
        {
            await UIWorkBatch.Run(async () =>
               {
                   await Arrange(layoutVersion);
               });
        }
    }
}
