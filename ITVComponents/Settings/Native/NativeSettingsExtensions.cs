using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace ITVComponents.Settings.Native
{
    public static class NativeSettingsExtensions
    {
        public static T GetSection<T>(this IConfiguration configuration, string path, Action<T> configureDefaults = null) where T : class, new()
        {
            var retVal = new T();
            var cfg = configuration.GetSection(path);
            if (cfg.Exists())
            {
                cfg.Bind(retVal);
            }
            else
            {
                configureDefaults?.Invoke(retVal);
            }

            return retVal;
        }

        public static void RefResolve<T>(this IConfiguration configuration, T optionsModel)
        {
            ResolveObjProps(configuration, typeof(T), optionsModel);
        }

        private static void ResolveObjProps(IConfiguration configuration, Type t, object model)
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
                        if (s.StartsWith(":-->"))
                        {
                            s = s.Substring(4);
                            var defaultId = s.IndexOf("??");
                            string defaultValue = null;
                            if (defaultId != -1)
                            {
                                defaultValue = s.Substring(defaultId + 2);
                                s = s.Substring(0, defaultId);
                            }
                            s = configuration[s];
                            if (string.IsNullOrEmpty(s) && !string.IsNullOrEmpty(defaultValue))
                            {
                                s = defaultValue;
                            }

                            member.SetValue(model, s);
                        }
                    }
                }
                else if (Attribute.IsDefined(member, typeof(AutoResolveChildrenAttribute)))
                {
                    var raw = member.GetValue(model);
                    if (raw is IEnumerable i)
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
}
