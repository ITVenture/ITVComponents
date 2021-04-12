using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Helpers
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
