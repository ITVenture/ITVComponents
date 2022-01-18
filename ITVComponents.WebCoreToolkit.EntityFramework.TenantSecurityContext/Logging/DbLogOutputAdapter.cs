using System.Collections.Generic;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Options.Logging;
using ITVComponents.WebCoreToolkit.Logging;
using ITVComponents.WebCoreToolkit.Models;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Logging
{
    internal class DbLogOutputAdapter:ILogOutputAdapter
    {
        private readonly SecurityContext db;

        public DbLogOutputAdapter(SecurityContext db)
        {
            this.db = db;
        }

        /// <summary>
        /// Populates a collected event to the target of this adapter
        /// </summary>
        /// <param name="eventData">the collected event-data record</param>
        public void PopulateEvent(SystemEvent eventData)
        {
            db.SystemLog.Add(eventData.ToViewModel<SystemEvent, Models.SystemEvent>());
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
