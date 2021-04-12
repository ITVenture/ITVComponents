using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
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
                string typeName = (string)job["$type"];
                if (typeName != null)
                {
                    Type type = Type.GetType(typeName);
                    if (type != null)
                    {
                        value = job.ToObject(type);
                    }
                }
            }
            else if (value is JValue jav)
            {
                value = jav.Value;
            }

            return value;
        }
    }
}
