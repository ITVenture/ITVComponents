using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.TemplateHandling;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared
{
    [WebPart]
    public static class WebPartInit
    {
        [MvcRegistrationMethod]
        public static void RegisterAssemblyPart(ApplicationPartManager manager)
        {
            manager.RegisterSharedViews();
        }

        [ServiceRegistrationMethod]
        public static void RegisterServices(IServiceCollection services)
        {
            services.AddScoped<ITemplateHandlerFactory, TemplateHandlerFactory>();
        }
    }
}
