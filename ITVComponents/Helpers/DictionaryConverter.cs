using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Text;
using ITVComponents.Logging;

namespace ITVComponents.Helpers
{
    public static class DictionaryConverter
    {
        /// <summary>
        /// Converts an object into a dictionary
        /// </summary>
        /// <param name="source">the source object</param>
        /// <param name="simpleTypesOnly">indicates whether to ignore all properties that represent types that are not basic-types or enums</param>
        /// <returns>a dictionary representing the source object</returns>
        public static Dictionary<string, object> ToDictionary(this object source, bool simpleTypesOnly = false)
        {
            if (source != null)
            {
                Type t = source.GetType();
                return (from p in
                            t.GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.GetProperty |
                                            BindingFlags.Instance | BindingFlags.Public)
                        where (!simpleTypesOnly || IsSimpleType(p.PropertyType)) && !Attribute.IsDefined(p, typeof(ExcludeFromDictionaryAttribute))
                        select new {n = p.Name, v = p.GetValue(source, null)}).ToDictionary(p => p.n, p => p.v);
            }

            return null;
        }

        /// <summary>
        /// Converts a KeyValue-Collection instance to a string-object dictionary
        /// </summary>
        /// <param name="source">the source collection</param>
        /// <returns>a dictionary containing the same values</returns>
        public static Dictionary<string, object> ToDictionary(this IEnumerable<KeyValuePair<string, string>> source)
        {
            Dictionary<string,object> retVal = new Dictionary<string, object>();
            foreach (var item in source)
            {
                retVal.Add(item.Key, item.Value);
            }

            return retVal;
        }

        /// <summary>
        /// Converts a NameValueCollection instance to a string-object dictionary
        /// </summary>
        /// <param name="source">the source collection</param>
        /// <returns>a dictionary containing the same values</returns>
        public static Dictionary<string, object> ToDictionary(this NameValueCollection source)
        {
            Dictionary<string,object> retVal = new Dictionary<string, object>();
            foreach (string key in source.AllKeys)
            {
                retVal.Add(key, source[key]);
            }

            return retVal;
        }

        /// <summary>
        /// Converts a JToken instance to a string-object Dictionary. If the Token represents an array or a literal, the name of the Property will be '.'.
        /// </summary>
        /// <param name="object">the object to convert</param>
        /// <returns>a dictionary containing the contents of the JToken</returns>

        public static Dictionary<string, object> ToDictionary(this JToken @object)
        {
            LogEnvironment.LogDebugEvent($"Entered JToken2Dictionary for {@object}", LogSeverity.Report);
            if (@object is JObject jObj)
            {
                LogEnvironment.LogDebugEvent($"{@object} is an object", LogSeverity.Report);
                return jObj.ToDictionary();
            }


            Dictionary<string,object> retVal = new Dictionary<string, object>();
            if (@object.Type == JTokenType.Array)
            {
                LogEnvironment.LogDebugEvent($"{@object} is an array", LogSeverity.Report);
                retVal.Add(".",ToArray((JArray) @object));
            }
            else
            {
                LogEnvironment.LogDebugEvent($"{@object} is {@object.Type}", LogSeverity.Report);
                retVal.Add(".",@object.ToObject(typeof(object)));
            }

            return retVal;
        }

        /// <summary>
        /// Converts a JObject instance to a string-object Dictionary
        /// </summary>
        /// <param name="object">the object to convert</param>
        /// <returns>a dictionary containing the contents of the JObject</returns>
        public static Dictionary<string, object> ToDictionary(this JObject @object)
        {
            LogEnvironment.LogDebugEvent($"Entered JObject2Dictionary for {@object}", LogSeverity.Report);
            Dictionary<string,object> retVal = new Dictionary<string, object>();
            foreach (var k in @object.Properties())
            {
                var v = k.Value;
                if (v.Type == JTokenType.Array)
                {
                    LogEnvironment.LogDebugEvent($"Property {k.Name } is Array", LogSeverity.Report);
                    retVal[k.Name] = ToArray((JArray) k.Value);
                }
                else if (v.Type == JTokenType.Object)
                {
                    LogEnvironment.LogDebugEvent($"Property {k.Name } is object", LogSeverity.Report);
                    retVal[k.Name] = ToDictionary((JObject) k.Value);
                }
                else
                {
                    LogEnvironment.LogDebugEvent($"Property {k.Name } is {k.Type}", LogSeverity.Report);
                    retVal[k.Name] = k.Value?.ToObject(typeof(object));
                }
            }

            return retVal;
        }

        /// <summary>
        /// Convers a JArray to a List of objects
        /// </summary>
        /// <param name="array">the JArray containing the objects to convert</param>
        /// <returns>a list of converted objects</returns>
        private static List<object> ToArray(JArray array)
        {
            LogEnvironment.LogDebugEvent($"Entered JArray2Array for {array}", LogSeverity.Report);
            List<object> retVal = new List<object>();
            foreach (var item in array)
            {
                if (item.Type == JTokenType.Array)
                {
                    LogEnvironment.LogDebugEvent($"found Array", LogSeverity.Report);
                    retVal.Add(ToArray((JArray) item));
                }
                else if (item.Type == JTokenType.Object)
                {
                    LogEnvironment.LogDebugEvent($"found Object", LogSeverity.Report);
                    retVal.Add(ToDictionary((JObject) item));
                }
                else
                {
                    LogEnvironment.LogDebugEvent($"found {item.Type}", LogSeverity.Report);
                    retVal.Add(item.ToObject(typeof(object)));
                }
            }

            return retVal;
        }

        /// <summary>
        /// indicates for a provided type whether it is a primitive type or not
        /// </summary>
        /// <param name="type">the type to check for being simple</param>
        /// <returns>a value indicating whether the provided type is simple</returns>
        public static bool IsSimpleType(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                // nullable type, check if the nested type is simple.
                return IsSimpleType(type.GetGenericArguments()[0]);
            }
            else if (type.IsArray && type.HasElementType)
            {
                return IsSimpleType(type.GetElementType());
            }

            return type.IsPrimitive
              || type.IsEnum
              || type.Equals(typeof(string))
              || type.Equals(typeof(decimal))
              || type.Equals(typeof(DateTime));
        }
    }
}
