using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Formatters.Json
{
    internal class StringEnumConverterEx: JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsEnum;
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            var ret = typeof(JsonStringEnumConverterEx<>).MakeGenericType(typeToConvert).GetConstructor(Type.EmptyTypes)
                .Invoke(null);
            return (JsonConverter)ret;
        }
    }
}
