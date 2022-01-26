using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.Logging;
using ITVComponents.WebCoreToolkit.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Logging
{
    internal class DbLogOutputAdapter:ILogOutputAdapter
    {
        private readonly IBaseTenantContext db;

        public DbLogOutputAdapter(IBaseTenantContext db)
        {
            this.db = db;
        }

        /// <summary>
        /// Populates a collected event to the target of this adapter
        /// </summary>
        /// <param name="eventData">the collected event-data record</param>
        public void PopulateEvent(SystemEvent eventData)
        {
            db.SystemLog.Add(eventData.ToViewModel<SystemEvent, TenantSecurityShared.Models.SystemEvent>());
        }

        /// <summary>
        /// Saves all populated changes to the target
        /// </summary>
        public void Flush()
        {
            db.SaveChanges();
        }
    }
}
