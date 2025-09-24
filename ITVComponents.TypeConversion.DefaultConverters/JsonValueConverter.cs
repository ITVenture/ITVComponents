using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace ITVComponents.TypeConversion.DefaultConverters
{
    public class JsonValueConverter:TypeConversionPlugin
    {
        public override bool CapableFor(object value, Type targetType)
        {
            if (value is JsonElement)
            {
                return true;
            }

            return false;
        }

        public override bool TryConvert(object value, Type targetType, out object result)
        {
            result = null;
            try
            {
                var tmp = (JsonElement)value;
                var realSource = ConvertSource(tmp);
                result = TypeConverter.TryConvert(realSource, targetType);
                return true;
            }
            catch
            {
                return false;
            }

        }

        private object ConvertSource(JsonElement src)
        {
            switch (src.ValueKind)
            {
                case JsonValueKind.Undefined:
                    return null;
                    
                case JsonValueKind.Object:
                    return src.GetRawText();
                    
                case JsonValueKind.Array:
                    return JsonArray.Create(src).Select(n => ConvertSource(n)).ToArray();
                    
                case JsonValueKind.String:
                    return src.GetString();
                    
                case JsonValueKind.Number:
                    return src.GetDecimal();
                    
                case JsonValueKind.True:
                    return true;
                    
                case JsonValueKind.False:
                    return false;
                    
                case JsonValueKind.Null:
                    return null;
                    
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private object ConvertSource(JsonNode src)
        {
            switch (src.GetValueKind())
            {
                case JsonValueKind.Undefined:
                    return null;

                case JsonValueKind.Object:
                    return src.ToJsonString();

                case JsonValueKind.Array:
                    return src.AsArray().Select(ConvertSource).ToArray();

                case JsonValueKind.String:
                    return src.GetValue<string>();

                case JsonValueKind.Number:
                    return src.GetValue<decimal>();

                case JsonValueKind.True:
                    return true;

                case JsonValueKind.False:
                    return false;

                case JsonValueKind.Null:
                    return null;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
