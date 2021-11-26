using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.CSharp.RuntimeBinder;
using Binder = Microsoft.CSharp.RuntimeBinder.Binder;
/*#if (NETSTANDARD2_0 || NETCOREAPP2_0)
using CSharpBinderFlags = Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfoFlags;
using CSharpArgumentInfo = Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo;
using CSharpArgumentInfoFlags = Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfoFlags;
#endif*/

using ITVComponents.Logging;


namespace ITVComponents.ExtendedFormatting
{
    /// <summary>
    /// Provides functionality to process regex notation driven formating ([{objectId.}propertyName{:format}{,padding}])
    /// </summary>
    public static class RegexFormat
    {
        /// <summary>
        /// The Regex that is used to read the format-settings provided for one or multiple objects
        /// </summary>
        private const string FormattingRegex = @"\[((?<objectId>\d+)\.)?(?<propertyName>([\w_\$\@]|\.)+)(\:(?<format>([^,\]\""\\]+|(\""(\\\""|\\|[^\""\\]+)*\""))*))?(\,(?<padding>\-?\d+))?\]";

        /// <summary>
        /// Formats a string using the Regex-formatter notation. [{objectId.}PropertyName{:format}{,padding}]
        /// </summary>
        /// <param name="format">the format that must be applied to the objects of the array</param>
        /// <param name="data">the objects that must be formatted</param>
        /// <returns>the formatted string</returns>
        [Obsolete("Use ITVComponents.Formatting.dll instead!", true)]
        public static string Format(string format, params object[] data)
        {
            format = format.Replace("[[", "#!#OP#!#").Replace("]]", "#!#CL#!#");
            string retVal = Regex.Replace(format, FormattingRegex, m => Evaluator(m,data, DefaultFormat),
                                          RegexOptions.CultureInvariant |
                                          RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace |
                                          RegexOptions.Multiline);
            retVal = retVal.Replace("#!#OP#!#", "[").Replace("#!#CL#!#", "]");
            return retVal;
        }

        /// <summary>
        /// Formats a string using the Regex-formatter notation. [{objectId.}PropertyName{:format}{,padding}]
        /// </summary>
        /// <param name="format">the format that must be applied to the objects of the array</param>
        /// <param name="formatCallback">a user-defined method describing the default method how objects are formatted</param>
        /// <param name="data">the objects that need to be formatted</param>
        /// <returns>the formatted string</returns>
        [Obsolete("Use ITVComponents.Formatting.dll instead!",true)]
        public static string Format(string format, Func<object, string, string> formatCallback, params object[] data)
        {
            string retVal = Regex.Replace(format, FormattingRegex, m => Evaluator(m, data, formatCallback),
                                          RegexOptions.CultureInvariant |
                                          RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace |
                                          RegexOptions.Multiline);
            return retVal;
        }

        /// <summary>
        /// Runs the Format method on a specific object
        /// </summary>
        /// <param name="value">the object on which to run the Format method</param>
        /// <param name="format">the demanded format</param>
        /// <returns>the format result of the given format hint</returns>
        [Obsolete("Use ITVComponents.Formatting.dll instead!", true)]
        public static string Format(this object value, string format)
        {
            return Format(format, value);
        }

        
        /// <summary>
        /// Runs the Format method on a specific object
        /// </summary>
        /// <param name="value">the object on which to run the Format method</param>
        /// <param name="format">the demanded format</param>
        /// <param name="formatCallback">a user-defined method describing the default method how objects are formatted</param>
        /// <returns>the format result of the given format hint</returns>
        [Obsolete("Use ITVComponents.Formatting.dll instead!", true)]
        public static string Format(this object value, string format, Func<object, string, string> formatCallback)
        {
            return Format(format, formatCallback, value);
        }

        /// <summary>
        /// The default implementation of a FormatCallback. Formats an object according to the demanded format
        /// </summary>
        /// <param name="value">the value that needs to be formatted</param>
        /// <param name="format">the demanded format</param>
        /// <returns>the string representing the value in the requested format</returns>
        [Obsolete("Use ITVComponents.Formatting.dll instead!", true)]
        public static string DefaultFormat(object value, string format)
        {
            string retVal = string.Empty;
            if (format.Equals("obj", StringComparison.OrdinalIgnoreCase))
            {
                return ObjectFormatter.FormatObject(value);
            }

            IFormattable formattable = value as IFormattable;
            if (formattable != null)
            {
                retVal = formattable.ToString(format, null);
            }

            return retVal;
        }

        /// <summary>
        /// Replaces one  Format expression with the appropriate format
        /// </summary>
        /// <param name="match">the match that contains the captured format hint</param>
        /// <param name="data">the data that was passed to the Format method</param>
        /// <param name="formatCallback">a user-defined method describing the default method how objects are formatted</param>
        /// <returns>the string representing the result of the given format hint</returns>
        private static string Evaluator(Match match, object[] data, Func<object,string,string> formatCallback)
        {
            string retVal = "";
            int objectId = 0;
            string propertyName = match.Groups["propertyName"].Value;
            string format = null;
            int padding = 0;
            if (match.Groups["objectId"].Success)
            {
                objectId = int.Parse(match.Groups["objectId"].Value);
            }

            if (match.Groups["format"].Success)
            {
                format = match.Groups["format"].Value;
            }

            if (match.Groups["padding"].Success)
            {
                padding = int.Parse(match.Groups["padding"].Value);
            }

            object o = GetObject(data[objectId], propertyName);
            if (o!= null && !string.IsNullOrEmpty(format))
            {
                retVal = formatCallback(o, format);
            }
            else if (o != null)
            {
                retVal = o.ToString();
            }

            if (padding > 0 && retVal.Length < padding)
            {
                retVal = retVal.PadLeft(padding);
            }
            else if (padding < 0 && retVal.Length < Math.Abs(padding))
            {
                retVal = retVal.PadRight(Math.Abs(padding));
            }
            else if (padding != 0 && retVal.Length > Math.Abs(padding))
            {
                retVal = retVal.Substring(0, Math.Abs(padding));
            }

            return retVal;
        }

        /// <summary>
        /// Gets the demanded value from the given object
        /// </summary>
        /// <param name="source">the source object from which to get the desired value</param>
        /// <param name="propertyPath">the path to the Property to read from the object</param>
        /// <returns>the value of the given property</returns>
        private static object GetObject(object source, string propertyPath)
        {
            string[] path = TokenizePath(propertyPath);
            object retVal = source;
            foreach (string propertyName in path)
            {
                object ret = null;
                if (retVal is IDictionary)
                {
                    IDictionary tmp = (IDictionary) retVal;
                    if (tmp.Contains(propertyName))
                    {
                        ret = tmp[propertyName];
                    }
                }
                else if (retVal is IDictionary<string, object>)
                {
                    IDictionary<string, object> tmp = (IDictionary<string, object>)retVal;
                    if (tmp.ContainsKey(propertyName))
                    {
                        ret = tmp[propertyName];
                    }
                    else
                    {
                        LogEnvironment.LogDebugEvent(null, string.Format("Property {0} is unknown", propertyName), (int)LogSeverity.Warning, null);
                    }
                }
                else if (retVal is IBasicKeyValueProvider)
                {
                    IBasicKeyValueProvider tmp = (IBasicKeyValueProvider)retVal;
                    ret = tmp[propertyName];
                }
                else if (retVal is IDataReader)
                {
                    IDataReader tmp = (IDataReader)retVal;
                    for (int i = 0; i < tmp.FieldCount; i++)
                    {
                        if (tmp.GetName(i).Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                        {
                            ret = tmp[i];
                            break;
                        }
                    }
                }
                else if (retVal is DataRow)
                {
                    DataRow tmp = (DataRow)retVal;
                    if (tmp.Table != null && tmp.Table.Columns.IndexOf(propertyName) != -1)
                    {
                        ret = tmp[propertyName];
                    }
                }
                else if (retVal is DynamicObject)
                {
                    try
                    {

                        var binder = Binder.GetMember(CSharpBinderFlags.None, propertyName, retVal.GetType(),
                                                      new[]
                                                          {
                                                              CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null)
                                                          });
                        var callsite = CallSite<Func<CallSite, object, object>>.Create(binder);
                        ret = callsite.Target(callsite, retVal);
                    }
                    catch (Exception ex)
                    {
                        LogEnvironment.LogDebugEvent(null, ex.ToString(), (int) LogSeverity.Error, null);
                        throw;
                    }
                }

                if (ret == null && retVal != null)
                {
                    Type t = retVal.GetType();
                    PropertyInfo info = t.GetProperty(propertyName,
                                                      BindingFlags.FlattenHierarchy | BindingFlags.GetProperty |
                                                      BindingFlags.IgnoreCase |
                                                      BindingFlags.Instance | BindingFlags.Public);
                    if (info != null)
                    {
                        try
                        {
                            ret = info.GetValue(retVal, null);
                        }
                        catch (Exception ex)
                        {
                            ret = null;
                        }
                    }
                    else if (propertyName == ".")
                    {
                        ret = retVal;
                    }
                    else
                    {
                        FieldInfo f = t.GetField(propertyName);
                        if (f != null)
                        {
                            ret = f.GetValue(retVal);
                        }
                    }
                }

                retVal = ret;
                if (retVal == null)
                {
                    break;
                }
            }

            return retVal;
        }

        /// <summary>
        /// Parses a PropertyPath simply by splitting with points and treating doublepoints a properties
        /// </summary>
        /// <param name="propertyPath">the path to a specific property</param>
        /// <returns>a string array that holds all splitted elements of the specified propertypath</returns>
        private static string[] TokenizePath(string propertyPath)
        {
            List<string> retVal = new List<string>();
            StringBuilder currentString = new StringBuilder();
            bool lastDot = true;
            foreach (char c in propertyPath)
            {
                if (c == '.' && lastDot)
                {
                    retVal.Add(".");
                }
                else if (c == '.' && !lastDot)
                {
                    retVal.Add(currentString.ToString());
                    currentString.Clear();
                    lastDot = true;
                }
                else
                {
                    lastDot = false;
                    currentString.Append(c);
                }
            }

            if (!lastDot)
            {
                retVal.Add(currentString.ToString());
                currentString.Clear();
            }

            return retVal.ToArray();
        }
    }
}
