using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ITVComponents.Formatting;
using ITVComponents.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace ITVComponents.WebCoreToolkit.Net.Handlers
{
    public static class Tools
    {
        /// <summary>
        /// Translates the given input field. If a value was prefixed with a dollar-sign, it will be formatted using ITVComponents.Formatting feature
        /// </summary>
        /// <param name="input">the query-value that needs to be translated</param>
        /// <param name="actionContext">the context of the current action</param>
        /// <returns></returns>
        public static string TranslateValue(string input, object actionContext)
        {
            if (!string.IsNullOrEmpty(input) && input.StartsWith("$"))
            {
                return actionContext.FormatText(input.Substring(1), TextFormat.DefaultFormatPolicyWithPrimitives);
            }

            return input;
        }

        /// <summary>
        /// Translates a specific filter to a Dictionary that is processable by the Context-Extensions for ForeignKey processing
        /// </summary>
        /// <param name="values">the values that were posted in a forms-dictionary</param>
        /// <returns>a more accurate search-dictioanry</returns>
        public static Dictionary<string, object> TranslateForm(IFormCollection values, Func<string, StringValues, object> propertyCallback = null, bool expectFilterForm = false)
        {
            var ret = new Dictionary<string, object>();
            foreach (var v in values)
            {
                var tmp = propertyCallback?.Invoke(v.Key, v.Value);
                if (tmp != null)
                {
                    ret.Add($"Parsed{v.Key}", tmp);
                }
                
                if (expectFilterForm)
                {
                    switch (v.Key)
                    {
                        case "sort":
                        case "page":
                        case "group":
                            {
                                LogEnvironment.LogDebugEvent($"Ignoring {v.Key}", LogSeverity.Report);
                                break;
                            }
                        case "filter":
                            {
                                var tmpFilter = v.Value.FirstOrDefault();
                                var st = "~contains~'";
                                if (tmpFilter?.Contains(st, StringComparison.OrdinalIgnoreCase) ?? false)
                                {
                                    var id = tmpFilter.IndexOf(st, StringComparison.OrdinalIgnoreCase);
                                    var searchName = tmpFilter.Substring(0, id);
                                    if (searchName == "Label")
                                    {
                                        id += st.Length;
                                        var ln = tmpFilter.Length - 1 - id;
                                        if (ln > 0)
                                        {
                                            tmpFilter = tmpFilter.Substring(id, ln);
                                            ret.Add("Filter", tmpFilter);
                                        }
                                    }
                                    else
                                    {
                                        LogEnvironment.LogEvent($"Unexpected Search-Filter: {tmpFilter}",
                                            LogSeverity.Warning);
                                    }
                                }
                                else
                                {
                                    LogEnvironment.LogEvent($"Unexpected Search-Filter: {tmpFilter}", LogSeverity.Warning);
                                }

                                break;
                            }
                        default:
                            {
                                ret.Add(v.Key, v.Value.FirstOrDefault());
                                break;
                            }
                    }
                }
                else
                {
                    ret.Add(v.Key, v.Value.FirstOrDefault());
                }
            }

            return ret;
        }


        public static bool RegexValidate(string value, string regexPattern)
        {
            return Regex.IsMatch(value, regexPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }
    }
}
