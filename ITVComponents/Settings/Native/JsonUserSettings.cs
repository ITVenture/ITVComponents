using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ITVComponents.Helpers;
using ITVComponents.Json;


namespace ITVComponents.Settings.Native
{
    public class JsonUserSettings
    {
        private string fileName;

        private Dictionary<string, JsonNode> rootSettings;

        private Dictionary<string, object> bufferedObjects = new Dictionary<string, object>();

        private JsonUserSettings parent;

        public static JsonUserSettings LoadFrom(string fileName)
        {
            if (File.Exists(fileName))
            {
                return new JsonUserSettings
                {
                    fileName = fileName,
                    rootSettings = JsonHelper.ReadObject<Dictionary<string, JsonNode>>(fileName, SerializationTypingMode.StaticTyping)
                };
            }

            var fp = Path.GetDirectoryName(Path.GetFullPath(fileName));
            if (!Directory.Exists(fp))
            {
                Directory.CreateDirectory(fp);
            }

            return new JsonUserSettings
            {
                fileName = fileName,
                rootSettings = new Dictionary<string, JsonNode>()
            };
        }

        /// <summary>
        /// Prevents a default instance of the JsonUserSettings class from being created
        /// </summary>
        private JsonUserSettings()
        {
        }

        public JsonUserSettings GetSubSection(string name)
        {
            JsonUserSettings retVal;
            if (!bufferedObjects.ContainsKey(name) && rootSettings.TryGetValue(name, out var setting))
            {
                var tmp = setting.GetValue<Dictionary<string,JsonNode>>();
                retVal = new JsonUserSettings
                {
                    parent = this,
                    rootSettings = tmp
                };
                bufferedObjects.Add(name, retVal);
            }
            else if (bufferedObjects.TryGetValue(name, out var o))
            {
                retVal = (JsonUserSettings)o;
            }
            else
            {
                retVal = new JsonUserSettings
                {
                    parent = this,
                    rootSettings = new()
                };

                bufferedObjects.Add(name, retVal);
            }

            return retVal;
        }

        public T GetSubSection<T>(string name) where T : new()
        {
            T retVal;
            if (!bufferedObjects.ContainsKey(name) && rootSettings.TryGetValue(name, out var setting))
            {
                retVal = setting.GetValue<T>();
                bufferedObjects.Add(name, retVal);
            }
            else if (bufferedObjects.TryGetValue(name, out var o))
            {
                retVal = (T)o;
            }
            else
            {
                retVal = new();
                bufferedObjects.Add(name, retVal);
            }

            return retVal;
        }

        public void Save()
        {
            if (parent != null)
            {
                parent.Save();
            }
            else if (!string.IsNullOrEmpty(fileName))
            {
                Persist();
                JsonHelper.WriteObject(rootSettings, SerializationTypingMode.StaticTyping, fileName);
            }
            else
            {
                throw new InvalidOperationException("Invalid object state!");
            }
        }

        private void Persist()
        {
            foreach (var tmp in bufferedObjects)
            {
                if (tmp.Value is JsonUserSettings jse)
                {
                    jse.Persist();
                    var jt = new JsonObject(jse.rootSettings);//JsonObject.Create(jse.rootSettings);
                    rootSettings[tmp.Key] =jt;
                    CleanupObject(jt);
                }
                else if (tmp.Value != null)
                {
                    ;
                    var jt = JsonSerializer.SerializeToNode(tmp.Value); //jsonnodeJsonNode.FromObject(tmp.Value);
                    
                    if (jt.GetValueKind() == JsonValueKind.Object)
                    {
                        var jo = jt.AsObject();
                        CleanupObject(jo);
                        rootSettings[tmp.Key] = jo;
                    }
                    else
                    {
                        rootSettings[tmp.Key] = jt;
                    }
                }
                else
                {
                    if (rootSettings.ContainsKey(tmp.Key))
                    {
                        rootSettings.Remove(tmp.Key);
                    }
                }
            }
        }

        private void CleanupObject(JsonObject jo)
        {
            foreach (var prop in jo.ToArray()) 
            {
                if (prop.Value == null || prop.Value.GetValueKind() == JsonValueKind.Null)
                {
                    jo.Remove(prop.Key);
                }
            }
        }
    }
}
