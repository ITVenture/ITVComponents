using ITVComponents.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.InterProcessExtensions.JwtAuth.Config;

namespace ITVComponents.WebCoreToolkit.InterProcessExtensions.JwtAuth
{
    public class JwtAuthenticationSection : JsonSettingsSection
    {
        public bool UseExtConfig { get; set; } = false;

        public JwtAuthConfigCollection JwtAuthSchemes { get; set; } = new();

        public static JwtAuthenticationSection Instance => GetSection<JwtAuthenticationSection>("ITV_IPC_GRPC_HubAuthJwt");

        public static class Helper
        {
            private static JwtAuthenticationSettings native =
                NativeSettings.GetSection<JwtAuthenticationSettings>("ITVenture:InterProcessCommunication:Grpc:HubAuthJwtConfig");

            public static JwtAuthConfigCollection JwtAuthSchemes =>
                Instance.UseExtConfig ? Instance.JwtAuthSchemes : native.JwtAuthSchemes;
        }
    }
}
