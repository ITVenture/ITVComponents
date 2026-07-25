using System;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.WebPlugins;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.Workflow.Plugins.WebCoreToolkit
{
    /// <summary>
    /// Ein <see cref="IActivityHost"/> fuer den WebCoreToolkit-Stack: treibt jede Instanz je Vortrieb in
    /// einem eigenen DI-Scope voran, der auf den Tenant der Instanz fixiert ist. So sehen die als Plugin
    /// geladenen Aktivitaeten UND ihre tenant-abhaengigen (DB-)Kontexte konsistent den richtigen Tenant -
    /// ein tenant-uebergreifender Runner arbeitet damit jede Instanz unter ihrem Tenant ab.
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
        private readonly IServiceProvider rootServices;

        /// <summary>
        /// Initialisiert den Host mit dem (globalen) Service-Provider, aus dem je Vortrieb ein Scope
        /// erzeugt wird.
        /// </summary>
        public WebToolkitActivityHost(IServiceProvider rootServices)
        {
            this.rootServices = rootServices ?? throw new ArgumentNullException(nameof(rootServices));
        }

        /// <inheritdoc/>
        public IActivityScope OpenScope(WorkflowInstance instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            return new WebToolkitActivityScope(rootServices, instance.TenantId);
        }

        private sealed class WebToolkitActivityScope : IActivityScope
        {
            private readonly IServiceScope diScope;
            private readonly string tenant;
            private IPluginFactory pluginScope;

            public WebToolkitActivityScope(IServiceProvider root, string tenant)
            {
                this.tenant = tenant;
                diScope = root.CreateScope();
                // Hintergrund-Scope unter dem Tenant der Instanz: fixiert den IPermissionScope, damit
                // tenant-abhaengige Kontexte auf diesen Tenant filtern (auch ohne HTTP-Benutzer).
                diScope.ServiceProvider.PrepareEmptyContext(tenant, out _);
            }

            public IWorkflowActivity Resolve(string activityRef)
            {
                // Traege: erst beim ersten aufgeloesten Schritt die tenant-spezifische Plugin-Factory samt
                // Operations-Scope oeffnen (Plugin-Auswahl + scope-owned Kontexte fuer den Tenant); beim
                // Dispose des Scopes wird alles wieder freigegeben.
                pluginScope ??= OpenPluginScope();

                if (pluginScope[activityRef, true] is IActivityPlugin plugin)
                {
                    return plugin;
                }

                throw new InvalidOperationException(
                    $"Fuer den ActivityRef '{activityRef}' konnte im Tenant '{tenant ?? "(none)"}' kein " +
                    "Workflow-Aktivitaets-Plugin (IActivityPlugin) aufgeloest werden.");
            }

            private IPluginFactory OpenPluginScope()
            {
                IWebPluginHelper helper = diScope.ServiceProvider.GetRequiredService<IWebPluginHelper>();
                return string.IsNullOrEmpty(tenant)
                    ? helper.CreateOperationScope()
                    : helper.CreateOperationScope(tenant);
            }

            public void Dispose()
            {
                // Erst den Plugin-Operations-Scope (stoppt/disposed die geladenen Schritt-Plugins und die
                // scope-owned Kontexte), dann den DI-Scope.
                pluginScope?.Dispose();
                pluginScope = null;
                diScope?.Dispose();
            }
        }
    }
}
