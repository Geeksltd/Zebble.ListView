using System;
using System.Reflection;

namespace Zebble
{
    partial class CollectionView<TSource>
    {
        class ViewItem
        {
            public View View;
            public IBindable Model;
            public TSource Item;
            public bool IsInUse;

            public ViewItem(View view)
            {
                View = view;
                Model = FindModel();
                Item = (TSource)Model.Value;
            }

            IBindable FindModel()
            {
                var type = View.GetType();
                var modelProperty = type.GetPropertyOrField("Model")
                  ?? throw new Exception(type.GetProgrammingName() + " does not have a field or property named Model");

                if (!modelProperty.GetPropertyOrFieldType().IsA<IBindable>())
                    throw new Exception(type.GetProgrammingName() + ".Model is not IBindable.");

                return (IBindable)modelProperty.GetValue(View);
            }

            internal void Load(TSource vm)
            {
                Item = vm;
                Model.Value = vm;

                View.RefreshBindings();
            }
        }
    }
}