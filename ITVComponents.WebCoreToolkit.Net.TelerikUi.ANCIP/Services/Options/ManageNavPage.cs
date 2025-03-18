using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Services.Options
{
    public class ManageNavPage
    {
        public string NavTag { get; set; }

        public string NavLinkId { get; set; }

        public string PageLink { get; set; }

        public string LocalizerType { get; set; }

        public string NavigationLinkText { get; set; }

        public string LocalName(IServiceProvider services)
        {
            if (!string.IsNullOrEmpty(LocalizerType))
            {
                Type localizerType = null;
                try
                {
                    var tmpType = Type.GetType(LocalizerType);
                    if (tmpType != null)
                    {
                        localizerType = typeof(IStringLocalizer<>).MakeGenericType(tmpType);
                    }
                }
                catch (Exception ex)
                {
                    services.GetService<ILogger<ManageNavPage>>().LogError("Type {LocalizerType} could not beloaded. {Error}", LocalizerType, ex.Message);
                }

                if (localizerType != null)
                {
                    IStringLocalizer localizer = (IStringLocalizer)services.GetService(localizerType);
                    if (localizer != null)
                    {
                        return localizer[NavigationLinkText];
                    }
                }
            }

            return NavigationLinkText;
        }
    }
}
