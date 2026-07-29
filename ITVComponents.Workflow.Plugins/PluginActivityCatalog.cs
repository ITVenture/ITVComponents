using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Config;
using ITVComponents.Plugins.Initialization;
using ITVComponents.Workflow.Activities;

namespace ITVComponents.Workflow.Plugins
{
    /// <summary>
    /// In-Process-Implementierung des <see cref="IWorkflowActivityCatalog"/>: ermittelt die verfuegbaren
    /// Aktivitaets-Typen aus den Scoped-Plugin-Definitionen der <see cref="IDynamicLoader"/> der Factory
    /// und liest ihre Parameter per Reflection aus den Klassen-Attributen - OHNE die Aktivitaeten zu
    /// instanzieren.
    /// </summary>
    /// <remarks>
    /// Ist zugleich <see cref="IPlugin"/>, damit der Katalog unter einem UniqueName in einer Factory
    /// registriert und - fuer den verteilten Betrieb - ueber die InterProcessCommunication exponiert
    /// werden kann (der Vertrag ist bewusst serialisierbar/typfrei gehalten). Der Client-Gegenpart ist
    /// <c>WorkflowActivityCatalogClient</c> in <c>ITVComponents.Workflow.Plugins.Ipc</c>.
    /// </remarks>
    public sealed class PluginActivityCatalog : IInjectableWorkflowActivityCatalog
    {
        private readonly PluginFactory factory;

        /// <summary>Initialisiert den Katalog mit der Factory, deren Loader die Aktivitaeten kennen.</summary>
        public PluginActivityCatalog(PluginFactory factory)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <summary>Gets or sets the UniqueName of this Plugin.</summary>
        public string UniqueName { get; set; }

        /// <summary>Informs a calling class of a Disposal of this Instance.</summary>
        public event EventHandler Disposed;

        /// <inheritdoc/>
        public IReadOnlyList<ActivityTypeInfo> GetActivityTypes()
        {
            var result = new List<ActivityTypeInfo>();
            foreach ((PluginConfigurationItem item, Type type) in Activities())
            {
                result.Add(ActivityReflection.ToActivityTypeInfo(item.Name, type));
            }

            // Bewusst als Array (nicht List): ueber die InterProcessCommunication muss der Laufzeittyp
            // eines Rueckgabewerts ein Array sein - der Deserialisierer bildet den TypeName sonst auf
            // T[] statt auf das Element ab und scheitert. Ein Array erfuellt zugleich IReadOnlyList<T>.
            return result.ToArray();
        }

        /// <inheritdoc/>
        public IReadOnlyList<ActivityParameter> GetParameters(string activityRef)
        {
            return ActivityReflection.GetParameters(TypeFor(activityRef));
        }

        /// <inheritdoc/>
        public IReadOnlyList<ActivityParameterValue> GetValidValues(string activityRef, string parameterName)
        {
            Type type = TypeFor(activityRef);
            ActivityParameterAttribute attr = ActivityReflection.FindParameter(type, parameterName);
            if (attr == null)
            {
                return Array.Empty<ActivityParameterValue>();
            }

            if (attr.Kind != ActivityParameterKind.CallbackList)
            {
                // Statische Picklist (oder nichts).
                return ActivityReflection.StaticValues(attr);
            }

            // Der Provider wird ueber seinen UniqueName aufgeloest (DynamicLoader -> konfigurierter
            // Konstruktions-String). Der Name im Attribut ist ein Template: {UniqueName} (der Name der
            // Activity) und {ParameterName} werden ersetzt - so kann derselbe Activity-Klassen-Typ unter
            // verschiedenen UniqueNames verschiedene Provider ansprechen. Kein Name -> die Aktivitaet
            // selbst muss IValuesProvider sein (aufgeloest ueber ihren eigenen Namen).
            string providerName = string.IsNullOrEmpty(attr.ValuesProvider)
                ? activityRef
                : attr.ValuesProvider
                    .Replace("{UniqueName}", activityRef)
                    .Replace("{ParameterName}", parameterName);

            IPluginFactory scope = null;
            try
            {
                // Eigener Scope: der Provider wird darin (mit seiner gebundenen Konfiguration) geladen und
                // beim Schliessen wieder freigegeben.
                scope = factory.NewScope(new Dictionary<string, object>(), null, false);
                if (scope[providerName, true] is IValuesProvider provider)
                {
                    return provider.GetValues(parameterName)?.ToArray() ?? Array.Empty<ActivityParameterValue>();
                }

                LogEnvironment.LogEvent(
                    $"Value provider '{providerName}' for parameter '{parameterName}' of activity " +
                    $"'{activityRef}' could not be resolved as an IValuesProvider.", LogSeverity.Error);
                return Array.Empty<ActivityParameterValue>();
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(
                    $"Value provider '{providerName}' for parameter '{parameterName}' of activity " +
                    $"'{activityRef}' failed: {ex.OutlineException()}", LogSeverity.Error);
                return Array.Empty<ActivityParameterValue>();
            }
            finally
            {
                scope?.Dispose();
            }
        }

        private Type TypeFor(string activityRef)
        {
            return Activities()
                .Where(a => a.Item.Name == activityRef)
                .Select(a => a.Type)
                .FirstOrDefault();
        }

        /// <summary>
        /// Alle Scoped-Plugins der Loader, die sich als <see cref="IActivityPlugin"/> aufloesen lassen -
        /// samt ihres CLR-Typs (reflection-only, ohne Instanz).
        /// </summary>
        private IEnumerable<(PluginConfigurationItem Item, Type Type)> Activities()
        {
            foreach (IDynamicLoader loader in factory.GetPlugins<IDynamicLoader>())
            {
                IEnumerable<PluginConfigurationItem> names;
                try
                {
                    names = loader.GetScopedPluginNames() ?? Enumerable.Empty<PluginConfigurationItem>();
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent(
                        $"Could not read scoped plugin names from a dynamic loader: {ex.OutlineException()}",
                        LogSeverity.Warning);
                    continue;
                }

                foreach (PluginConfigurationItem item in names)
                {
                    if (item?.ConstructionString != null
                        && factory.TryGetPluginType(item.ConstructionString, out Type type)
                        && typeof(IActivityPlugin).IsAssignableFrom(type))
                    {
                        yield return (item, type);
                    }
                }
            }
        }

        /// <summary>
        /// Gibt den Katalog frei. Die zugrunde liegende <see cref="PluginFactory"/> gehoert dem Katalog
        /// NICHT (sie wird geteilt und vom Host verwaltet) und wird daher hier nicht disposed.
        /// </summary>
        public void Dispose()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }
    }
}
