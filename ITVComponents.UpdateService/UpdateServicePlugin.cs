using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Versioning;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.InterProcessCommunication.NamedPipe.Hub;
using ITVComponents.InterProcessCommunication.NamedPipe.Server;
using ITVComponents.Json;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Modules;
using ITVComponents.GenericService.Sessions;
using ITVComponents.Settings.Native;
using ITVComponents.UpdateService.Abstractions.Contracts;
using ITVComponents.UpdateService.Agents;
using ITVComponents.UpdateService.Feed;
using ITVComponents.UpdateService.Installation;
using ITVComponents.UpdateService.Modules;
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
        /// <summary>
        /// The id of the target formed by the single values of <see cref="UpdateServiceOptions"/>; its plugin instances lie in
        /// <c>&lt;DataDirectory&gt;\targets\default\plugins.json</c>, its state directly in <c>&lt;DataDirectory&gt;</c>.
        /// </summary>
        public const string DefaultTargetId = UpdateServiceOptions.DefaultTargetId;

        private readonly IServiceHubProvider hubProvider;
        private readonly NamedPipeHubEndPoint endPoint;
        private readonly string settingsSection;
        private readonly IServiceSessionEvents? sessionEvents;
        private HttpClient? http;
        private System.Threading.Timer? roleTimer;
        private NamedPipeServer? server;
        private List<UpdateTarget> targets = new List<UpdateTarget>();
        private System.Threading.SemaphoreSlim? installGate;

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
            if (string.IsNullOrWhiteSpace(options.FeedUrl) || string.IsNullOrWhiteSpace(options.DataDirectory))
            {
                throw new InvalidOperationException($"The update service needs FeedUrl and DataDirectory in {settingsSection}.");
            }

            IReadOnlyList<UpdateServiceOptions> resolved;
            try
            {
                resolved = options.ResolveTargets();
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException($"{settingsSection}: {ex.Message}", ex);
            }

            UpdateContractTypes.RegisterAll((t, alias) => JsonHelper.RegisterManualType(t, alias));
            ProtectedDirectory.Ensure(options.DataDirectory);
            var packages = Directory.CreateDirectory(Path.Combine(options.DataDirectory, "packages")).FullName;
            var logs = Directory.CreateDirectory(Path.Combine(options.DataDirectory, "logs")).FullName;

            // Shared by every target: the connection, the package store and the right to run msiexec.
            http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            var downloader = new PackageDownloader(http, packages);
            installGate = new System.Threading.SemaphoreSlim(1, 1);
            targets = resolved.Select(t => CreateTarget(t, downloader, logs)).ToList();
            if (sessionEvents is { Active: true })
            {
                sessionEvents.SessionChanged += SessionChanged;
            }
            else
            {
                LogEnvironment.LogEvent("The update service receives no session notifications (run the service with UseGenericWindowsService and pass $serviceSessionEvents); there is no handover between sessions.", LogSeverity.Warning);
            }

            roleTimer = new System.Threading.Timer(_ => TickRoles(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            var facade = new UpdateServiceFacade(targets);
            endPoint.ClientDisconnected += ClientDisconnected;
            server = new NamedPipeServer(hubProvider, new Dictionary<string, object> { { UpdateContractTypes.FacadeObjectName, facade } }, options.HubServiceName);
            server.Initialize();
            foreach (var target in targets)
            {
                target.Coordinator.Start();
            }

            Initialized = true;
            LogEnvironment.LogEvent($"The update service runs for {string.Join(", ", targets.Select(t => t.ToString()))} (feed {options.FeedUrl}, channel {options.Channel}).", LogSeverity.Report);
        }

        /// <summary>
        /// Builds everything that belongs to one target alone.
        /// </summary>
        private UpdateTarget CreateTarget(UpdateServiceOptions options, PackageDownloader downloader, string logs)
        {
            // The target "default" keeps its state where the service kept it before there were targets, so that a switch
            // to a list of targets loses nothing. Every folder lies below DataDirectory and inherits its ACL: the
            // application reads plugins.json, only the service writes.
            var targetDirectory = Directory.CreateDirectory(Path.Combine(options.DataDirectory, "targets", options.TargetId)).FullName;
            var stateDirectory = string.Equals(options.TargetId, DefaultTargetId, StringComparison.OrdinalIgnoreCase) ? options.DataDirectory : targetDirectory;
            var registry = new AgentRegistry(options);
            var stateStore = new InstallStateStore(stateDirectory);
            var applicationDirectory = Path.GetDirectoryName(Path.GetFullPath(options.ApplicationPath))!;
            var moduleFiles = new ModuleFiles(options.ModulesDirectory ?? Path.Combine(applicationDirectory, "modules"), applicationDirectory);
            var pluginInstances = new ModulePluginRegistry(Path.Combine(targetDirectory, ModulePluginInstanceFile.FileName), moduleFiles);
            var moduleManager = new ModuleManager(options, downloader, moduleFiles, stateStore, null, pluginInstances);
            moduleManager.ReconcilePlugins();
            var coordinator = new UpdateCoordinator(options, new UpdateFeedClient(http!, options), downloader,
                new MsiPackageVerifier(options), new MsiInstaller(), new MsiInstallResultEvaluator(), stateStore,
                registry, new ApplicationControl(options), logs, null, moduleManager, installGate);
            var roles = new RoleCoordinator(registry, new WindowsSessions(), new ApplicationControl(options), () => coordinator.HoldsApplication);
            return new UpdateTarget(options, coordinator, registry, roles, moduleManager);
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
            foreach (var target in targets)
            {
                target.Coordinator.Dispose();
            }

            server?.Dispose();
            http?.Dispose();
            installGate?.Dispose();
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        private void SessionChanged(object? sender, ServiceSessionChangedEventArgs e)
        {
            foreach (var target in targets)
            {
                try
                {
                    target.Roles.SessionChanged(e.Reason, e.SessionId);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"The session change ({e.Reason}, session {e.SessionId}) could not be handled for {target}: {ex.OutlineException()}", LogSeverity.Error);
                }
            }
        }

        private void TickRoles()
        {
            foreach (var target in targets)
            {
                try
                {
                    target.Roles.Tick();
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"The handover between sessions failed a step for {target}: {ex.OutlineException()}", LogSeverity.Error);
                }
            }
        }

        private void ClientDisconnected(object? sender, PipeClientEventArgs e)
        {
            foreach (var target in targets)
            {
                target.Registry.Remove(e.Client.ConnectionId);
            }
        }
    }
}
