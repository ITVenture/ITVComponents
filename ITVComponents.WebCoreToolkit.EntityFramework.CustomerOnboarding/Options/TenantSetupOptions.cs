using ITVComponents.WebCoreToolkit.Configuration;

namespace ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding.Options
{
    [SettingName("TenantSetup")]
    public class TenantSetupOptions
    {
        public string BasicTenantTemplate { get; set; }

        public string AdminUserRole { get; set; }

        public string SubscriptionAssetKey { get; set; }
    }
}
