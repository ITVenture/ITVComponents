using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Options
{
    public class ActivationOptions
    {
        public bool ActivateDbContext { get; set; }

        public string ConnectionStringName { get; set; }

        public bool UseNavigation { get; set; }


        public bool UsePlugins { get; set; }
        
        public bool UseLogAdapter { get; set; }

        public bool UseGlobalSettings { get; set; }

        public bool UseTenantSettings { get; set; }
    }
}
