using System;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Helpers
{
    internal class FullSecurityAccessHelper:IDisposable
    {
        private SecurityContext db;
        private bool hideGlobals;
        private bool showAllTenants;

        public FullSecurityAccessHelper(SecurityContext db, bool allTenants, bool hideGlobals)
        {
            this.db = db;
            this.hideGlobals = db.HideGlobals;
            this.showAllTenants = db.ShowAllTenants;
            db.HideGlobals = hideGlobals;
            db.ShowAllTenants = allTenants;
        }
        public void Dispose()
        {
            db.HideGlobals = hideGlobals;
            db.ShowAllTenants = showAllTenants;
        }
            
    }
}
