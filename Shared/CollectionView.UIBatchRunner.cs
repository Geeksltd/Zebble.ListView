using System;
using System.Threading.Tasks;
using System.Linq;
using Olive;
using System.Diagnostics;

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
                    Debug.WriteLine(DateTime.Now.ToString("mm:ss") + " cancelled Arrange");
                    return;
                }
            }

            IsUIBatchRunning = true;

            Debug.WriteLine(DateTime.Now.ToString("mm:ss") + " " + originMethodName);

            await UIWorkBatch.Run(async () =>
            {
                await Arrange(layoutVersion);
            });

            IsUIBatchRunning = false;
        }
    }
}
