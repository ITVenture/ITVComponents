using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Formatting;
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
            if (model is not IDictionary<string, object> dc)
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
                        if (raw is IEnumerable i && raw is not IDictionary<string,object>)
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
            else
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

                expression = configuration[expression];
                if (string.IsNullOrEmpty(expression) && !string.IsNullOrEmpty(defaultValue))
                {
                    expression = defaultValue;
                }

                applyValue = true;
                return expression;
            }
            else if (expression.StartsWith("$-->"))
            {
                expression = expression.Substring(4);
                expression = new { Me = model, Config = configuration }.FormatText(expression);
                applyValue = true;
                return expression;
            }

            return expression;
        }
    }
}
