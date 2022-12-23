using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.GenericService.WebService.Configurators;
using Microsoft.AspNetCore.Builder;

namespace ITVComponents.GenericService.WebService
{
    public interface IWebHostStartup
    {
        /// <summary>
        /// Gets the WebApp-Builder that enables dependent modules to register extension for Security or custom dependencies
        /// </summary>
        WebApplicationBuilder WebAppBuilder { get; }


        void RegisterConfigurator(IServiceHostConfigurator configurator);
    }
}
