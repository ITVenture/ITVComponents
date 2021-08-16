using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Settings;
using ITVComponents.Settings.Native;

namespace ITVComponents.InterProcessCommunication.Grpc
{
    public class GrpcHubConfiguration : JsonSettingsSection
    {
        private static GrpcHubConfiguration Instance => GetSection<GrpcHubConfiguration>("ITV_IPC_GRPC_Hub");
        public bool UseExtConfig { get; set; } = false;

        public bool TrustAllCertificates { get; set; }

        public List<string> TrustedCertificates { get; set; } = new List<string>();

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
            private static GrpcHubSettings native = NativeSettings.GetSection<GrpcHubSettings>("ITVenture:InterProcessCommunication:Grpc:HubConfig", d => { d.TrustAllCertificates = true; });

            public static bool TrustAllCertificates => Instance.UseExtConfig ? Instance.TrustAllCertificates : native.TrustAllCertificates;

            public static List<string> TrustedCertificates => Instance.UseExtConfig ? Instance.TrustedCertificates : native.TrustedCertificates;
        }
    }
}