using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Shared.Security;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Health.Impl
{
    internal class ServicesDecorator:IServices
    {
        private readonly IServiceProvider services;

        public ServicesDecorator(IServiceProvider services)
        {
            this.services = services;
        }

        public T GetService<T>()
        {
            return services.GetService<T>();
        }

        public IList<T> GetServices<T>()
        {
            return services.GetServices<T>().ToList();
        }
    }
}
