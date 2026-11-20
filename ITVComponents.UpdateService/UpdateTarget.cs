using ITVComponents.UpdateService.Agents;
using ITVComponents.UpdateService.Modules;
using ITVComponents.UpdateService.Sessions;

namespace ITVComponents.UpdateService
{
    /// <summary>
    /// One application the service keeps up to date, with everything that belongs to it alone: its registered instances,
    /// its course of updates, its roles across sessions and its modules (issue ModulePlugins-Instances, D).
    /// </summary>
    internal sealed class UpdateTarget
    {
        public UpdateTarget(UpdateServiceOptions options, UpdateCoordinator coordinator, AgentRegistry registry, RoleCoordinator roles, ModuleManager? modules)
        {
            Options = options;
            Coordinator = coordinator;
            Registry = registry;
            Roles = roles;
            Modules = modules;
        }

        /// <summary>Gets the id of the target.</summary>
        public string Id => Options.TargetId;

        /// <summary>Gets the options resolved for this target.</summary>
        public UpdateServiceOptions Options { get; }

        /// <summary>Gets the course of updates.</summary>
        public UpdateCoordinator Coordinator { get; }

        /// <summary>Gets the registered instances.</summary>
        public AgentRegistry Registry { get; }

        /// <summary>Gets the roles across sessions.</summary>
        public RoleCoordinator Roles { get; }

        /// <summary>Gets the modules, or null when the service keeps none.</summary>
        public ModuleManager? Modules { get; }

        /// <inheritdoc />
        public override string ToString() => $"target {Id} ({Options.ProductId})";
    }
}
