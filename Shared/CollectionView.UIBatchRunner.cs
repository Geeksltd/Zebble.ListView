using System;
using System.Threading.Tasks;

namespace Zebble
{
    partial class CollectionView<TSource> : ICollectionView
    {
        bool IsUIBatchRunning;

        async Task BatchArrange(Guid layoutVersion, string originMethodName)
        {
            while (IsUIBatchRunning)
            {
                await Task.Delay(10);
                if (LayoutVersion != layoutVersion)
                {
                    return;
                }
            }

            IsUIBatchRunning = true;

            await UIWorkBatch.Run(async () =>
               {
                   await Arrange(layoutVersion);
               });

            IsUIBatchRunning = false;

        }
    }
}
