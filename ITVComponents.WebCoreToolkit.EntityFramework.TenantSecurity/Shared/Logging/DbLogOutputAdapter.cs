using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.Logging;
using ITVComponents.WebCoreToolkit.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Logging
{
    internal class DbLogOutputAdapter:ILogOutputAdapter
    {
        private readonly IToolkitContextFactory contextFactory;

        /// <summary>
        /// Lease held for the duration of one populate*→flush cycle. The added events and the subsequent SaveChanges
        /// MUST run on the same context instance, so a single fresh per-operation context is leased lazily on the first
        /// <see cref="PopulateEvent"/> and disposed in <see cref="Flush"/>.
        /// </summary>
        private IContextLease<ICoreSystemContext> lease;

        public DbLogOutputAdapter(IToolkitContextFactory contextFactory)
        {
            this.contextFactory = contextFactory;
        }

        /// <summary>
        /// Populates a collected event to the target of this adapter
        /// </summary>
        /// <param name="eventData">the collected event-data record</param>
        public void PopulateEvent(SystemEvent eventData)
        {
            lease ??= contextFactory.Lease<ICoreSystemContext>();
            var db = lease.Context;
            db.SystemLog.Add(eventData.ToViewModel<SystemEvent, Shared.Models.SystemEvent>());
        }

        /// <summary>
        /// Saves all populated changes to the target
        /// </summary>
        public void Flush()
        {
            if (lease == null)
            {
                return;
            }

            try
            {
                var db = lease.Context;
                db.SaveChanges();
            }
            finally
            {
                lease.Dispose();
                lease = null;
            }
        }
    }
}
