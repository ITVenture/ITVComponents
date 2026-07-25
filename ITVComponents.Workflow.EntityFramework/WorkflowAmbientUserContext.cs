using System;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.EntityFramework.DIIntegration;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
using ITVComponents.Workflow.Runtime;

namespace ITVComponents.Workflow.EntityFramework
{
    /// <summary>
    /// Ein <see cref="IUserAwareContext"/>, der den Tenant aus dem ambienten
    /// <see cref="WorkflowExecutionScope"/> liest (nicht aus einem HTTP-Benutzer). Damit lassen sich im
    /// tenant-uebergreifenden Runner AUCH beliebige andere tenant-abhaengige DB-Kontexte (fachliche
    /// Daten) korrekt auf den Tenant der gerade vorangetriebenen Instanz einstellen: der Host injiziert
    /// diesen Kontext als <see cref="IUserAwareContext"/> jener Kontexte.
    /// </summary>
    /// <remarks>
    /// Gedacht fuer den Dienst-/Runner-Betrieb. Im Web nutzt man weiterhin den echten Security-Kontext;
    /// dort ist kein Scope aktiv und <see cref="CurrentTenant"/> waere null. Der <see cref="WorkflowContext"/>
    /// selbst braucht diesen Adapter NICHT - er konsultiert den <see cref="WorkflowExecutionScope"/> direkt.
    /// </remarks>
    [ScopedDependency(FriendlyName = "WorkflowAmbientUserContext")]
    public sealed class WorkflowAmbientUserContext : IUserAwareContext, IPlugin
    {
        /// <summary>Im Dienst gibt es keinen angemeldeten Benutzer.</summary>
        public string CurrentUserName => null;

        /// <summary>Der Tenant der gerade vorangetriebenen Instanz (oder null).</summary>
        public string CurrentTenant => WorkflowExecutionScope.CurrentTenant;

        /// <inheritdoc/>
        public string UniqueName { get; set; }

        /// <inheritdoc/>
        public event EventHandler Disposed;

        /// <inheritdoc/>
        public void Dispose()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }
    }
}
