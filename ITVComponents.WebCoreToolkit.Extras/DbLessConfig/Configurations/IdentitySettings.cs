using ITVComponents.WebCoreToolkit.Models;
using Role = ITVComponents.WebCoreToolkit.Extras.DbLessConfig.Models.Role;
using User = ITVComponents.WebCoreToolkit.Extras.DbLessConfig.Models.User;

namespace ITVComponents.WebCoreToolkit.Extras.DbLessConfig.Configurations
{
    public class IdentitySettings
    {
        public const string SettingsKey="ITVenture:Identity";

        public User[] Users { get; set; }

        public Role[] Roles { get; set; }

        public Feature[] Features { get; set; }

        public string[] ExplicitPermissions { get; set; }
        
        public string[] ExplicitPermissionScopes { get; set; }
    }
}
