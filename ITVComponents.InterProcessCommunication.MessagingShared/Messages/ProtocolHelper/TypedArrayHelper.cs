using ITVComponents.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Json.Contracts;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Messages.ProtocolHelper
{
    internal static class TypedArrayHelper
    {
        public static string PackArguments(this object[] arguments)
        {
            return JsonHelper.ToJson(new TypedArray { RawData = arguments },
                SerializationTypingMode.AssistedPolymorphism, null);
        }

        public static object[] UnpackArguments(this string arguments)
        {
            return JsonHelper.FromJsonString<IManualSerializer>(arguments, SerializationTypingMode.AssistedPolymorphism)
                .Cast<TypedArray>().RawData;
        }
    }
}
