using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.NamedPipe.Security;
using ITVComponents.Logging;

namespace ITVComponents.UpdateService.Agents
{
    /// <summary>
    /// An application that announced itself to the service.
    /// </summary>
    public sealed class RegisteredAgent
    {
        public string ConnectionId { get; init; } = string.Empty;

        public string UserName { get; init; } = string.Empty;

        public int SessionId { get; init; }

        public int ProcessId { get; init; }

        public string? ProcessPath { get; init; }

        public string Version { get; init; } = string.Empty;

        public DateTimeOffset RegisteredUtc { get; init; }

        /// <summary>
        /// Gets a value indicating whether the instance said it is active when it registered. A hint for a service that
        /// restarted and does not know yet which instance is active - nothing else.
        /// </summary>
        public bool ClaimedActive { get; init; }

        /// <summary>
        /// Gets the target the application belongs to (issue ModulePlugins-Instances, D).
        /// </summary>
        public string TargetId { get; init; } = UpdateServiceOptions.DefaultTargetId;

        /// <inheritdoc />
        public override string ToString() => string.Equals(TargetId, UpdateServiceOptions.DefaultTargetId, StringComparison.OrdinalIgnoreCase)
            ? $"{UserName} (session {SessionId}, process {ProcessId}, version {Version})"
            : $"{UserName} ({TargetId}, session {SessionId}, process {ProcessId}, version {Version})";
    }

    /// <summary>
    /// The applications connected right now. An application counts from its registration until its pipe connection ends;
    /// everything about it (user, session, process, path) comes from the operating system.
    /// </summary>
    public sealed class AgentRegistry
    {
        private readonly UpdateServiceOptions options;
        private readonly IWindowsServices? services;
        private readonly ConcurrentDictionary<string, RegisteredAgent> agents = new ConcurrentDictionary<string, RegisteredAgent>(StringComparer.Ordinal);

        /// <param name="options">the options of the target</param>
        /// <param name="services">the service control manager; needed for a target of the kind <c>WindowsService</c>, whose
        /// applications are refused without it</param>
        public AgentRegistry(UpdateServiceOptions options, IWindowsServices? services = null)
        {
            this.options = options;
            this.services = services;
        }

        /// <summary>
        /// Is raised when a registered application is gone.
        /// </summary>
        public event EventHandler<RegisteredAgent>? AgentLeft;

        /// <summary>
        /// Is raised when an application registered (also again, over the same connection).
        /// </summary>
        public event EventHandler<RegisteredAgent>? AgentRegistered;

        /// <summary>
        /// Gets the registered applications.
        /// </summary>
        public IReadOnlyList<RegisteredAgent> Active => agents.Values.ToArray();

        /// <summary>
        /// Gets the target this registry keeps the applications of.
        /// </summary>
        public string TargetId => options.TargetId;

        /// <summary>
        /// Gets the product of the target.
        /// </summary>
        public string ProductId => options.ProductId;

        /// <summary>
        /// Indicates whether a process of the given path belongs to this target: it runs from the application's folder,
        /// or the target does not ask for that. Lets the service pick the target before it registers.
        /// </summary>
        public bool Admits(string? processPath) => !options.RequireApplicationPath || RunsFromApplicationFolder(processPath);

