using System;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.UpdateService.Abstractions.Contracts;
using ITVComponents.UpdateService.Agents;
using ITVComponents.UpdateService.Sessions;

namespace ITVComponents.UpdateService
{
    /// <summary>
    /// What an application can reach of the service over the pipe - and nothing else: every public member of this class
    /// can be called, so it has exactly the operations of <see cref="IUpdateServiceFacade"/>, each with the caller's
    /// identity as the hub took it from the operating system.
    /// </summary>
    /// <remarks>
    /// It does not implement <see cref="IUpdateServiceFacade"/> on purpose: those overloads would be callable without an
    /// identity. The message server finds the overload with the trailing <see cref="IIdentity"/>.
    /// </remarks>
    public sealed class UpdateServiceFacade
    {
        private readonly UpdateCoordinator coordinator;
        private readonly AgentRegistry registry;
        private readonly RoleCoordinator roles;

        internal UpdateServiceFacade(UpdateCoordinator coordinator, AgentRegistry registry, RoleCoordinator roles)
        {
            this.coordinator = coordinator;
            this.registry = registry;
            this.roles = roles;
            coordinator.StatusChanged += s => StatusChanged?.Invoke(s);
            coordinator.ShutdownRequested += r => ShutdownRequested?.Invoke(r);
            roles.RoleChanged += c => RoleChanged?.Invoke(c);
        }

        /// <summary>
        /// See <see cref="IUpdateServiceFacade.RoleChanged"/>.
        /// </summary>
        public event Action<RoleChange>? RoleChanged;

        /// <summary>
        /// See <see cref="IUpdateServiceFacade.ShutdownRequested"/>.
        /// </summary>
        public event Action<ShutdownRequest>? ShutdownRequested;

        /// <summary>
        /// See <see cref="IUpdateServiceFacade.StatusChanged"/>.
        /// </summary>
        public event Action<UpdateStatus>? StatusChanged;

        /// <summary>
        /// See <see cref="IUpdateServiceFacade.RegisterAsync"/>.
        /// </summary>
        public Task<AgentRegistration> RegisterAsync(string productId, string version, bool currentlyActive, IIdentity caller)
        {
            var agent = registry.Register(caller as ClaimsIdentity, productId, version, currentlyActive, out var reason, out var running);
            if (agent == null)
            {
                LogEnvironment.LogEvent($"{caller?.Name ?? "An unknown caller"} could not register: {reason}", running != null ? LogSeverity.Report : LogSeverity.Warning);
                return Task.FromResult(new AgentRegistration
                {
                    Accepted = false,
                    Reason = reason,
                    AlreadyRunning = running != null,
                    ActiveSessionId = running?.SessionId,
                    ActiveUserName = running?.UserName
                });
            }

            var role = roles.RoleOf(agent);
            var activeInstance = roles.Active;
            return Task.FromResult(new AgentRegistration
            {
                Accepted = true,
                SessionId = agent.SessionId,
                ProcessId = agent.ProcessId,
                Role = role,
                ActiveSessionId = activeInstance != null && activeInstance.ConnectionId != agent.ConnectionId ? activeInstance.SessionId : null,
                ActiveUserName = activeInstance != null && activeInstance.ConnectionId != agent.ConnectionId ? activeInstance.UserName : null,
                Status = coordinator.Status
            });
        }

        /// <summary>
        /// See <see cref="IUpdateServiceFacade.AssignModulesAsync"/>. Only a registered application may assign - and the
        /// registration only admits the application from its installation folder.
        /// </summary>
        public Task AssignModulesAsync(string[] moduleIds, IIdentity caller)
        {
            var agent = registry.Find(caller as ClaimsIdentity);
            if (agent == null)
            {
                LogEnvironment.LogEvent($"{caller?.Name ?? "An unknown caller"} assigned modules without being registered; ignored.", LogSeverity.Warning);
                return Task.CompletedTask;
            }

            coordinator.AssignModules(moduleIds ?? Array.Empty<string>());
            return Task.CompletedTask;
        }

        /// <summary>
        /// See <see cref="IUpdateServiceFacade.ReportModulesLoadedAsync"/>. Only a registered application may report.
        /// </summary>
        public Task ReportModulesLoadedAsync(ModuleLoadReport[] reports, IIdentity caller)
        {
            var agent = registry.Find(caller as ClaimsIdentity);
            if (agent == null)
            {
                LogEnvironment.LogEvent($"{caller?.Name ?? "An unknown caller"} reported modules without being registered; ignored.", LogSeverity.Warning);
                return Task.CompletedTask;
            }

            coordinator.ReportModules(reports ?? Array.Empty<ModuleLoadReport>());
            return Task.CompletedTask;
        }

        /// <summary>
        /// See <see cref="IUpdateServiceFacade.ReportReleasedAsync"/>. Only a registered application may report.
        /// </summary>
        public Task ReportReleasedAsync(IIdentity caller)
        {
            var agent = registry.Find(caller as ClaimsIdentity);
            if (agent == null)
            {
                LogEnvironment.LogEvent($"{caller?.Name ?? "An unknown caller"} reported a release without being registered; ignored.", LogSeverity.Warning);
                return Task.CompletedTask;
            }

            roles.Released(agent);
            return Task.CompletedTask;
        }

        /// <summary>
        /// See <see cref="IUpdateServiceFacade.GetStatusAsync"/>.
        /// </summary>
        public Task<UpdateStatus> GetStatusAsync(IIdentity caller)
        {
            return Task.FromResult(coordinator.Status);
        }

        /// <summary>
        /// See <see cref="IUpdateServiceFacade.CheckNowAsync"/>. Only a registered application may ask.
        /// </summary>
        public Task CheckNowAsync(IIdentity caller)
        {
            var agent = registry.Find(caller as ClaimsIdentity);
            if (agent == null)
            {
                LogEnvironment.LogEvent($"{caller?.Name ?? "An unknown caller"} asked to check now without being registered; ignored.", LogSeverity.Warning);
                return Task.CompletedTask;
            }

            LogEnvironment.LogEvent($"{agent} asks to check for a new version now.", LogSeverity.Report);
            coordinator.CheckNow();
            return Task.CompletedTask;
        }

        /// <summary>
        /// See <see cref="IUpdateServiceFacade.DeferAsync"/>. Only a registered application may ask.
        /// </summary>
        public Task<bool> DeferAsync(int minutes, string reason, IIdentity caller)
        {
            var agent = registry.Find(caller as ClaimsIdentity);
            if (agent == null)
            {
                LogEnvironment.LogEvent($"{caller?.Name ?? "An unknown caller"} asked to defer without being registered; refused.", LogSeverity.Warning);
                return Task.FromResult(false);
            }

            try
            {
                return Task.FromResult(coordinator.Defer(minutes, reason ?? string.Empty, agent));
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"The deferral asked by {agent} could not be recorded: {ex.OutlineException()}", LogSeverity.Error);
                return Task.FromResult(false);
            }
        }
    }
}
