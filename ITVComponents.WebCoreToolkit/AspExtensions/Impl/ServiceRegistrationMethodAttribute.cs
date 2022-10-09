using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.AspExtensions.Impl
{
    public class ServiceRegistrationMethodAttribute : CustomConfiguratorAttribute
    {
        public ServiceRegistrationMethodAttribute() : base(typeof(IServiceCollection))
        {
        }
    }
}
