using Microsoft.AspNetCore.Authentication;

namespace ITVComponents.WebCoreToolkit.Extras.AnonymousAssetAccess
{
    public class AnonymousAssetAuthenticationOptions : AuthenticationSchemeOptions
    {
        public const string DefaultScheme = "Shared-Asset-Key";
        public string Scheme => DefaultScheme;
        public string AuthenticationType { get; set; } = DefaultScheme;
    }
}
