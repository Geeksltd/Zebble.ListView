namespace Zebble
{
    partial class CollectionView<TSource>
    {
        struct Measurement
        {
            public float Margin, Size;
            public View ActualView;

            public Measurement(RepeatDirection direction, View view)
            {
                ActualView = view;

                if (direction == RepeatDirection.Horizontal)
                {
                    Margin = view.Margin.Left();
                    Size = Margin + view.ActualWidth + view.Margin.Left();
                }
                else
                {
                    Margin = view.Margin.Top();
                    Size = Margin + view.ActualHeight + view.Margin.Bottom();
                }
            }
        }
    }
}