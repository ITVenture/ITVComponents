using System.Collections.Generic;
using ITVComponents.GenericService.ServiceSecurity.Config;
using ITVComponents.Settings;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.Models;

namespace ITVComponents.GenericService.ServiceSecurity
{
    public class HostConfiguration:JsonSettingsSection
    {
        private static HostConfiguration Instance => GetSection<HostConfiguration>("ITV_SVC_Host_Security");

        public bool UseExtConfig { get; set; } = false;

        public List<HostUser> HostUsers { get; set; }= new List<HostUser>();

        public List<HostRole> HostRoles{get;set;} = new List<HostRole>();

        public List<string> KnownHostPermissions { get; set; }

        public List<Feature> Features { get; set; } = new List<Feature>();


        /// <summary>
        /// Offers a derived class to define default-configuration-settings
        /// </summary>
        protected override void LoadDefaults()
        {
            base.LoadDefaults();
        }

        public static class Helper
        {
            private static HostSettings native = NativeSettings.GetSection<HostSettings>("ITVenture:ServiceConfiguration:Security");

            public static List<HostUser> HostUsers => Instance.UseExtConfig ? Instance.HostUsers : native.HostUsers;

            public static List<HostRole> HostRoles => Instance.UseExtConfig ? Instance.HostRoles : native.HostRoles;

            public static List<string> KnownHostPermissions => Instance.UseExtConfig ? Instance.KnownHostPermissions : native.KnownHostPermissions;

            public static List<Feature> Features => Instance.UseExtConfig ? Instance.Features : native.Features;
        }
    }
}
