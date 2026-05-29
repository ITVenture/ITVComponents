using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Interfaces;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models
{

    public class BaseTenantContextSecurityTrustConfig : BaseTenantContextSecurityTrustConfig<BaseTenantContextSecurityTrustConfig>
    {
        /*protected override BaseTenantContextSecurityTrustConfig Clone(BaseTenantContextSecurityTrustConfig other)
        {
            return new BaseTenantContextSecurityTrustConfig
            {
                HideGlobals = HideGlobals,
                ShowAllTenants = ShowAllTenants,
                SpecialFilterSettings = new Dictionary<string, bool>(SpecialFilterSettings)
            };
        }*/
    }
    public class BaseTenantContextSecurityTrustConfig<TTrustConfig>:ITrustConfig<TTrustConfig> 
        where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
    {
        public bool HideGlobals { get; set; }

        public bool ShowAllTenants { get; set; }

        public IDictionary<string, bool> SpecialFilterSettings { get; set; } = new Dictionary<string, bool>();
        TTrustConfig ITrustConfig<TTrustConfig>.Clone(TTrustConfig other)
        {
            return Clone(other);
        }

        protected virtual TTrustConfig Clone(TTrustConfig other)
        {
            var retVal = new TTrustConfig { HideGlobals = HideGlobals, ShowAllTenants = ShowAllTenants && other.ShowAllTenants };
            if (SpecialFilterSettings != null)
            {
                retVal.SpecialFilterSettings =
                    new Dictionary<string, bool>(
                        SpecialFilterSettings.Select(n => new KeyValuePair<string, bool>(n.Key, n.Value)));
            }

            return retVal;
        }
    }
}
