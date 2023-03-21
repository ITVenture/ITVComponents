using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Logging;
using ITVComponents.TypeConversion;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ITVComponents.EFRepo.Expressions.Models
{

    public class ValueHold
    {
        public static object ValueHoldFor(Type t, object value, out Type finalType)
        {
            finalType = typeof(ValueHold<>).MakeGenericType(t);
            var retVal = finalType.GetConstructor(Type.EmptyTypes).Invoke(null);
            if (TypeConverter.TryConvert(value, t, out var tval))
            {
                finalType.GetProperty("Value").SetValue(retVal, tval);
            }
            else
            {
                LogEnvironment.LogEvent($"Unable to convert the value {value} to type {t.FullName}", LogSeverity.Error);
            }

            return retVal;
        }
    }

    public class ValueHold<T>
    {
        public T Value { get; set; }
    }
}