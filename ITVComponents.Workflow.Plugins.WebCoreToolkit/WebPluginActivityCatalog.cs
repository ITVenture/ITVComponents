using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.WebPlugins;
using ITVComponents.Workflow.Activities;

namespace ITVComponents.Workflow.Plugins.WebCoreToolkit
{
    /// <summary>
    /// Ein <see cref="IWorkflowActivityCatalog"/>, der die verfuegbaren Aktivitaets-Typen und deren Parameter
    /// ueber die <b>WebPlugin-Schnittstelle</b> (<see cref="IWebPluginsSelector"/>) aufloest - NICHT ueber
    /// einen <c>IDynamicLoader</c> wie der stack-neutrale <see cref="PluginActivityCatalog"/>. Er wird selbst
    /// als (Web-)Plugin geladen und mit der Liste der auf einem Worker <b>zulaessigen</b>
    /// Aktivitaets-Plugin-Namen konfiguriert.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Passt zum Umgebungs-/Instanz-Modell: jede Service-Instanz (Worker) einer Umgebung hat ihren eigenen
    /// Katalog mit ihrer erlaubten Aktivitaets-Menge. Der Editor erfragt fuer das <c>ExecutionTarget</c> einer
    /// automatisierten Aktivitaet genau diesen Katalog nach Typen und Parametern.
    /// </para>
    /// <para>
    /// Je Aktivitaet holt der Katalog die WebPlugin-Definition (<c>UniqueName -&gt; Constructor</c>) ueber den
    /// <see cref="IWebPluginsSelector"/>, loest den CLR-Typ <b>reflection-only</b> ueber die Factory auf
    /// (<see cref="PluginFactory.TryGetPluginType"/>) und liest die Attribute
    /// (<see cref="WorkflowActivityAttribute"/> / <see cref="ActivityParameterAttribute"/>) - ohne die
    /// Aktivitaet zu instanzieren. Die eigentliche Attribut-Auswertung teilt er sich mit dem
    /// In-Process-Katalog ueber <see cref="ActivityReflection"/>.
    /// </para>
    /// <para>
    /// Konstruktion als Plugin: die Factory injiziert sich selbst (<c>AllowFactoryParameter</c>); der
    /// <see cref="IWebPluginsSelector"/> wird ueber das in der Factory registrierte Objekt
    /// (<see cref="Global.PlugInSelectorName"/>) bezogen - denselben Weg nutzt die WebPlugin-Infrastruktur
    /// selbst. Die Namen der zulaessigen Aktivitaeten kommen aus dem Konstruktions-String der WebPlugin-
    /// Definition dieses Katalogs.
    /// </para>
    /// </remarks>
    public sealed class WebPluginActivityCatalog : IWorkflowActivityCatalog, IPlugin
    {
        private readonly PluginFactory factory;
        private readonly IWebPluginsSelector selector;
        private readonly string[] activityRefs;

        /// <summary>
        /// Initialisiert den Katalog mit der Factory (die ihn laedt) und der Liste der zulaessigen
        /// Aktivitaets-Plugin-Namen. Der <see cref="IWebPluginsSelector"/> wird aus der Factory bezogen.
        /// </summary>
        /// <param name="factory">die ladende Plugin-Factory (injiziert sich selbst)</param>
        /// <param name="activityNames">die auf diesem Worker zulaessigen Aktivitaets-Plugin-Namen</param>
        public WebPluginActivityCatalog(PluginFactory factory, string[] activityNames)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            this.selector = factory.GetRegisteredObject(ITVComponents.WebCoreToolkit.Global.PlugInSelectorName) as IWebPluginsSelector
                            ?? throw new InvalidOperationException(
                                "Im Plugin-Factory-Kontext ist kein IWebPluginsSelector registriert - der " +
                                $"{nameof(WebPluginActivityCatalog)} kann nur im WebCoreToolkit-Plugin-Stack geladen werden.");
            this.activityRefs = activityNames ?? Array.Empty<string>();
        }

        /// <summary>Gets or sets the UniqueName of this Plugin.</summary>
        public string UniqueName { get; set; }

        /// <summary>Informs a calling class of a Disposal of this Instance.</summary>
        public event EventHandler Disposed;

        /// <inheritdoc/>
        public IReadOnlyList<ActivityTypeInfo> GetActivityTypes()
        {
            var result = new List<ActivityTypeInfo>();
            foreach (string name in activityRefs)
            {
                Type type = TypeFor(name);
                if (type != null)
                {
                    result.Add(ActivityReflection.ToActivityTypeInfo(name, type));
                }
            }

            // Als Array (nicht List): ueber die InterProcessCommunication muss der Laufzeittyp eines
            // Rueckgabewerts ein Array sein; ein Array erfuellt zugleich IReadOnlyList<T>.
            return result.ToArray();
        }

        /// <inheritdoc/>
        public IReadOnlyList<ActivityParameter> GetParameters(string activityRef)
            => ActivityReflection.GetParameters(TypeFor(activityRef));

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

            // Der Provider wird ueber seinen UniqueName aufgeloest. Der Name im Attribut ist ein Template:
            // {UniqueName} (der Name der Activity) und {ParameterName} werden ersetzt. Kein Name -> die
            // Aktivitaet selbst muss IValuesProvider sein (aufgeloest ueber ihren eigenen Namen).
            string providerName = string.IsNullOrEmpty(attr.ValuesProvider)
                ? activityRef
                : attr.ValuesProvider
                    .Replace("{UniqueName}", activityRef)
                    .Replace("{ParameterName}", parameterName);

            IPluginFactory scope = null;
            try
            {
                // Eigener Scope: der Provider wird darin geladen und beim Schliessen wieder freigegeben.
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

        /// <summary>
        /// Der CLR-Typ einer zulaessigen Aktivitaet (reflection-only, ohne Instanz) - oder null, wenn es die
        /// WebPlugin-Definition nicht gibt oder ihr Typ keine <see cref="IActivityPlugin"/> ist.
        /// </summary>
        private Type TypeFor(string activityRef)
        {
            if (string.IsNullOrEmpty(activityRef))
            {
                return null;
            }

            WebPlugin def;
            try
            {
                def = selector.GetPlugin(activityRef);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(
                    $"WebPlugin definition '{activityRef}' could not be read: {ex.OutlineException()}",
                    LogSeverity.Warning);
                return null;
            }

            if (string.IsNullOrEmpty(def?.Constructor))
            {
                return null;
            }

            return factory.TryGetPluginType(def.Constructor, out Type type)
                   && typeof(IActivityPlugin).IsAssignableFrom(type)
                ? type
                : null;
        }

        /// <summary>Gibt den Katalog frei (die geteilte Factory gehoert ihm NICHT).</summary>
        public void Dispose()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }
    }
}
