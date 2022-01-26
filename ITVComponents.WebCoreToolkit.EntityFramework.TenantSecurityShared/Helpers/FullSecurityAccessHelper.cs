using System;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers
{
    public class FullSecurityAccessHelper:IDisposable
    {
        private IBaseTenantContext db;
        private bool hideGlobals;
        private bool showAllTenants;

        public FullSecurityAccessHelper(IBaseTenantContext db, bool allTenants, bool hideGlobals)
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
