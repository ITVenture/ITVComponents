using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.TypeConversion.DefaultConverters
{
    public class NullableConverter : TypeConversionPlugin
    {
        public override bool CapableFor(object value, Type targetType)
        {
            bool retVal = targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>);
            if (!retVal && value != null)
            {
                var t = retVal.GetType();
                retVal = CapableFor(null, t);
            }

            return retVal;
        }

        public override bool TryConvert(object value, Type targetType, out object result)
        {
            var srcType = value?.GetType();
            if (srcType == targetType)
            {
                result = value;
                return true;
            }

            bool targetNullable = targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>);
            if (srcType == null && targetNullable)
            {
                result = null;
                return true;
            }
            else if (srcType == null && !targetNullable)
            {

                result = null;
                return false;
            }

            bool srcNullable = srcType.IsGenericType && srcType.GetGenericTypeDefinition() == typeof(Nullable<>);
            bool typesCompatible = (srcNullable ? srcType.GetGenericArguments()[0] : srcType) == (targetNullable ? targetType.GetGenericArguments()[0] : targetType);
            if (!typesCompatible)
            {
                result = null;
                return false;
            }

            if (srcNullable)
            {
                result = srcType.GetProperty("Value").GetValue(value);
                return true;
            }

            var ctor = targetType.GetConstructor(new[] { srcType });
            result = ctor.Invoke(new[] { value });
            return true;
        }
    }
}