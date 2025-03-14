using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace ITVComponents.Json.Contracts
{
    public class ManualSerializationData
    {
        private bool processed = false;

        public string PropertyName { get; set; }

        public string TypeName { get; set; }

        public object Data { get; set; }

        public static ManualSerializationData FromValue(string propertyName, object value)
        {
            return new ManualSerializationData
            {
                PropertyName = propertyName,
                TypeName = value?.GetType().AssemblyQualifiedName,
                Data = value
            };
        }

        public void ReadValues(JsonSerializerOptions serializationOptions)
        {
            if (!processed)
            {
                Type t = null;
                if (!string.IsNullOrEmpty(TypeName))
                {
                    t = Type.GetType(TypeName);
                }

                if (t != null)
                {
                    var tarr = t;
                    if (!t.IsArray)
                    {
                        tarr = t.MakeArrayType();
                    }

                    if (Data is JsonNode je)
                    {
                        switch (je.GetValueKind())
                        {
                            case JsonValueKind.Undefined:
                                Data = null;
                                break;
                            case JsonValueKind.Object:
                                Data = je.AsObject().Deserialize(t, serializationOptions);
                                break;
                            case JsonValueKind.Array:
                                Data = je.AsArray().Deserialize(tarr, serializationOptions);
                                break;
                            case JsonValueKind.String:
                            case JsonValueKind.Number:
                                Data = je.Deserialize(t, serializationOptions);
                                break;
                            case JsonValueKind.True:
                                Data = true;
                                break;
                            case JsonValueKind.False:
                                Data = false;
                                break;
                            case JsonValueKind.Null:
                                Data = null;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }

                    processed = true;
                }
                else
                {
                    Data = null;
                }
            }
        }
    }
}
