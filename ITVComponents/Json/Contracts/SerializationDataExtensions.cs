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
            var retVal = list.FirstOrDefault(n => n.PropertyName == name)?.Data;
            if (retVal is T r)
            {
                return r;
            }

            LogEnvironment.LogEvent("Failed to get deserialized value.", LogSeverity.Error);
            return default;
        }
    }
}
