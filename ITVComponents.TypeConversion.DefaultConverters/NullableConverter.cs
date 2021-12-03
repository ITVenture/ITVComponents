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
                var t = value.GetType();
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
            var dstType = (targetNullable ? targetType.GetGenericArguments()[0] : targetType);
            bool typesCompatible = (srcNullable ? srcType.GetGenericArguments()[0] : srcType) == dstType;
            object srcValue = (srcNullable ? srcType.GetProperty("Value").GetValue(value) : value);
            if (!typesCompatible)
            {
                if (!TypeConverter.TryConvert(srcValue, dstType, out var tmpSrcValue))
                {
                    result = null;
                    return false;
                }

                srcValue = tmpSrcValue;
            }

            if (srcNullable)
            {
                result = srcValue;
                return true;
            }

            var ctor = targetType.GetConstructor(new[] { srcType }) ?? targetType.GetConstructor(new[] { dstType });
            result = ctor.Invoke(new[] { srcValue });
            return true;
        }
    }
}