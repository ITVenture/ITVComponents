using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.EFRepo.Expressions.Models
{

    public class ValueHold
    {
        public static object ValueHoldFor(Type t, object value, out Type finalType)
        {
            finalType = typeof(ValueHold<>).MakeGenericType(t);
            var retVal = finalType.GetConstructor(Type.EmptyTypes).Invoke(null);
            finalType.GetProperty("Value").SetValue(retVal, value);
            return retVal;
        }
    }

    public class ValueHold<T>
    {
        public T Value { get; set; }
    }
}