using System;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.WebPlugins;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Plugins;
using ITVComponents.Workflow.ValueHandles;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.Workflow.Plugins.WebCoreToolkit
{
    /// <summary>
    /// Ein <see cref="IValueHandlerHost"/> fuer den WebCoreToolkit-Stack: loest die Wert-Handler je
    /// Aufloesungsrunde in einem eigenen DI-Scope auf, der auf den Tenant der Instanz fixiert ist.
    /// </summary>
    /// <remarks>
    /// Dasselbe Muster wie <see cref="WebToolkitActivityHost"/> - und hier ist es kein Beiwerk: der
    /// Handler holt fremde Daten, und <b>wessen</b> Daten das sind, entscheidet der fixierte Tenant. Ein
    /// mandantenuebergreifender Runner arbeitet damit jede Instanz unter ihrem Mandanten ab, und ein
    /// Handler-Name in einer <b>oeffentlichen</b> Definition trifft je Mandant das, was dort unter ihm
    /// eingerichtet ist.
    /// </remarks>
    public sealed class WebToolkitValueHandlerHost : IValueHandlerHost
    {
        private readonly IServiceProvider rootServices;
        private readonly string backgroundUserName;

        /// <summary>
        /// Initialisiert den Host mit dem (globalen) Service-Provider, aus dem je Runde ein Scope erzeugt
        /// wird.
        /// </summary>
        /// <param name="rootServices">der globale Service-Provider</param>
        /// <param name="backgroundUserName">
        /// der Benutzername, unter dem der Hintergrundprozess laeuft (aktiviert die Tenant-Filter); der
        /// Standard ist <see cref="WebToolkitActivityHost.DefaultBackgroundUserName"/>
        /// </param>
        public WebToolkitValueHandlerHost(IServiceProvider rootServices,
            string backgroundUserName = WebToolkitActivityHost.DefaultBackgroundUserName)
        {
            this.rootServices = rootServices ?? throw new ArgumentNullException(nameof(rootServices));
            this.backgroundUserName = backgroundUserName;
        }

        /// <inheritdoc/>
        public IValueHandlerScope OpenScope(WorkflowInstance instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            return new WebToolkitValueHandlerScope(rootServices, backgroundUserName, instance.TenantId);
        }

        private sealed class WebToolkitValueHandlerScope : IValueHandlerScope
        {
            private readonly IServiceScope diScope;
            private readonly string tenant;
            private IPluginFactory pluginScope;

            public WebToolkitValueHandlerScope(IServiceProvider root, string userName, string tenant)
            {
                this.tenant = tenant;
                diScope = root.CreateScope();
                diScope.ServiceProvider.PrepareBackgroundContext(userName, tenant, out _);
            }

            public IWorkflowValueHandler Resolve(string handlerName)
            {
                // Traege: erst beim ersten aufgeloesten Handler die tenant-spezifische Plugin-Factory
                // samt Operations-Scope oeffnen; beim Dispose wird alles wieder freigegeben.
                pluginScope ??= OpenPluginScope();

                if (pluginScope[handlerName, true] is IValueHandlerPlugin plugin)
                {
                    return plugin;
                }

                throw new InvalidOperationException(
                    $"Fuer den Handler-Namen '{handlerName}' konnte im Tenant '{tenant ?? "(none)"}' kein " +
                    "Workflow-Wert-Handler (IValueHandlerPlugin) aufgeloest werden.");
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
                pluginScope?.Dispose();
                pluginScope = null;
                diScope?.Dispose();
            }
        }
    }
}
