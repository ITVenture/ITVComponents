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

        /// <inheritdoc />
        public override string ToString() => $"{UserName} (session {SessionId}, process {ProcessId}, version {Version})";
    }

    /// <summary>
    /// The applications connected right now. An application counts from its registration until its pipe connection ends;
    /// everything about it (user, session, process, path) comes from the operating system.
    /// </summary>
    public sealed class AgentRegistry
    {
        private readonly UpdateServiceOptions options;
        private readonly ConcurrentDictionary<string, RegisteredAgent> agents = new ConcurrentDictionary<string, RegisteredAgent>(StringComparer.Ordinal);

        public AgentRegistry(UpdateServiceOptions options)
        {
            this.options = options;
        }

        /// <summary>
        /// Is raised when a registered application is gone.
        /// </summary>
        public event EventHandler<RegisteredAgent>? AgentLeft;

        /// <summary>
        /// Gets the registered applications.
        /// </summary>
        public IReadOnlyList<RegisteredAgent> Active => agents.Values.ToArray();

        /// <summary>
        /// Registers the caller as an application.
        /// </summary>
        /// <param name="caller">the identity the pipe hub read from the operating system</param>
        /// <param name="productId">the product the caller claims to belong to</param>
        /// <param name="version">the version it claims to have (shown only, never trusted for a decision)</param>
        /// <param name="reason">why the registration was refused</param>
        /// <returns>the registration, or null when refused</returns>
        public RegisteredAgent? Register(ClaimsIdentity? caller, string productId, string version, out string? reason)
        {
            reason = null;
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

            var agent = new RegisteredAgent
            {
                ConnectionId = caller.FindFirst(PipeClaimTypes.ConnectionId)?.Value ?? string.Empty,
                UserName = caller.Name ?? string.Empty,
                SessionId = int.Parse(caller.FindFirst(PipeClaimTypes.SessionId)?.Value ?? "-1", CultureInfo.InvariantCulture),
                ProcessId = int.Parse(caller.FindFirst(PipeClaimTypes.ProcessId)?.Value ?? "0", CultureInfo.InvariantCulture),
                ProcessPath = processPath,
                Version = version,
                RegisteredUtc = DateTimeOffset.UtcNow
            };
            agents[agent.ConnectionId] = agent;
            LogEnvironment.LogEvent($"Application registered: {agent}.", LogSeverity.Report);
            return agent;
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

        private bool RunsFromApplicationFolder(string? processPath)
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
