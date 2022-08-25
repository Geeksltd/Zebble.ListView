using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Olive;

namespace Zebble
{
    partial class CollectionView<TSource>
    {
        public bool ShouldMeasureForAll { get; set; }
        ConcurrentDictionary<int, Range<float>> ItemPositionOffsets;
        View MeasurementView;

        protected virtual async Task MeasureOffsets(Guid layoutVersion)
        {
            if (MeasurementView == null)
            {
                var firstItem = Source.First();
                var type = GetViewType(firstItem);

                if (!type.IsA<ITemplate>())
                    throw new Exception(type.GetProgrammingName() + " does not implement ITemplate.");

                MeasurementView = type.CreateInstance<View>();
                MeasurementView.SetViewModelValue(firstItem);
                MeasurementView.Ignored = true;
                MeasurementView.Id = "MeasurementView";
                await Add(MeasurementView);
            }

            var numProcs = Environment.ProcessorCount;
            var concurrencyLevel = numProcs * 2;
            var newOffsets = new ConcurrentDictionary<int, Range<float>>(concurrencyLevel, OnSource(x => x.Count()));

            var counter = 0;

            var from = Horizontal ? Padding.Left() : Padding.Top();

            foreach (var item in OnSource(x => x.ToArray()))
            {
                var measure = await Measure(item);
                if (layoutVersion != LayoutVersion) return;

                if (counter == 0) from += measure.Margin;

                newOffsets[counter] = new Range<float>(from, from + measure.Size);
                from += measure.Size;
                counter++;
            }
            ItemPositionOffsets = newOffsets;
        }

        async Task<Measurement> Measure(TSource item)
        {
            var actual = ViewItems().FirstOrDefault(x => x.GetViewModelValue() == item);
            if (actual != null)
            {
                return new Measurement(Direction, actual);
            }
            else
            {
                if (ShouldMeasureForAll)
                {
                    MeasurementView.SetViewModelValue(item);
                    MeasurementView.RefreshBindings();
                }
                return new Measurement(Direction, MeasurementView);
            }
        }

        void ResizeToEmptyTemplate()
        {
            if (Horizontal) Width.Set(FindEmptyTemplate()?.ActualWidth ?? 0);
            else Height.Set(FindEmptyTemplate()?.ActualHeight ?? 0);
        }
    }
}