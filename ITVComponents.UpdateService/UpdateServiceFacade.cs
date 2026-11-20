using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.NamedPipe.Security;
using ITVComponents.Logging;
using ITVComponents.UpdateService.Abstractions.Contracts;
using ITVComponents.UpdateService.Agents;
using ITVComponents.UpdateService.Modules;
using ITVComponents.UpdateService.Sessions;

namespace ITVComponents.UpdateService
{
    /// <summary>
    /// What an application can reach of the service over the pipe - and nothing else: every public member of this class
    /// can be called, so it has exactly the operations of <see cref="IUpdateServiceFacade"/>, each with the caller's
    /// identity as the hub took it from the operating system.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It does not implement <see cref="IUpdateServiceFacade"/> on purpose: those overloads would be callable without an
    /// identity. The message server finds the overload with the trailing <see cref="IIdentity"/>.
    /// </para>
    /// <para>
    /// With several targets (issue ModulePlugins-Instances, D), the registration decides the target of an application from
    /// its product and folder; every later call goes to that target only. An application can never reach the modules or
    /// instances of another target. The events go to every application on the pipe and carry the target; the client
    /// ignores those of another one.
    /// </para>
    /// </remarks>
    public sealed class UpdateServiceFacade
    {
        private readonly IReadOnlyList<UpdateTarget> targets;

        internal UpdateServiceFacade(UpdateCoordinator coordinator, AgentRegistry registry, RoleCoordinator roles, ModuleManager? modules = null)
            : this(new[] { new UpdateTarget(new UpdateServiceOptions { ProductId = registry.ProductId, TargetId = registry.TargetId }, coordinator, registry, roles, modules) })
        {
        }

        internal UpdateServiceFacade(IReadOnlyList<UpdateTarget> targets)
        {
            if (targets.Count == 0)
            {
                throw new ArgumentException("The update service needs at least one target.", nameof(targets));
            }

            this.targets = targets;
            foreach (var target in targets)
            {
                var id = target.Id;
                target.Coordinator.StatusChanged += s => StatusChanged?.Invoke(s);
                target.Coordinator.ShutdownRequested += r => ShutdownRequested?.Invoke(r);
                if (target.Roles != null)
                {
                    target.Roles.RoleChanged += c =>
                    {
                        c.TargetId = id;
                        RoleChanged?.Invoke(c);
                    };
                }
            }
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
            var identity = caller as ClaimsIdentity;
            var target = SelectTarget(identity, productId);
            if (target == null)
            {
                var reason = $"This service keeps {string.Join(", ", targets.Select(t => t.Options.ProductId).Distinct(StringComparer.OrdinalIgnoreCase))} up to date, not {productId}.";
                LogEnvironment.LogEvent($"{caller?.Name ?? "An unknown caller"} could not register: {reason}", LogSeverity.Warning);
                return Task.FromResult(new AgentRegistration { Accepted = false, Reason = reason });
            }

            var agent = target.Registry.Register(identity, productId, version, currentlyActive, out var refusal, out var running);
            if (agent == null)
            {
                LogEnvironment.LogEvent($"{caller?.Name ?? "An unknown caller"} could not register: {refusal}", running != null ? LogSeverity.Report : LogSeverity.Warning);
                return Task.FromResult(new AgentRegistration
                {
                    Accepted = false,
                    Reason = refusal,
                    AlreadyRunning = running != null,
                    ActiveSessionId = running?.SessionId,
                    ActiveUserName = running?.UserName,
                    TargetId = target.Id
                });
            }

            // A Windows service has one instance; it is always active.
            var role = target.Roles?.RoleOf(agent) ?? AgentRole.Active;
            var activeInstance = target.Roles?.Active;
            return Task.FromResult(new AgentRegistration
            {
                Accepted = true,
                SessionId = agent.SessionId,
                ProcessId = agent.ProcessId,
                Role = role,
                ActiveSessionId = activeInstance != null && activeInstance.ConnectionId != agent.ConnectionId ? activeInstance.SessionId : null,
                ActiveUserName = activeInstance != null && activeInstance.ConnectionId != agent.ConnectionId ? activeInstance.UserName : null,
                Status = target.Coordinator.Status,
                TargetId = target.Id
            });
        }

