using System;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security.SharedAssets;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Extensions
{
    /// <summary>
    /// Verdrahtung der persistierenden Registry der Asset-Argument-Konsumenten.
    /// </summary>
    public static class AssetArgumentRegistryExtensions
    {
        /// <summary>
        /// Registriert die Fassung der Registry, die ihre Deklarationen in der Systemtabelle behaelt.
        /// Wird von den <c>UseDbSharedAssets</c>-Aufrufen mitgezogen, kann aber auch einzeln gerufen
        /// werden.
        /// <para>
        /// Sie ueberschreibt die In-Memory-Grundfassung aus dem Kern-Paket. Beide Reihenfolgen gehen auf:
        /// registriert der Kern zuerst, gewinnt diese hier als spaetere Registrierung; registriert diese
        /// zuerst, laesst das <c>TryAdd</c> des Kerns sie stehen.
        /// </para>
        /// </summary>
        /// <param name="services">die Dienstsammlung</param>
        /// <param name="options">optionale Konfiguration</param>
        /// <returns>die uebergebene Dienstsammlung</returns>
        public static IServiceCollection UsePersistentAssetArgumentRegistry(this IServiceCollection services,
            Action<AssetArgumentRegistryOptions> options = null)
        {
            if (options != null)
            {
                services.Configure(options);
            }

            return services.AddSingleton<IAssetArgumentRegistry, PersistentAssetArgumentRegistry>();
        }

        /// <summary>
        /// Registriert das Zugriffsprotokoll, das in die Systemtabelle schreibt. Ueberschreibt die
        /// Null-Fassung aus dem Kern-Paket.
        /// </summary>
        /// <param name="services">die Dienstsammlung</param>
        /// <returns>die uebergebene Dienstsammlung</returns>
        public static IServiceCollection UseDbAssetAccessLog(this IServiceCollection services)
        {
            return services.AddScoped<IAssetAccessLog, DbAssetAccessLog>();
        }
    }
}
