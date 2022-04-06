using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Castle.Core.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewComponents
{
    public class RequiredSection:ViewComponent
    {
        private readonly ILogger<RequiredSection> logger;

        public RequiredSection(ILogger<RequiredSection> logger)
        {
            this.logger = logger;
        }
        public async Task<IViewComponentResult> InvokeAsync(string nameOfView = null, IDictionary<string,object> customViewData = null, bool warnOnViewNotFound=true)
        {
            if (customViewData != null)
            {
                foreach (var vd in customViewData)
                {
                    ViewData[vd.Key] = vd.Value;
                }
            }

            var pth = $"Components/RequiredSection/{(!string.IsNullOrEmpty(nameOfView)?nameOfView:"Default")}";
            if (ViewEngine.FindView(ViewContext, pth, false).Success)
            {
                if (string.IsNullOrEmpty(nameOfView))
                {
                    return View();
                }
                else
                {
                    return View(viewName: nameOfView);
                }
            }

            if (warnOnViewNotFound)
            {
                logger.Log(LogLevel.Warning, "The Component-View {pth} was not found.", pth);
                return Content(string.Empty);
            }

            throw new InvalidOperationException($"The Component-View {pth} was not found.");
        }
    }
}