        /// <summary>
        /// See <see cref="IUpdateServiceFacade.AssignModulesAsync"/>. Only a registered application may assign - and the
        /// registration only admits the application from its installation folder.
        /// </summary>
        public Task AssignModulesAsync(string[] moduleIds, IIdentity caller)
        {
            var (target, agent) = Find(caller);
            if (target == null || agent == null)
            {
                LogEnvironment.LogEvent($"{caller?.Name ?? "An unknown caller"} assigned modules without being registered; ignored.", LogSeverity.Warning);
                return Task.CompletedTask;
            }

            target.Coordinator.AssignModules(moduleIds ?? Array.Empty<string>());
            return Task.CompletedTask;
        }

        /// <summary>
        /// See <see cref="IUpdateServiceFacade.ReportModulesLoadedAsync"/>. Only a registered application may report.
        /// </summary>
        public Task ReportModulesLoadedAsync(ModuleLoadReport[] reports, IIdentity caller)
        {
            var (target, agent) = Find(caller);
            if (target == null || agent == null)
            {
                LogEnvironment.LogEvent($"{caller?.Name ?? "An unknown caller"} reported modules without being registered; ignored.", LogSeverity.Warning);
                return Task.CompletedTask;
            }

            target.Coordinator.ReportModules(reports ?? Array.Empty<ModuleLoadReport>());
            return Task.CompletedTask;
        }

        /// <summary>
        /// See <see cref="IUpdateServiceFacade.CreatePluginInstanceAsync"/>. Only a registered application may create - the
        /// same check as for <see cref="AssignModulesAsync"/>.
        /// </summary>
        public Task<PluginInstanceResult> CreatePluginInstanceAsync(string moduleId, string typeName, string name, IIdentity caller)
        {
            return ChangePluginInstance(caller, $"create plugin instance {name} of {moduleId}/{typeName}", m => m.CreatePluginInstance(moduleId ?? string.Empty, typeName ?? string.Empty, name ?? string.Empty));
        }

        /// <summary>
        /// See <see cref="IUpdateServiceFacade.RemovePluginInstanceAsync"/>. Only a registered application may remove.
        /// </summary>
        public Task<PluginInstanceResult> RemovePluginInstanceAsync(string name, IIdentity caller)
        {
            return ChangePluginInstance(caller, $"remove plugin instance {name}", m => m.RemovePluginInstance(name ?? string.Empty));
        }

        /// <summary>
        /// See <see cref="IUpdateServiceFacade.ReportReleasedAsync"/>. Only a registered application may report.
        /// </summary>
        public Task ReportReleasedAsync(IIdentity caller)
        {
            var (target, agent) = Find(caller);
            if (target == null || agent == null)
            {
                LogEnvironment.LogEvent($"{caller?.Name ?? "An unknown caller"} reported a release without being registered; ignored.", LogSeverity.Warning);
                return Task.CompletedTask;
            }

            if (target.Roles == null)
            {
                LogEnvironment.LogDebugEvent($"{agent} reported a release, but {target} has no roles; nothing to do.", LogSeverity.Report);
                return Task.CompletedTask;
            }

            target.Roles.Released(agent);
            return Task.CompletedTask;
        }

        /// <summary>
        /// See <see cref="IUpdateServiceFacade.GetStatusAsync"/>. A registered application gets the state of its target; with
        /// one target, anybody gets its state, as before.
        /// </summary>
        public Task<UpdateStatus> GetStatusAsync(IIdentity caller)
        {
            var (target, _) = Find(caller);
            if (target == null && targets.Count == 1)
            {
                target = targets[0];
            }

            if (target == null)
            {
                LogEnvironment.LogDebugEvent($"{caller?.Name ?? "An unknown caller"} asked for the state without being registered; with {targets.Count} targets it is not known which one.", LogSeverity.Report);
                return Task.FromResult(new UpdateStatus { Phase = UpdatePhase.Unknown });
            }

            return Task.FromResult(target.Coordinator.Status);
        }

