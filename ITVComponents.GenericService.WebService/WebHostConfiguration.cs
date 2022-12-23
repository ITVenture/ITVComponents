using System.Collections.Generic;
using ITVComponents.Settings;
using ITVComponents.Settings.Native;

namespace ITVComponents.GenericService.WebService
{
    public class WebHostConfiguration : JsonSettingsSection
    {
        private static WebHostConfiguration Instance => GetSection<WebHostConfiguration>("ITV_SVC_Host");
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
            private static WebHostSettings native = NativeSettings.GetSection<WebHostSettings>("ITVenture:ServiceConfiguration:WebHostConfig", d => { d.TrustAllCertificates = true; });

            public static bool TrustAllCertificates => Instance.UseExtConfig ? Instance.TrustAllCertificates : native.TrustAllCertificates;

            public static List<string> TrustedCertificates => Instance.UseExtConfig ? Instance.TrustedCertificates : native.TrustedCertificates;
        }
    }
}