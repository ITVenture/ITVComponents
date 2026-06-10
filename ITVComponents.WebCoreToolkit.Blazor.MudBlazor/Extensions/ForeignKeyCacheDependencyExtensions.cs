using ITVComponents.WebCoreToolkit.Blazor.SharedComponents.ForeignKeys;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents;

public static class ForeignKeyCacheDependencyExtensions
{
    /// <summary>
    /// Registers the foreign-key label cache used by <c>ForeignKeyLabel</c> /
    /// <c>ForeignKeyColumn</c>. Idempotent — safe to call from multiple WebParts.
    /// </summary>
    public static IServiceCollection AddToolkitForeignKeyCache(this IServiceCollection services)
    {
        services.TryAddScoped<IForeignKeyLabelCache, ForeignKeyLabelCache>();
        return services;
    }
}