        /// <summary>
        /// See <see cref="IUpdateServiceFacade.CheckNowAsync"/>. Only a registered application may ask.
        /// </summary>
        public Task CheckNowAsync(IIdentity caller)
        {
            var (target, agent) = Find(caller);
            if (target == null || agent == null)
            {
                LogEnvironment.LogEvent($"{caller?.Name ?? "An unknown caller"} asked to check now without being registered; ignored.", LogSeverity.Warning);
                return Task.CompletedTask;
            }

            LogEnvironment.LogEvent($"{agent} asks to check for a new version now.", LogSeverity.Report);
            target.Coordinator.CheckNow();
            return Task.CompletedTask;
        }

        /// <summary>
        /// See <see cref="IUpdateServiceFacade.DeferAsync"/>. Only a registered application may ask.
        /// </summary>
        public Task<bool> DeferAsync(int minutes, string reason, IIdentity caller)
        {
            var (target, agent) = Find(caller);
            if (target == null || agent == null)
            {
                LogEnvironment.LogEvent($"{caller?.Name ?? "An unknown caller"} asked to defer without being registered; refused.", LogSeverity.Warning);
                return Task.FromResult(false);
            }

            try
            {
                return Task.FromResult(target.Coordinator.Defer(minutes, reason ?? string.Empty, agent));
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"The deferral asked by {agent} could not be recorded: {ex.OutlineException()}", LogSeverity.Error);
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Picks the target an application registers with: by product, and among several targets of the same product by
        /// the folder it runs from. Null when no target keeps the product.
        /// </summary>
        private UpdateTarget? SelectTarget(ClaimsIdentity? caller, string productId)
        {
            if (targets.Count == 1)
            {
                // The registry gives the reasons, as before.
                return targets[0];
            }

            var candidates = targets.Where(t => string.Equals(t.Options.ProductId, productId, StringComparison.OrdinalIgnoreCase)).ToList();
            if (candidates.Count <= 1)
            {
                return candidates.FirstOrDefault();
            }

            var processPath = caller?.FindFirst(PipeClaimTypes.ProcessPath)?.Value;
            return candidates.FirstOrDefault(t => t.Registry.RunsFromApplicationFolder(processPath))
                   ?? candidates.FirstOrDefault(t => t.Registry.Admits(processPath))
                   ?? candidates[0];
        }

        private (UpdateTarget? Target, RegisteredAgent? Agent) Find(IIdentity? caller)
        {
            var identity = caller as ClaimsIdentity;
            foreach (var target in targets)
            {
                var agent = target.Registry.Find(identity);
                if (agent != null)
                {
                    return (target, agent);
                }
            }

            return (null, null);
        }

        private Task<PluginInstanceResult> ChangePluginInstance(IIdentity caller, string what, Func<ModuleManager, string?> change)
        {
            var (target, agent) = Find(caller);
            if (target == null || agent == null)
            {
                LogEnvironment.LogEvent($"{caller?.Name ?? "An unknown caller"} asked to {what} without being registered; refused.", LogSeverity.Warning);
                return Task.FromResult(new PluginInstanceResult { Reason = "the application is not registered at the update service" });
            }

            if (target.Modules == null)
            {
                LogEnvironment.LogEvent($"{agent} asked to {what}, but this update service keeps no modules; refused.", LogSeverity.Warning);
                return Task.FromResult(new PluginInstanceResult { Reason = "this update service keeps no modules" });
            }

            try
            {
                var refusal = change(target.Modules);
                if (refusal != null)
                {
                    LogEnvironment.LogEvent($"{agent} asked to {what}; refused: {refusal}", LogSeverity.Warning);
                    return Task.FromResult(new PluginInstanceResult { Reason = refusal });
                }

                return Task.FromResult(new PluginInstanceResult { Success = true });
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"{agent} asked to {what}; it failed: {ex.OutlineException()}", LogSeverity.Error);
                return Task.FromResult(new PluginInstanceResult { Reason = $"the update service failed: {ex.Message}" });
            }
        }
    }
}
