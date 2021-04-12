using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;

namespace ITVComponents.WebCoreToolkit.Localization
{
    /// <summary>
    /// Interface that can be used to produce client-side useable localization scripts
    /// </summary>
    public interface ILocalScriptProvider<out T>
    {
        /// <summary>
        /// Renders the Script-block at the current location
        /// </summary>
        /// <returns>a html-script representing the requested script-block</returns>
        IHtmlContent RenderLocalizationScript(IHtmlHelper html);
    }
}
