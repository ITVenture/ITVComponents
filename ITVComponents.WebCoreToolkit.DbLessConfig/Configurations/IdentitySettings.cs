using ITVComponents.WebCoreToolkit.Models;
using Role = ITVComponents.WebCoreToolkit.DbLessConfig.Models.Role;
using User = ITVComponents.WebCoreToolkit.DbLessConfig.Models.User;

namespace ITVComponents.WebCoreToolkit.DbLessConfig.Configurations
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
