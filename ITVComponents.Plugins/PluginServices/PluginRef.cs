using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.ExtendedFormatting;

namespace ITVComponents.Plugins.PluginServices
{
    public class PluginRef : IBasicKeyValueProvider
    {
        public string UniqueName { get; internal set; }

        public Type PluginType { get; internal set; }

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
