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
        private DbContext context;

        /// <summary>
        /// Initializes a new instance of the RepoConstraintFactory class
        /// </summary>
        /// <param name="contextProvider">the contextprovider object that enables this Factory to access the db using EntityFramework</param>
        /// <param name="factory">the pluginFactory that will provide custom plugins that are required in order to execute some constraints</param>
        public RepoConstraintFactory(DbContext context, IPluginFactory factory) : base(factory)
        {
            this.context = context;
            RefreshConstraints();
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
            RepoConstraintsInitializer.InitializeConstraints(this, this.Factory, context);
        }
    }
}
