using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.Versioning;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.InterProcessCommunication.NamedPipe.Hub;
using ITVComponents.InterProcessCommunication.NamedPipe.Server;
using ITVComponents.Json;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.Settings.Native;
using ITVComponents.UpdateService.Abstractions.Contracts;
using ITVComponents.UpdateService.Agents;
using ITVComponents.UpdateService.Feed;
using ITVComponents.UpdateService.Installation;
using ITVComponents.UpdateService.Packages;

namespace ITVComponents.UpdateService
{
    /// <summary>
    /// The update service as a plugin of a generic service. It needs the hub and its pipe endpoint as plugins next to it:
    /// <code>
    /// hub      [ITVComponents.InterProcessCommunication.MessagingShared]&lt;…MessageServiceHub&gt;()
    /// pipe     […NamedPipe]&lt;…NamedPipeHubEndPoint&gt;("MiniStore.Updates", $hub, "", false, false)
    /// updates  [ITVComponents.UpdateService]&lt;…UpdateServicePlugin&gt;($hub, $pipe, "ITVenture:UpdateService")
    /// </code>
    /// The service runs as LocalSystem; its settings come from its own configuration (<see cref="UpdateServiceOptions"/>).
    /// Run the generic service with <c>FailOnStartupError</c>, so that a broken start is restarted by Windows.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class UpdateServicePlugin : IPlugin, IDeferredInit
    {
        private readonly IServiceHubProvider hubProvider;
        private readonly NamedPipeHubEndPoint endPoint;
        private readonly string settingsSection;
        private HttpClient? http;
        private UpdateCoordinator? coordinator;
        private NamedPipeServer? server;
        private AgentRegistry? registry;

        public UpdateServicePlugin(IServiceHubProvider hubProvider, NamedPipeHubEndPoint endPoint, string settingsSection)
        {
            this.hubProvider = hubProvider;
            this.endPoint = endPoint;
            this.settingsSection = settingsSection;
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
            var facade = new UpdateServiceFacade(coordinator, registry);
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
            endPoint.ClientDisconnected -= ClientDisconnected;
            coordinator?.Dispose();
            server?.Dispose();
            http?.Dispose();
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        private void ClientDisconnected(object? sender, PipeClientEventArgs e)
        {
            registry?.Remove(e.Client.ConnectionId);
        }
    }
}
