using System.Globalization;
using System.Resources;
using Microsoft.AspNetCore.Localization;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreTenantSecurityUserView.Resources
{
    public static class TextsAndMessagesHelper
    {

        private static ResourceManager resourceMan;

        /// <summary>
        ///   Gibt die zwischengespeicherte ResourceManager-Instanz zurück, die von dieser Klasse verwendet wird.
        /// </summary>
        private static ResourceManager ResourceManager => resourceMan ??= new ResourceManager("ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreTenantSecurityUserView.Resources.TextsAndMessages", typeof(TextsAndMessagesHelper).Assembly);

        private static string GetString(string name, CultureInfo culture = null)
        {
            culture ??= CultureInfo.CurrentUICulture;
            var mgr = ResourceManager;
            return mgr.GetString(name, culture);
        }

        public static string IWCAU_Titles_LogIns => GetString(nameof(IWCAU_Titles_LogIns));

        public static string IWCAU_Titles_Tokens => GetString(nameof(IWCAU_Titles_Tokens));

        public static string IWCAU_Titles_Claims => GetString(nameof(IWCAU_Titles_Claims));
        //--


        public static string GetIWCAU_Titles_LogIns(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCAU_Titles_LogIns), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCAU_Titles_Tokens(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCAU_Titles_Tokens), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCAU_Titles_Claims(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCAU_Titles_Claims), requestCulture.RequestCulture.UICulture);
        }
    }
}
