using System;
using System.Collections.Generic;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.Services.Options;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.Services.Impl
{
    public class ManageNavPages: IManageNavigator
    {
        private readonly IOptions<ManageNavOptions> navOptions;
        public string ActivePage { get; set; }

        public ManageNavPages(IOptions<ManageNavOptions> navOptions)
        {
            this.navOptions = navOptions;
        }

        public string PageNavClass(ViewContext viewContext, string page)
        {
            var activePage = ActivePage
                ?? System.IO.Path.GetFileNameWithoutExtension(viewContext.ActionDescriptor.DisplayName);
            if (navOptions.Value.IsNavPage(page))
            {
                return string.Equals(activePage, page, StringComparison.OrdinalIgnoreCase) ? "active" : null;
            }

            return null;
        }

        public IEnumerable<ManageNavPage> NavPages => navOptions.Value.Pages;
    }
}
