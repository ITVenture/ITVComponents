using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Grpc.Config;
using ITVComponents.Settings;
using ITVComponents.Settings.Native;

namespace ITVComponents.InterProcessCommunication.Grpc
{
    public class HubConfiguration:JsonSettingsSection
    {
        private static HubConfiguration Instance => GetSection<HubConfiguration>("ITV_IPC_GRPC_Hub");

        public bool UseExtConfig { get; set; } = false;

        public bool TrustAllCertificates { get;set; }

        public List<string> TrustedCertificates { get; set; } = new List<string>();

        public List<HubUser> HubUsers { get; set; }= new List<HubUser>();

        public List<HubRole> HubRoles{get;set;} = new List<HubRole>();

        public List<string> KnownHubPermissions { get; set; }


        /// <summary>
        /// Offers a derived class to define default-configuration-settings
        /// </summary>
        protected override void LoadDefaults()
        {
            base.LoadDefaults();
            TrustAllCertificates = true;
        }

        public static class Helper
        {
            private static HubSettings native = NativeSettings.GetSection<HubSettings>("ITVenture:InterProcessCommunication:Grpc:HubConfig", d =>
            {
                d.TrustAllCertificates = true;
            });

            public static bool TrustAllCertificates => Instance.UseExtConfig ? Instance.TrustAllCertificates : native.TrustAllCertificates;

            public static List<string> TrustedCertificates => Instance.UseExtConfig ? Instance.TrustedCertificates : native.TrustedCertificates;

            public static List<HubUser> HubUsers => Instance.UseExtConfig ? Instance.HubUsers : native.HubUsers;

            public static List<HubRole> HubRoles => Instance.UseExtConfig ? Instance.HubRoles : native.HubRoles;

            public static List<string> KnownHubPermissions => Instance.UseExtConfig ? Instance.KnownHubPermissions : native.KnownHubPermissions;
        }
    }
}
