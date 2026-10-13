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

        /// <summary>
        /// Wie <see cref="GetSection{T}"/>, fuer einen Typ, der erst zur Laufzeit feststeht. Fehlt der Abschnitt,
        /// kommt die Vorbelegung des Typs zurueck.
        /// </summary>
        public static object GetSection(this IConfiguration configuration, string path, Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            var retVal = Activator.CreateInstance(type);
            var cfg = configuration.GetSection(path);
            if (cfg.Exists())
            {
                cfg.Bind(retVal);
            }

            return retVal;
        }
    }
}
