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
        ConcurrentDictionary<Type, View> MeasurementViews = new();

        async Task CreateMeasurementViews()
        {
            foreach (var template in Source.GroupBy(GetViewType))
            {
                if (MeasurementViews.ContainsKey(template.Key)) continue;

                var type = template.Key;

                if (!type.IsA<ITemplate>())
                    throw new Exception(type.GetProgrammingName() + " does not implement ITemplate.");

                var view = type.CreateInstance<View>();
                view.SetViewModelValue(template.First());
                view.Ignored = true;
                view.Id = "MeasurementView";
                await Add(view);
                MeasurementViews[type] = view;
            }
        }

        protected virtual async Task MeasureOffsets(Guid layoutVersion)
        {
            await CreateMeasurementViews();

            var numProcs = Environment.ProcessorCount;
            var concurrencyLevel = numProcs * 2;
            var newOffsets = new ConcurrentDictionary<int, Range<float>>(concurrencyLevel, OnSource(x => x.Count()));

            var counter = 0;

            var from = Horizontal ? Padding.Left() : Padding.Top();

            foreach (var item in OnSource(x => x.ToArray()).OrEmpty())
            {
                var measure = Measure(item);
                if (layoutVersion != LayoutVersion) return;

                if (counter == 0) from += measure.Margin;

                newOffsets[counter] = new Range<float>(from, from + measure.Size);
                from += measure.Size;
                counter++;
            }
            ItemPositionOffsets = newOffsets;
        }

        Measurement Measure(TSource item)
        {
            var actual = ViewItems().FirstOrDefault(x => x.GetViewModelValue() == item);
            if (actual != null)
            {
                return new Measurement(Direction, actual);
            }
            else
            {
                var view = MeasurementViews[GetViewType(item)];

                if (ShouldMeasureForAll)
                {
                    view.SetViewModelValue(item);
                    view.RefreshBindings();
                }

                return new Measurement(Direction, view);
            }
        }

        void ResizeToEmptyTemplate()
        {
            if (Horizontal) Width.Set(FindEmptyTemplate()?.ActualWidth ?? 0);
            else Height.Set(FindEmptyTemplate()?.ActualHeight ?? 0);
        }
    }
}