using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.Versioning;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.InterProcessCommunication.NamedPipe.Hub;
using ITVComponents.InterProcessCommunication.NamedPipe.Server;
using ITVComponents.Json;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.GenericService.Sessions;
using ITVComponents.Settings.Native;
using ITVComponents.UpdateService.Abstractions.Contracts;
using ITVComponents.UpdateService.Agents;
using ITVComponents.UpdateService.Feed;
using ITVComponents.UpdateService.Installation;
using ITVComponents.UpdateService.Packages;
using ITVComponents.UpdateService.Sessions;

namespace ITVComponents.UpdateService
{
    /// <summary>
    /// The update service as a plugin of a generic service. It needs the hub and its pipe endpoint as plugins next to it:
    /// <code>
    /// hub      [ITVComponents.InterProcessCommunication.MessagingShared]&lt;…MessageServiceHub&gt;()
    /// pipe     […NamedPipe]&lt;…NamedPipeHubEndPoint&gt;("MiniStore.Updates", $hub, "", false, false)
    /// updates  [ITVComponents.UpdateService]&lt;…UpdateServicePlugin&gt;($hub, $pipe, "ITVenture:UpdateService", $serviceSessionEvents)
    /// </code>
    /// <c>$serviceSessionEvents</c> are the session notifications of the generic service; they reach the plugin when the
    /// service runs with <c>UseGenericWindowsService()</c>. Without them there is no handover between sessions.
    /// The service runs as LocalSystem; its settings come from its own configuration (<see cref="UpdateServiceOptions"/>).
    /// Run the generic service with <c>FailOnStartupError</c>, so that a broken start is restarted by Windows.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class UpdateServicePlugin : IPlugin, IDeferredInit
    {
        private readonly IServiceHubProvider hubProvider;
        private readonly NamedPipeHubEndPoint endPoint;
        private readonly string settingsSection;
        private readonly IServiceSessionEvents? sessionEvents;
        private HttpClient? http;
        private RoleCoordinator? roles;
        private System.Threading.Timer? roleTimer;
        private UpdateCoordinator? coordinator;
        private NamedPipeServer? server;
        private AgentRegistry? registry;

        public UpdateServicePlugin(IServiceHubProvider hubProvider, NamedPipeHubEndPoint endPoint, string settingsSection)
            : this(hubProvider, endPoint, settingsSection, null)
        {
        }

        public UpdateServicePlugin(IServiceHubProvider hubProvider, NamedPipeHubEndPoint endPoint, string settingsSection, IServiceSessionEvents? serviceSessionEvents)
        {
            this.hubProvider = hubProvider;
            this.endPoint = endPoint;
            this.settingsSection = settingsSection;
            sessionEvents = serviceSessionEvents;
        }

        /// <inheritdoc />
        public string? UniqueName { get; set; }

        /// <inheritdoc />
        public bool Initialized { get; private set; }

        /// <inheritdoc />
        public bool ForceImmediateInitialization => false;

        /// <inheritdoc />
        public event EventHandler? Disposed;

        /// <inheritdoc />
        public void Initialize()
        {
            if (Initialized)
            {
                return;
            }

            var options = NativeSettings.GetSection<UpdateServiceOptions>(settingsSection);
            if (string.IsNullOrWhiteSpace(options.ProductId) || string.IsNullOrWhiteSpace(options.FeedUrl)
                                                            || string.IsNullOrWhiteSpace(options.DataDirectory) || string.IsNullOrWhiteSpace(options.ApplicationPath))
            {
                throw new InvalidOperationException($"The update service needs ProductId, FeedUrl, DataDirectory and ApplicationPath in {settingsSection}.");
            }

            UpdateContractTypes.RegisterAll((t, alias) => JsonHelper.RegisterManualType(t, alias));
            ProtectedDirectory.Ensure(options.DataDirectory);
            var packages = Directory.CreateDirectory(Path.Combine(options.DataDirectory, "packages")).FullName;
            var logs = Directory.CreateDirectory(Path.Combine(options.DataDirectory, "logs")).FullName;

            http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            registry = new AgentRegistry(options);
            coordinator = new UpdateCoordinator(options, new UpdateFeedClient(http, options), new PackageDownloader(http, packages),
                new MsiPackageVerifier(options), new MsiInstaller(), new MsiInstallResultEvaluator(), new InstallStateStore(options.DataDirectory),
                registry, new ApplicationControl(options), logs);
            var application = new ApplicationControl(options);
            var updates = coordinator;
            roles = new RoleCoordinator(registry, new WindowsSessions(), application, () => updates.HoldsApplication);
            if (sessionEvents is { Active: true })
            {
                sessionEvents.SessionChanged += SessionChanged;
            }
            else
            {
                LogEnvironment.LogEvent("The update service receives no session notifications (run the service with UseGenericWindowsService and pass $serviceSessionEvents); there is no handover between sessions.", LogSeverity.Warning);
            }

            roleTimer = new System.Threading.Timer(_ => TickRoles(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            var facade = new UpdateServiceFacade(coordinator, registry, roles);
            endPoint.ClientDisconnected += ClientDisconnected;
            server = new NamedPipeServer(hubProvider, new Dictionary<string, object> { { UpdateContractTypes.FacadeObjectName, facade } }, options.HubServiceName);
            server.Initialize();
            coordinator.Start();
            Initialized = true;
            LogEnvironment.LogEvent($"The update service for {options.ProductId} runs (feed {options.FeedUrl}, channel {options.Channel}).", LogSeverity.Report);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            roleTimer?.Dispose();
            if (sessionEvents != null)
            {
                sessionEvents.SessionChanged -= SessionChanged;
            }

            endPoint.ClientDisconnected -= ClientDisconnected;
            coordinator?.Dispose();
            server?.Dispose();
            http?.Dispose();
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        private void SessionChanged(object? sender, ServiceSessionChangedEventArgs e)
        {
            roles?.SessionChanged(e.Reason, e.SessionId);
        }

        private void TickRoles()
        {
            try
            {
                roles?.Tick();
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"The handover between sessions failed a step: {ex.OutlineException()}", LogSeverity.Error);
            }
        }

        private void ClientDisconnected(object? sender, PipeClientEventArgs e)
        {
            registry?.Remove(e.Client.ConnectionId);
        }
    }
}
