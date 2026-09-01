using System;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.WebPlugins;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Plugins;
using ITVComponents.Workflow.ValueHandles;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.Workflow.Plugins.WebCoreToolkit
{
    /// <summary>
    /// Ein <see cref="IActivityHost"/> fuer den WebCoreToolkit-Stack: arbeitet jede Instanz je
    /// Arbeitseinheit in einem eigenen DI-Scope ab, der auf den Tenant der Instanz fixiert ist. So sehen
    /// die als Plugin geladenen Aktivitaeten, die Wert-Handler UND ihre tenant-abhaengigen (DB-)Kontexte
    /// konsistent den richtigen Tenant - ein tenant-uebergreifender Runner arbeitet damit jede Instanz
    /// unter ihrem Tenant ab.
    ///
    /// Beim Wert-Handler ist das kein Beiwerk: er holt fremde Daten, und <b>wessen</b> Daten das sind,
    /// entscheidet der fixierte Tenant. Ein Handler-Name in einer <b>oeffentlichen</b> Definition trifft
    /// damit je Mandant das, was dort unter ihm eingerichtet ist.
    /// </summary>
    /// <remarks>
    /// Muster nach dem Toolkit-Hintergrunddienst (<c>BackgroundTaskProcessorService</c> /
    /// <c>ScriptedHealthCheck</c>): pro Arbeitseinheit ein <see cref="IServiceScope"/>, darin ein leerer
    /// Ausfuehrungs-Kontext mit fixiertem Tenant (<c>PrepareEmptyContext(fixedScope)</c>) und die
    /// tenant-spezifische Plugin-Factory (<see cref="IWebPluginHelper.CreateOperationScope(string)"/>).
    ///
    /// Das ist die toolkit-native Alternative zum stack-neutralen <c>PluginActivityHost</c> +
    /// <c>WorkflowExecutionScope</c>: hier treibt der <c>IPermissionScope</c> die ganze Security-/
    /// Filter-Kette (Plugin-Auswahl, Tenant-Filter, Permissions), nicht nur der Workflow-Kontext.
    /// </remarks>
    public sealed class WebToolkitActivityHost : IActivityHost
    {
        /// <summary>
        /// Standard-Benutzername des Hintergrundprozesses. Aktiviert die tenant-abhaengige Filterung
        /// (der Benutzer muss nicht existieren); ein spaeter angelegter Benutzer dieses Namens laesst
        /// sich ueber die normalen TenantUser-/Rollen-Zuordnungen mit Rechten ausstatten.
        /// </summary>
        public const string DefaultBackgroundUserName = "#Toolkit#Process";

        private readonly IServiceProvider rootServices;
        private readonly string backgroundUserName;

        /// <summary>
        /// Initialisiert den Host mit dem (globalen) Service-Provider, aus dem je Vortrieb ein Scope
        /// erzeugt wird.
        /// </summary>
        /// <param name="rootServices">der globale Service-Provider</param>
        /// <param name="backgroundUserName">
        /// der Benutzername, unter dem der Hintergrundprozess laeuft (aktiviert die Tenant-Filter); der
        /// Standard ist <see cref="DefaultBackgroundUserName"/>
        /// </param>
        public WebToolkitActivityHost(IServiceProvider rootServices,
            string backgroundUserName = DefaultBackgroundUserName)
        {
            this.rootServices = rootServices ?? throw new ArgumentNullException(nameof(rootServices));
            this.backgroundUserName = backgroundUserName;
        }

        /// <inheritdoc/>
        public IActivityScope OpenScope(WorkflowInstance instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            return new WebToolkitActivityScope(rootServices, backgroundUserName, instance.TenantId);
        }

        private sealed class WebToolkitActivityScope : IActivityScope
        {
            private readonly IServiceScope diScope;
            private readonly string tenant;
            private IPluginFactory pluginScope;

            public WebToolkitActivityScope(IServiceProvider root, string userName, string tenant)
            {
                this.tenant = tenant;
                diScope = root.CreateScope();
                // Hintergrund-Scope unter dem Tenant der Instanz: authentifizierter (synthetischer) Benutzer
                // -> FilterAvailable == true -> die tenant-abhaengigen Filter greifen und scopen ueber den
                // fixierten IPermissionScope auf den Tenant der Instanz (im benutzerfreien Zustand waeren die
                // Filter komplett aus).
                diScope.ServiceProvider.PrepareBackgroundContext(userName, tenant, out _);
            }

            public IWorkflowActivity Resolve(string activityRef)
            {
                if (PluginScope[activityRef, true] is IActivityPlugin plugin)
                {
                    return plugin;
                }

                throw new InvalidOperationException(
                    $"Fuer den ActivityRef '{activityRef}' konnte im Tenant '{tenant ?? "(none)"}' kein " +
                    "Workflow-Aktivitaets-Plugin (IActivityPlugin) aufgeloest werden.");
            }

            public IWorkflowValueHandler ResolveValueHandler(string handlerName)
            {
                // Derselbe Operations-Scope wie fuer die Schritte - und damit dieselben scope-owned
                // Kontexte. Ein eigener Scope hiesse eine zweite Plugin-Factory und ein zweites Set
                // Kontexte fuer denselben Knoten.
                if (PluginScope[handlerName, true] is IValueHandlerPlugin plugin)
                {
                    return plugin;
                }

                throw new InvalidOperationException(
                    $"Fuer den Handler-Namen '{handlerName}' konnte im Tenant '{tenant ?? "(none)"}' kein " +
                    "Workflow-Wert-Handler (IValueHandlerPlugin) aufgeloest werden.");
            }

            /// <summary>
            /// Die tenant-spezifische Plugin-Factory samt Operations-Scope - traege geoeffnet.
            /// </summary>
            /// <remarks>
            /// Erst beim ersten aufgeloesten Plugin (Plugin-Auswahl + scope-owned Kontexte fuer den
            /// Tenant); beim Dispose des Scopes wird alles wieder freigegeben. Genau EINMAL je
            /// Arbeitseinheit: <c>CreateOperationScope(tenant)</c> baut je Aufruf eine neue Factory und
            /// wuerde die vorige unbemerkt ersetzen.
            /// </remarks>
            private IPluginFactory PluginScope => pluginScope ??= OpenPluginScope();

            private IPluginFactory OpenPluginScope()
            {
                IWebPluginHelper helper = diScope.ServiceProvider.GetRequiredService<IWebPluginHelper>();
                return string.IsNullOrEmpty(tenant)
                    ? helper.CreateOperationScope()
                    : helper.CreateOperationScope(tenant);
            }

            public void Dispose()
            {
                // Erst den Plugin-Operations-Scope (stoppt/disposed die geladenen Schritt- und
                // Handler-Plugins und die scope-owned Kontexte), dann den DI-Scope.
                pluginScope?.Dispose();
                pluginScope = null;
                diScope?.Dispose();
            }
        }
    }
}
