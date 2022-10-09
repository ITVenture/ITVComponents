using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.SettingsExtensions;

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

        public bool UseSharedAssets { get; set; }

        public bool UseHealthChecks { get; set; }

        [AutoResolveChildren]
        public List<HealthCheckDefinition> HealthChecks { get; set; } = new();

        public bool UseApplicationTokens { get; set; }
    }
}
