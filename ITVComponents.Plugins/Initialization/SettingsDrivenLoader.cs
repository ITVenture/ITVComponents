using ITVComponents.Helpers;
using ITVComponents.Plugins.Config;
using ITVComponents.Scripting.CScript.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.ExtendedFormatting;
using ITVComponents.Plugins.Helpers;

namespace ITVComponents.Plugins.Initialization
{
    /// <summary>
    /// Ein <see cref="IDynamicLoader"/>, der seine Plugins aus den Einstellungen nimmt statt aus einer
    /// Datenbank.
    /// </summary>
    /// <remarks>
    /// Verhaelt sich nach aussen wie der datenbankgetriebene Lader: nur was nicht abgeschaltet ist, wird
    /// geliefert, und die Bereichs-Plugins sind die mit <see cref="PluginLoadType.Scope"/>.
    /// </remarks>
    public class SettingsDrivenLoader:IPlugin, IDynamicLoader
    {
        private readonly PluginConfigurationCollection plugins;
        private readonly GenericTypeConstructionCollection typeParameters;
        private readonly IPluginFactory factory;
        public string UniqueName { get; set; }

        public SettingsDrivenLoader(PluginConfigurationCollection plugins, GenericTypeConstructionCollection typeParameters, IPluginFactory factory)
        {
            this.plugins = plugins;
            this.typeParameters = typeParameters;
            this.factory = factory;
        }

        public void Dispose()
        {
            OnDisposed();
        }

        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler Disposed;
        /// <inheritdoc />
        /// <remarks>
        /// <c>Disabled</c> wird mitgeprueft. Ohne das laedt ein abgeschaltetes Plugin trotzdem - der Schalter
        /// stuende in der Konfiguration und haette keine Wirkung, was schlimmer ist als kein Schalter.
        /// </remarks>
        public IEnumerable<string> LoadDynamicAssemblies(PluginLoadType currentLoadType, bool writeAccess = true)
        {
            foreach(var plugin in plugins.Where(n => n.LoadType == currentLoadType && !n.Disabled))
            {
                factory.LoadPlugin<IPlugin>(plugin.Name, plugin.ConstructionString);
                yield return plugin.Name;
            }
        }

        public bool HasParamsFor(string uniqueName)
        {
            return typeParameters.ContainsKey(uniqueName) && typeParameters[uniqueName]?.Any() == true;
        }

        public void GetGenericParams(string uniqueName, List<GenericTypeArgument> genericTypeArguments, Dictionary<string, object> customVariables,
            StringFormatProvider formatter)
        {
            if (typeParameters.TryGetValue(uniqueName, out var list) && list?.Any() == true)
            {
                genericTypeArguments.BuildGenericArguments(uniqueName, list, customVariables, formatter);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// <b>Muss antworten und darf nicht werfen.</b> Die Fabrik fragt der Reihe nach JEDEN registrierten
        /// Lader, ob er einen Namen kennt (<c>FirstOrDefault</c>), und bei genau einem Lader ueberspringt sie
        /// diese Frage sogar und ruft direkt <see cref="GetScopedPlugin"/>. Ein Wurf an dieser Stelle legt
        /// damit das Aufloesen JEDES Bereichs-Plugins lahm, auch derer, die einem anderen Lader gehoeren.
        /// „Kenne ich nicht" ist eine gueltige Antwort, ein Fehler ist es nicht.
        /// </remarks>
        public bool HasScopedPlugin(string pluginName) => ScopedPlugin(pluginName) != null;

        /// <inheritdoc />
        public PluginConfigurationItem GetScopedPlugin(string pluginName) => ScopedPlugin(pluginName);

        /// <inheritdoc />
        public IEnumerable<PluginConfigurationItem> GetScopedPluginNames()
            => plugins.Where(IsAvailableScopePlugin).OrderBy(n => n.Name).ToArray();

        private PluginConfigurationItem ScopedPlugin(string pluginName)
            => plugins.FirstOrDefault(n => IsAvailableScopePlugin(n) && n.Name == pluginName);

        /// <summary>
        /// Dieselbe Bedingung wie beim datenbankgetriebenen Lader: Bereichs-Plugin und nicht abgeschaltet.
        /// </summary>
        private static bool IsAvailableScopePlugin(PluginConfigurationItem item)
            => item.LoadType == PluginLoadType.Scope && !item.Disabled;
    }
}
