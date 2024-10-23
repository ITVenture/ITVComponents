using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Helpers.Initialization
{
    public interface IDependencyInitializer
    {
        public IServiceCollection UseDbIdentities(IServiceCollection services, Action<IServiceProvider, DbContextOptionsBuilder> options);

        public IServiceCollection UseDbNavigation(IServiceCollection services);

        public IServiceCollection UseDbSharedAssets(IServiceCollection services);

        public IServiceCollection UseDbPlugins(IServiceCollection services, int bufferDuration);

        public IServiceCollection UseTenantSettings(IServiceCollection services);
    }
}
