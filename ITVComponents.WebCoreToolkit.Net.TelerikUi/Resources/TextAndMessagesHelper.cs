using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Localization;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Resources
{
    public static class TextsAndMessagesHelper
    {

        private static ResourceManager resourceMan;

        /// <summary>
        ///   Gibt die zwischengespeicherte ResourceManager-Instanz zurück, die von dieser Klasse verwendet wird.
        /// </summary>
        private static ResourceManager ResourceManager => resourceMan ??= new ResourceManager("ITVComponents.WebCoreToolkit.Net.TelerikUi.Resources.TextsAndMessages", typeof(TextsAndMessagesHelper).Assembly);

        private static string GetString(string name, CultureInfo culture = null)
        {
            culture ??= CultureInfo.CurrentUICulture;
            var mgr = ResourceManager;
            return mgr.GetString(name, culture);
        }
       
        public static string IWCN_KX_PCD_Caption => GetString(nameof(IWCN_KX_PCD_Caption));

        public static string IWCN_KX_RTB_Caption => GetString(nameof(IWCN_KX_RTB_Caption));

        public static string IWCN_Titles_Assemblies => GetString(nameof(IWCN_Titles_Assemblies));

        public static string IWCN_JCP_Title_Entity => GetString(nameof(IWCN_JCP_Title_Entity));

        public static string IWCN_JCP_Title_Action => GetString(nameof(IWCN_JCP_Title_Action));
        public static string IWCN_JCP_Title_Execute => GetString(nameof(IWCN_JCP_Title_Execute));

        public static string IWCN_JCP_Title_Identifyer => GetString(nameof(IWCN_JCP_Title_Identifyer));

        public static string IWCN_JCP_Title_Details => GetString(nameof(IWCN_JCP_Title_Details));


        public static string GetIWCN_KX_PCD_Caption(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_KX_PCD_Caption), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_KX_RTB_Caption(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_KX_RTB_Caption), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_Titles_Assemblies(this IRequestCultureFeature requestCulture)
        {
            return GetString(nameof(IWCN_Titles_Assemblies), requestCulture.RequestCulture.UICulture);
        }

        public static string GetIWCN_JCP_Title_Entity(this IRequestCultureFeature requestCulture) => GetString(nameof(IWCN_JCP_Title_Entity), requestCulture.RequestCulture.UICulture);

        public static string GetIWCN_JCP_Title_Action(this IRequestCultureFeature requestCulture) => GetString(nameof(IWCN_JCP_Title_Action), requestCulture.RequestCulture.UICulture);
        public static string GetIWCN_JCP_Title_Execute(this IRequestCultureFeature requestCulture) => GetString(nameof(IWCN_JCP_Title_Execute), requestCulture.RequestCulture.UICulture);

        public static string GetIWCN_JCP_Title_Identifyer(this IRequestCultureFeature requestCulture) => GetString(nameof(IWCN_JCP_Title_Identifyer), requestCulture.RequestCulture.UICulture);

        public static string GetIWCN_JCP_Title_Details(this IRequestCultureFeature requestCulture) => GetString(nameof(IWCN_JCP_Title_Details), requestCulture.RequestCulture.UICulture);
    }
}
