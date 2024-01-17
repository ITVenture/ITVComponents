using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.EFRepo.Expressions.Visitors
{
    public class PropertyInitializer<T>:IPropertyInitializer
    {
        public T Value => default(T);

        public PropertyInfo TargetProperty { get; }

        public string ArgumentName { get; }

        public PropertyInitializer(PropertyInfo info, string argumentSource)
        {
            TargetProperty = info;
            ArgumentName = argumentSource;
        }
    }

    public interface IPropertyInitializer
    {
        PropertyInfo TargetProperty { get; }

        string ArgumentName { get; }
    }
}
