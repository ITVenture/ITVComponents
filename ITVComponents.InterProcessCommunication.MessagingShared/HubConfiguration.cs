using System.Collections.Generic;
using ITVComponents.InterProcessCommunication.MessagingShared.Config;
using ITVComponents.Settings;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.Models;

namespace ITVComponents.InterProcessCommunication.MessagingShared
{
    public class HubConfiguration:JsonSettingsSection
    {
        private static HubConfiguration Instance => GetSection<HubConfiguration>("ITV_IPC_MSG_Hub");

        public bool UseExtConfig { get; set; } = false;

        public List<HubUser> HubUsers { get; set; }= new List<HubUser>();

        public List<HubRole> HubRoles{get;set;} = new List<HubRole>();

        public List<string> KnownHubPermissions { get; set; }

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
            private static HubSettings native = NativeSettings.GetSection<HubSettings>("ITVenture:InterProcessCommunication:Messaging:HubConfig");

            public static List<HubUser> HubUsers => Instance.UseExtConfig ? Instance.HubUsers : native.HubUsers;

            public static List<HubRole> HubRoles => Instance.UseExtConfig ? Instance.HubRoles : native.HubRoles;

            public static List<string> KnownHubPermissions => Instance.UseExtConfig ? Instance.KnownHubPermissions : native.KnownHubPermissions;

            public static List<Feature> Features => Instance.UseExtConfig ? Instance.Features : native.Features;
        }
    }
}
