namespace ITVComponents.WebCoreToolkit.Extras.AnonymousAssetAccess.Options
{
    public class WebPartOptions
    {
        public string AuthenticationType { get; set; } = AnonymousAssetAuthenticationOptions.DefaultScheme;

        public int MaxAnonymousLinkAge { get; set; } = 365;
    }
}
