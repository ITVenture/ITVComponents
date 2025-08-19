using ITVComponents.WebCoreToolkit.Blazor.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Blazor.Extensions
{
    public static class DependencyExtensions
    {
        public static IServiceCollection ConfigureStubComponents(this IServiceCollection services, Action<StubComponentConfiguration> configure)
        {
            return services.Configure(configure);
        }
    }
}
