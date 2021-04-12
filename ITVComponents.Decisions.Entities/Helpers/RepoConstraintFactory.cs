using ITVComponents.EFRepo;
using ITVComponents.Plugins;
using ITVComponents.Threading;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.Decisions.Entities.Helpers
{
    /// <summary>
    /// Provides a Repository-driven Constraint Factory
    /// </summary>
    public class RepoConstraintFactory:ConstraintFactory
    {
        private IContextBuffer contextProvider;

        /// <summary>
        /// Initializes a new instance of the RepoConstraintFactory class
        /// </summary>
        /// <param name="contextProvider">the contextprovider object that enables this Factory to access the db using EntityFramework</param>
        /// <param name="factory">the pluginFactory that will provide custom plugins that are required in order to execute some constraints</param>
        public RepoConstraintFactory(IContextBuffer contextProvider, PluginFactory factory) : base(factory)
        {
            this.contextProvider = contextProvider;
            RefreshConstraints();
            BackgroundRunner.AddPeriodicTask(RefreshConstraints, 60000);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public override void Dispose()
        {
            base.Dispose();
            BackgroundRunner.RemovePeriodicTask(RefreshConstraints);
        }

        /// <summary>
        /// Refreshes the constraints that are registered in this Factory
        /// </summary>
        private void RefreshConstraints()
        {
            DbContext repo;
            using (contextProvider.AcquireContext(out repo))
            {
                RepoConstraintsInitializer.InitializeConstraints(this, this.Factory, repo);
            }
        }
    }
}
