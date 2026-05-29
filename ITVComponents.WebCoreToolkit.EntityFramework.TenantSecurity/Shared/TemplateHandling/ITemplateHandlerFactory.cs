using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.TemplateHandling
{
    public interface ITemplateHandlerFactory
    {
        //Task<IHtmlContent> RenderHandlerComponent(IViewComponentHelper viewComponent, TemplateModuleConfigurator componentArguments);
        object GetBackEndHandler(TemplateModuleConfigurator configurator, out IDictionary<string, object> arguments);
    }
}
