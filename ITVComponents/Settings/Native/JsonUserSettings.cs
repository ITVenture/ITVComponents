using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ITVComponents.Helpers;
using Newtonsoft.Json.Linq;

namespace ITVComponents.Settings.Native
{
    public class JsonUserSettings
    {
        private string fileName;

        private Dictionary<string, JToken> rootSettings;

        private Dictionary<string, object> bufferedObjects = new Dictionary<string, object>();

        private JsonUserSettings parent;

        public static JsonUserSettings LoadFrom(string fileName)
        {
            if (File.Exists(fileName))
            {
                return new JsonUserSettings
                {
                    fileName = fileName,
                    rootSettings = JsonHelper.ReadObject<Dictionary<string, JToken>>(fileName)
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
                rootSettings = new Dictionary<string, JToken>()
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
                var tmp = setting.ToObject<Dictionary<string,JToken>>();
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
                retVal = setting.ToObject<T>();
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
                JsonHelper.WriteObject(rootSettings, fileName);
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
                    var jt = JObject.FromObject(jse.rootSettings);
                    rootSettings[tmp.Key] =jt;
                    CleanupObject(jt);
                }
                else if (tmp.Value != null)
                {
                    var jt = JToken.FromObject(tmp.Value);
                    rootSettings[tmp.Key] = jt;
                    if (jt is JObject jo)
                    {
                        CleanupObject(jo);
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

        private void CleanupObject(JObject jo)
        {
            var items = jo.Properties().ToArray();
            foreach (var prop in items)
            {
                if (prop.Value == null || prop.Value.Type == JTokenType.Null)
                {
                    jo.Remove(prop.Name);
                }
            }
        }
    }
}
