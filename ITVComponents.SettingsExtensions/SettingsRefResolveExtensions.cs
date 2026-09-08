using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Formatting;
using ITVComponents.Logging;
using ITVComponents.Scripting.CScript.Security;
using Microsoft.Extensions.Configuration;

namespace ITVComponents.SettingsExtensions
{
    public static class SettingsRefResolveExtensions
    {
        public static void RefResolve<T>(this IConfiguration configuration, T optionsModel)
        {
            ResolveObjProps(configuration, typeof(T), optionsModel);
        }

        private static void ResolveObjProps(IConfiguration configuration, Type t, object model)
        {
            if (model is IDictionary<string, object> dc)
            {
                foreach (var combo in dc.ToArray())
                {
                    if (combo.Value is string s && !string.IsNullOrEmpty(s))
                    {
                        s = GetValue(s, configuration, combo, out var apply);
                        if (apply)
                        {
                            dc[combo.Key] = s;
                        }
                    }
                }
            }
            else if (model is IDictionary weakDic)
            {
                foreach (var key in weakDic.Keys.Cast<object>().ToArray())
                {
                    if (weakDic[key] is string s && !string.IsNullOrEmpty(s))
                    {
                        s = GetValue(s, configuration, new KeyValuePair<string, object>(key as string ?? key?.ToString(), s), out var apply);
                        if (apply)
                        {
                            weakDic[key] = s;
                        }
                    }
                }
            }
            else
            {
                var allMembers =
                    t.GetProperties(BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.Public);
                foreach (var member in allMembers)
                {
                    if (member.PropertyType == typeof(string))
                    {
                        var raw = member.GetValue(model);
                        if (raw is string s && !string.IsNullOrEmpty(s) && member.CanWrite)
                        {
                            s = GetValue(s, configuration, model, out var apply);
                            if (apply)
                            {
                                member.SetValue(model, s);
                            }
                        }
                    }
                    else if (Attribute.IsDefined(member, typeof(AutoResolveChildrenAttribute)))
                    {
                        var raw = member.GetValue(model);
                        if (raw is IEnumerable i && raw is not IDictionary<string, object> && raw is not IDictionary)
                        {
                            foreach (var item in i)
                            {
                                if (item != null)
                                {
                                    ResolveObjProps(configuration, item.GetType(), item);
                                }
                            }
                        }
                        else if (raw != null)
                        {
                            ResolveObjProps(configuration, raw.GetType(), raw);
                        }
                    }
                }
            }
        }

        private static string GetValue(string expression, IConfiguration configuration, object model, out bool applyValue)
        {
            applyValue = false;
            if (expression.StartsWith(":-->"))
            {
                expression = expression.Substring(4);
                var defaultId = expression.IndexOf("??");
                string defaultValue = null;
                if (defaultId != -1)
                {
                    defaultValue = expression.Substring(defaultId + 2);
                    expression = expression.Substring(0, defaultId);
                }

                var key = expression;
                expression = configuration[key];
                if (string.IsNullOrEmpty(expression))
                {
                    if (!string.IsNullOrEmpty(defaultValue))
                    {
                        expression = defaultValue;
                    }
                    else
                    {
                        LogEnvironment.LogEvent($"Unable to resolve settings-reference ':-->{key}': the key is not set and no default (??) was provided.", LogSeverity.Warning);
                    }
                }

                applyValue = true;
                return expression;
            }
            else if (expression.StartsWith("$-->"))
            {
                expression = expression.Substring(4);
                expression = new { Me = model, Config = configuration }.FormatText(expression, ScriptingPolicy.Default);
                applyValue = true;
                return expression;
            }

            return expression;
        }
    }
}
