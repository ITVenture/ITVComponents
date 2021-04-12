using System;
using System.Collections.Generic;
using System.Text;
using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.Localization;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Localization;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Localization
{
    /// <summary>
    /// Creates a script that will provide localization values to the client-side throught ITVenture.Text tools
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class ClientScriptResourceProvider<T>:ILocalScriptProvider<T>
    {
        /// <summary>
        /// Resource provider for strings
        /// </summary>
        private readonly IStringLocalizer<T> resourceProvider;

        /// <summary>
        /// Randomizer for unique naming
        /// </summary>
        private Random rnd;

        /// <summary>
        /// Initializes a new instance of the ClientScriptResourceProvider class
        /// </summary>
        /// <param name="resourceProvider">the string-localizer that is used to extract required strings</param>
        public ClientScriptResourceProvider(IStringLocalizer<T> resourceProvider)
        {
            this.resourceProvider = resourceProvider;
            rnd = new Random();
        }

        /// <summary>
        /// Renders the Script-block at the current location
        /// </summary>
        /// <returns>a html-script representing the requested script-block</returns>
        public IHtmlContent RenderLocalizationScript(IHtmlHelper html)
        {
            var dic = new Dictionary<string, string>();
            foreach (var s in resourceProvider.GetAllStrings(true))
            {
                dic.Add(s.Name, s.Value);
            }

            var json = JsonHelper.ToJson(dic);
            StringBuilder bld = new StringBuilder();
            var ext = $"{rnd.Next(10000000)}_{DateTime.Now.Ticks}";
            bld.AppendLine("<script type=\"text/javascript\">");
            try
            {
                bld.AppendLine($"var __loca{ext}={json};");
                bld.AppendLine($@"for (var name in __loca{ext}){{
    if (__loca{ext}.hasOwnProperty(name)){{
        ITVenture.Text.setText(ITVenture.Lang,name,__loca{ext}[name]);
    }}
}}");
            }
            finally
            {
                bld.AppendLine("</script>");
            }

            return html.Raw(bld.ToString());
        }
    }
}
