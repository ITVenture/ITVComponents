using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Logging;

namespace ITVComponents.Json.Contracts
{
    public static class SerializationDataExtensions
    {
        public static T GetDeserializedValue<T>(this IList<ManualSerializationData> list, string name)
        {
            var ser = list.FirstOrDefault(n => n.PropertyName == name);
            var retVal = ser?.Data;
            if (retVal is T r)
            {
                return r;
            }

            LogEnvironment.LogEvent($"Failed to get deserialized value. Expected type: {typeof(T).FullName}, effective type: {retVal?.GetType().FullName??"null"}. Type of ManualSerializationData-Object: {ser?.TypeName??"null"}", LogSeverity.Error);
            return default;
        }
    }
}
