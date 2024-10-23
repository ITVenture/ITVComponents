using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Helpers.Initialization
{
    public interface IDependencyInitializer
    {
        public IServiceCollection UseDbIdentities(IServiceCollection services, Action<IServiceProvider, DbContextOptionsBuilder> options);

        public IServiceCollection UseDbNavigation(IServiceCollection services);

        public IServiceCollection UseDbSharedAssets(IServiceCollection services);

        public IServiceCollection UseApplicationTokenService(IServiceCollection services);

        public IServiceCollection UseDbPlugins(IServiceCollection services, int bufferDuration);

        public IServiceCollection UseTenantSettings(IServiceCollection services);
    }
}