        /// <summary>
        /// Registers the caller as an application.
        /// </summary>
        /// <param name="caller">the identity the pipe hub read from the operating system</param>
        /// <param name="productId">the product the caller claims to belong to</param>
        /// <param name="version">the version it claims to have (shown only, never trusted for a decision)</param>
        /// <param name="claimsActive">whether the instance says it is active (a hint after a restart of the service)</param>
        /// <param name="reason">why the registration was refused</param>
        /// <param name="runningInstance">the instance that already runs in the same session, if that is the reason</param>
        /// <returns>the registration, or null when refused</returns>
        public RegisteredAgent? Register(ClaimsIdentity? caller, string productId, string version, bool claimsActive, out string? reason, out RegisteredAgent? runningInstance)
        {
            reason = null;
            runningInstance = null;
            if (caller == null || caller.AuthenticationType != PipeClaimTypes.AuthenticationType)
            {
                reason = "Only an application on this machine, connected through the service's pipe, can register.";
                return null;
            }

            if (!string.Equals(productId, options.ProductId, StringComparison.OrdinalIgnoreCase))
            {
                reason = $"This service keeps {options.ProductId} up to date, not {productId}.";
                return null;
            }

            var processPath = caller.FindFirst(PipeClaimTypes.ProcessPath)?.Value;
            if (options.RequireApplicationPath && !RunsFromApplicationFolder(processPath))
            {
                reason = $"The process {processPath ?? "(unknown)"} does not run from the application's folder.";
                return null;
            }

            if (options.IsWindowsService)
            {
                var callerProcess = int.Parse(caller.FindFirst(PipeClaimTypes.ProcessId)?.Value ?? "0", CultureInfo.InvariantCulture);
                reason = CheckServiceProcess(callerProcess);
                if (reason != null)
                {
                    return null;
                }
            }

            var connectionId = caller.FindFirst(PipeClaimTypes.ConnectionId)?.Value ?? string.Empty;
            var sessionId = int.Parse(caller.FindFirst(PipeClaimTypes.SessionId)?.Value ?? "-1", CultureInfo.InvariantCulture);
            runningInstance = agents.Values.FirstOrDefault(a => a.SessionId == sessionId && a.ConnectionId != connectionId);
            if (runningInstance != null)
            {
                reason = $"The application already runs in this session (process {runningInstance.ProcessId}, {runningInstance.UserName}).";
                return null;
            }

            var agent = new RegisteredAgent
            {
                ConnectionId = connectionId,
                UserName = caller.Name ?? string.Empty,
                SessionId = sessionId,
                ClaimedActive = claimsActive,
                ProcessId = int.Parse(caller.FindFirst(PipeClaimTypes.ProcessId)?.Value ?? "0", CultureInfo.InvariantCulture),
                ProcessPath = processPath,
                Version = version,
                RegisteredUtc = DateTimeOffset.UtcNow,
                TargetId = options.TargetId
            };
            var again = agents.ContainsKey(agent.ConnectionId);
            agents[agent.ConnectionId] = agent;
            if (!again)
            {
                LogEnvironment.LogEvent($"Application registered: {agent}.", LogSeverity.Report);
            }

            try
            {
                AgentRegistered?.Invoke(this, agent);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"A listener on the registration of {agent} failed: {ex.OutlineException()}", LogSeverity.Error);
            }

            return agent;
        }

        /// <summary>
        /// For a target of the kind <c>WindowsService</c>: the caller must be the process the service control manager names
        /// for the service - the folder alone would admit any program started from there.
        /// </summary>
        /// <returns>why not, or null</returns>
        private string? CheckServiceProcess(int callerProcess)
        {
            var serviceName = options.ServiceName ?? string.Empty;
            if (services == null)
            {
                LogEnvironment.LogEvent($"Target {options.TargetId} is the service {serviceName}, but the update service cannot ask the service control manager; every registration is refused.", LogSeverity.Error);
                return "The update service cannot check the service process.";
            }

            WindowsServiceInfo? info;
            try
            {
                info = services.Query(serviceName);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"The service control manager could not be asked about {serviceName} (target {options.TargetId}); the registration of process {callerProcess} is refused: {ex.OutlineException()}", LogSeverity.Error);
                return $"The service {serviceName} could not be checked.";
            }

            if (info == null)
            {
                return $"The service {serviceName} is not installed.";
            }

            if (!info.OwnProcess)
            {
                return $"The service {serviceName} shares its process with other services; its process does not identify it.";
            }

            if (info.ProcessId == 0)
            {
                return $"The service {serviceName} does not run ({info.State}).";
            }

            return info.ProcessId == callerProcess ? null : $"Process {callerProcess} is not the service {serviceName}.";
        }

        /// <summary>
        /// Indicates whether the caller is a registered application.
        /// </summary>
        public RegisteredAgent? Find(ClaimsIdentity? caller)
        {
            var connection = caller?.FindFirst(PipeClaimTypes.ConnectionId)?.Value;
            return connection != null && agents.TryGetValue(connection, out var agent) ? agent : null;
        }

        /// <summary>
        /// Forgets the application of a pipe connection that ended.
        /// </summary>
        public void Remove(string connectionId)
        {
            if (!agents.TryRemove(connectionId, out var agent))
            {
                return;
            }

            LogEnvironment.LogEvent($"Application gone: {agent}.", LogSeverity.Report);
            try
            {
                AgentLeft?.Invoke(this, agent);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"A listener on the end of application {agent} failed: {ex.OutlineException()}", LogSeverity.Error);
            }
        }

        /// <summary>
        /// Indicates whether a process of the given path runs from the folder of the application.
        /// </summary>
        public bool RunsFromApplicationFolder(string? processPath)
        {
            var folder = Path.GetDirectoryName(options.ApplicationPath);
            if (string.IsNullOrEmpty(processPath) || string.IsNullOrEmpty(folder))
            {
                return false;
            }

            var expected = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(processPath).StartsWith(expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
