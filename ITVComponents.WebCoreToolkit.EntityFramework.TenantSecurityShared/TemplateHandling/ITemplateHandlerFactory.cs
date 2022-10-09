using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.TemplateHandling
{
    public interface ITemplateHandlerFactory
    {
        Task<IHtmlContent> RenderHandlerComponent(IViewComponentHelper viewComponent, string componentTypeExpression,
            Dictionary<string, string> componentArguments);
    }
}
