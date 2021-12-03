using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Logging;
using Newtonsoft.Json.Linq;

namespace ITVComponents.Helpers
{
    public static class SerializationInfoExtensions
    {
        public static object GetObject(this SerializationInfo info, string name)
        {
            object value = info.GetValue(name, typeof(object));
            if (value is JObject job)
            {
                value = GetJObjectValue(job);
            }
            else if (value is JValue jav)
            {
                value = jav.Value;
            }

            return value;
        }

        private static object GetJArrayValue(JArray jay, Type type)
        {
            bool makeArray = false;
            if (type.IsArray)
            {
                type = type.GetElementType();
                makeArray = true;
            }
            else if (type.IsGenericType)
            {
                type = type.GetGenericArguments()[0];
            }

            var lit = typeof(List<>).MakeGenericType(type);
            IList li = (IList)lit.GetConstructor(Type.EmptyTypes).Invoke(null);
            foreach (var item in jay)
            {
                if (item is JObject job)
                {
                    li.Add(GetJObjectValue(job));
                }
                else if (item is JValue jaw)
                {
                    li.Add(jaw.Value);
                }
                else
                {
                    LogEnvironment.LogDebugEvent($"Items is {item.GetType().FullName}", LogSeverity.Warning);
                }
            }

            if (makeArray)
            {
                var arr = Array.CreateInstance(type, li.Count);
                li.CopyTo(arr, 0);
                return arr;
            }

            return li;
        }

        private static object GetJObjectValue(JObject job)
        {
            object value = job;
            string typeName = (string)job["$type"];
            var values = job["$values"];
            if (typeName != null)
            {
                Type type = Type.GetType(typeName);
                if (type != null && (values == null || values is not JArray))
                {
                    value = job.ToObject(type);
                }
                else if (values is JArray jay)
                {
                    value = GetJArrayValue(jay, type);
                }
            }

            return value;
        }
    }
}
