using System;
using System.Threading.Tasks;
using System.Linq;
using Olive;
using System.Diagnostics;
using System.Threading;

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
