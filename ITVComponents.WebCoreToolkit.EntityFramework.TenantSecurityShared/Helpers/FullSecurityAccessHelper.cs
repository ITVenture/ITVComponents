using System;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers
{
    public sealed class FullSecurityAccessHelper:IDisposable
    {
        private readonly IBaseTenantContext db;
        public FullSecurityAccessHelper ForwardHelper { get; set; }

        public bool HideGlobals{ get; set; }
        public bool ShowAllTenants{ get; set; }

        public bool CreatedWithContext { get; }

        public FullSecurityAccessHelper()
        {
        }

        internal FullSecurityAccessHelper(IBaseTenantContext db, bool allTenants, bool hideGlobals)
        {
            this.db = db;
            HideGlobals = hideGlobals;
            ShowAllTenants = allTenants;
            CreatedWithContext = true;
            db.RegisterSecurityRollback(this);
        }
        public void Dispose()
        {
            db.RollbackSecurity(this);
        }
            
    }
}
