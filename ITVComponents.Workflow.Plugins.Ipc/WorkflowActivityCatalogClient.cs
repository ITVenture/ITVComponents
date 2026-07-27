using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using ITVComponents.InterProcessCommunication.Shared.Base;
using ITVComponents.Plugins;
using ITVComponents.Workflow.Activities;

namespace ITVComponents.Workflow.Plugins.Ipc
{
    /// <summary>
    /// Der Client-Gegenpart zu einem im Backend exponierten <see cref="IWorkflowActivityCatalog"/>
    /// (ueblicherweise ein <c>PluginActivityCatalog</c>). Reicht die Katalog-Abfragen ueber die
    /// InterProcessCommunication an das Backend durch - so kann ein Frontend (z.B. der Blazor-Editor)
    /// den Aktivitaets-Katalog abfragen, obwohl die Aktivitaets-Plugins nur im Backend-Prozess leben.
    /// </summary>
    /// <remarks>
    /// Muster wie <c>ScheduleManagementClient</c>/<c>ServiceContextConnector</c>: der Konstruktor holt
    /// sich per <see cref="IBaseClient.CreateProxy{T}(string)"/> einen dynamischen Proxy des
    /// Katalog-Objekts (aufgeloest ueber dessen UniqueName am Server) und leitet die drei Methoden
    /// daran weiter. Der Vertrag ist bewusst serialisierbar und ohne <c>System.Type</c> auf der Leitung
    /// gehalten (siehe <see cref="ActivityCatalog"/>-DTOs), damit er ueber die Prozessgrenze traegt.
    ///
    /// Bewusst KEINE Abhaengigkeit auf das WebCoreToolkit und kein <c>[ScopedDependency]</c> hier - der
    /// Wrapper bleibt transport- und host-neutral. Die DI-Registrierung (damit ein Web-Host
    /// <see cref="IWorkflowActivityCatalog"/> auf diesen Client abbildet) ist Host-Sache.
    /// </remarks>
    public sealed class WorkflowActivityCatalogClient : IWorkflowActivityCatalog, IPlugin
    {
        private readonly IWorkflowActivityCatalog proxy;

        /// <summary>
        /// Initialisiert den Client.
        /// </summary>
        /// <param name="client">der Basis-Client zur Kommunikation mit dem Backend-Dienst</param>
        /// <param name="remoteObjectName">
        /// der UniqueName, unter dem der Katalog am Server registriert ist
        /// </param>
        public WorkflowActivityCatalogClient(IBaseClient client, string remoteObjectName)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (string.IsNullOrEmpty(remoteObjectName))
            {
                throw new ArgumentNullException(nameof(remoteObjectName));
            }

            proxy = client.CreateProxy<IWorkflowActivityCatalog>(remoteObjectName);
        }

        /// <summary>Gets or sets the UniqueName of this Plugin.</summary>
        public string UniqueName { get; set; }

        /// <summary>Informs a calling class of a Disposal of this Instance.</summary>
        public event EventHandler Disposed;

        /// <inheritdoc/>
        public IReadOnlyList<ActivityTypeInfo> GetActivityTypes()
        {
            return proxy.GetActivityTypes();
        }

        /// <inheritdoc/>
        public IReadOnlyList<ActivityParameter> GetParameters(string activityRef)
        {
            IReadOnlyList<ActivityParameter> result = proxy.GetParameters(activityRef);
            if (result != null)
            {
                // Der einzige object-typisierte Wert im Vertrag ist der (typisierte) Default. Ueber die
                // IPC kommt er als JSON-Knoten zurueck (STJ liest object nicht typtreu) - hier wieder in
                // ein CLR-Primitiv entpacken, damit der Editor ihn wie in-process behandeln kann.
                foreach (ActivityParameter p in result)
                {
                    p.Default = UnwrapJson(p.Default);
                }
            }

            return result;
        }

        /// <inheritdoc/>
        public IReadOnlyList<ActivityParameterValue> GetValidValues(string activityRef, string parameterName)
        {
            return proxy.GetValidValues(activityRef, parameterName);
        }

        /// <summary>
        /// Entpackt einen ueber die IPC transportierten <c>object</c>-Wert (der als JSON-Knoten
        /// ankommt) wieder in ein CLR-Primitiv (string/double/bool). Andere Werte bleiben unveraendert.
        /// </summary>
        private static object UnwrapJson(object value)
        {
            switch (value)
            {
                case null:
                    return null;
                case JsonElement el:
                    return el.ValueKind switch
                    {
                        JsonValueKind.String => el.GetString(),
                        JsonValueKind.Number => el.GetDouble(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Null => null,
                        _ => el.GetRawText()
                    };
                case JsonNode node:
                    return node.GetValueKind() switch
                    {
                        JsonValueKind.String => node.GetValue<string>(),
                        JsonValueKind.Number => node.GetValue<double>(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Null => null,
                        _ => node.ToJsonString()
                    };
                default:
                    return value;
            }
        }

        /// <summary>Gibt den Client frei.</summary>
        public void Dispose()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }
    }
}
