using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ITVComponents.TypeConversion.DefaultConverters
{
    public class PrimitiveJsonElementConverter : TypeConversionProvider
    {
        public override bool CapableFor(object value, Type targetType)
        {
            return value is JsonElement;
        }

        public override bool TryConvert(object value, Type targetType, out object result)
        {
            bool retVal = false;
            if (value is JsonElement jee)
            {
                retVal = true;
                switch (jee.ValueKind)
                {
                    case JsonValueKind.String:
                        result = jee.GetString();
                        break;
                    case JsonValueKind.Number:
                    {
                        var tmp = jee.GetDecimal();
                        result = TypeConverter.Convert(tmp, targetType); 
                        break;
                        }
                    case JsonValueKind.True:
                        result = true;
                        break;
                    case JsonValueKind.False:
                        result = false;
                        break;
                    case JsonValueKind.Null:
                        result = null;
                        break;
                    default:
                        result = null;
                        retVal = false;
                        break;
                }
            }
            else
            {
                result = null;
            }

            return retVal;
        }
    }
}
