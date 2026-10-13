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
            if (!bufferedObjects.ContainsKey(name) && rootSettings.TryGetValue(name, out var setting) && setting != null)
            {
                // GetValue<T> kann nur ein JsonValue - ein Abschnitt aus der Datei ist ein JsonObject. Die Knoten
                // werden kopiert: sie haengen noch an ihrem Objekt und koennen in kein zweites.
                if (setting is not JsonObject section)
                {
                    throw new InvalidOperationException($"'{name}' is no section but a {setting.GetValueKind()} value.");
                }

                var tmp = section.ToDictionary(n => n.Key, n => n.Value?.DeepClone());
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
            if (!bufferedObjects.ContainsKey(name) && rootSettings.TryGetValue(name, out var setting) && setting != null)
            {
                // Deserialize statt GetValue<T>: GetValue kann nur ein JsonValue, ein Objekt aus der Datei ist keines.
                retVal = setting.Deserialize<T>();
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

        /// <summary>
        /// Ersetzt den Wert unter <paramref name="name"/> beim naechsten <see cref="Save"/>; <c>null</c> entfernt ihn.
        /// </summary>
        /// <param name="name">der Name des Eintrags</param>
        /// <param name="value">der neue Wert - ein Objekt, das serialisiert wird, oder ein <see cref="JsonNode"/></param>
        public void SetSubSection(string name, object value)
        {
            bufferedObjects[name] = value;
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
                    // Kopien: nach dem ersten Speichern haengen die Knoten am vorigen Objekt, und ein zweites
                    // Save wuerde sie sonst ein zweites Mal einhaengen.
                    var jt = new JsonObject(jse.rootSettings.Select(n => new KeyValuePair<string, JsonNode>(n.Key, n.Value?.DeepClone())));
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
