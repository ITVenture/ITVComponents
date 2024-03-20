using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.EFRepo.Expressions.Visitors
{
    public class PropertyInitializer<T>:IPropertyInitializer
    {
        public T Value => default(T);

        public PropertyInfo? TargetProperty { get; }

        public string ArgumentName { get; }

        public Expression ValueExpression { get; }

        public PropertyInitializer(PropertyInfo? info, string argumentSource, Expression valueExpression)
        {
            TargetProperty = info;
            ArgumentName = argumentSource;
            ValueExpression = valueExpression;
        }
    }

    public interface IPropertyInitializer
    {
        PropertyInfo TargetProperty { get; }

        string ArgumentName { get; }

        Expression ValueExpression { get; }
    }
}
