using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.ExtendedFormatting;
using ITVComponents.Plugins.Helpers;

namespace ITVComponents.Plugins.PluginServices
{
    public class PluginRef : IBasicKeyValueProvider
    {
        internal UniqueNameHelper UQ { get; set; }
        public string UniqueName => UQ.UniqueName;

        public string RawName => UQ.UniqueNameRaw;

        public Type PluginType { get; internal set; }

        /// <summary>
        /// Der unmittelbare Vorgaenger - das Plugin, dessen Aufloesung zum Laden DIESES Plugins gefuehrt
        /// hat. <c>null</c> an der Wurzel einer Ladekette.
        /// </summary>
        /// <remarks>
        /// Die Kette wird beim Parsen des Konstruktor-Strings geknuepft und ist danach unveraenderlich;
        /// sie gehoert dem Ladevorgang, nicht dem Plugin-Objekt. Damit ist in einer Konfiguration nicht
        /// nur der direkte Aufrufer erreichbar, sondern der ganze Weg dorthin - siehe
        /// <see cref="PrevPlugin"/>.
        /// </remarks>
        public PluginRef CallingPlugin { get; internal set; }

        /// <summary>
        /// Geht die Aufrufkette rueckwaerts. <c>PrevPlugin(0)</c> ist dieser Ref selbst,
        /// <c>PrevPlugin(1)</c> sein Aufrufer, und so weiter.
        /// </summary>
        /// <param name="n">die Anzahl Stufen, die zurueckgegangen wird</param>
        /// <returns>
        /// die n-te Stufe, oder <c>null</c> wenn die Kette kuerzer ist. Bewusst kein Wurf: eine
        /// Konfiguration, die zu weit zurueckgreift, soll als "nicht aufloesbar" enden statt den ganzen
        /// Ladevorgang zu sprengen.
        /// </returns>
        public PluginRef PrevPlugin(int n)
        {
            var retVal = this;
            while (n > 0 && retVal != null)
            {
                retVal = retVal.CallingPlugin;
                n--;
            }

            return retVal;
        }

        public Type GenericTypeDefinition =>
            PluginType.IsGenericType ? PluginType.GetGenericTypeDefinition() : null;

        public object this[string name] => GetGenericArgument(name);

        public string[] Keys
        {
            get
            {
                var gen = GenericTypeDefinition;
                if (gen != null)
                {
                    var args = gen.GetGenericArguments();
                    var id = args.Select((t, i) => t.Name).ToArray();
                    return id;
                }

                return Array.Empty<string>();
            }
        }

        public bool ContainsKey(string key)
        {
            var gen = GenericTypeDefinition;
            if (gen != null)
            {
                var args = gen.GetGenericArguments();
                var id = args.Select((t, i) => new { Index = i, Type = t })
                    .FirstOrDefault(n => n.Type.Name == key)?.Index ?? -1;
                return id != -1;
            }

            return false;
        }

        private Type GetGenericArgument(string genericArgumentName)
        {
            var gen = GenericTypeDefinition;
            if (gen != null)
            {
                Type retVal = null;
                var args = gen.GetGenericArguments();
                var id = args.Select((t, i) => new { Index = i, Type = t })
                    .FirstOrDefault(n => n.Type.Name == genericArgumentName)?.Index ?? -1;
                if (id != -1)
                {
                    var imps = PluginType.GenericTypeArguments;
                    retVal = imps[id];
                }

                return retVal;
            }

            return null;
        }
    }
}
