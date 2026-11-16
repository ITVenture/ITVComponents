using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Options;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Localization
{
    /// <summary>
    /// Standard-Umsetzung von <see cref="ITenantLocaleResolver"/>: fragt das Repository nach den gespeicherten
    /// Werten und ergänzt die Vorgaben des Hosts.
    /// </summary>
    /// <remarks>
    /// Scoped, und merkt sich die Antwort des Repositorys je Mandant für die Dauer des Scopes: ein Beleg fragt
    /// Zeitzone, Sprache und Kultur nacheinander, das soll nicht drei Rundreisen kosten.
    /// </remarks>
    public class TenantLocaleResolver : ITenantLocaleResolver
    {
        private readonly ISecurityRepository securityRepository;
        private readonly RequestLocalizationOptions localizationOptions;
        private readonly CultureOptions cultureOptions;
        private readonly ILogger<TenantLocaleResolver> logger;
        private readonly Dictionary<string, TenantLocale> buffer = new(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyList<CultureInfo> supportedCultures;

        public TenantLocaleResolver(ISecurityRepository securityRepository, IOptions<RequestLocalizationOptions> localizationOptions,
            IOptions<CultureOptions> cultureOptions, ILogger<TenantLocaleResolver> logger)
        {
            this.securityRepository = securityRepository;
            this.localizationOptions = localizationOptions.Value;
            this.cultureOptions = cultureOptions.Value;
            this.logger = logger;
        }

        /// <inheritdoc />
        public IReadOnlyList<CultureInfo> SupportedCultures => supportedCultures ??= ReadSupportedCultures();

        /// <inheritdoc />
        public TimeZoneInfo GetTimeZone(string permissionScopeName) => Locale(permissionScopeName).ResolveTimeZone();

        /// <inheritdoc />
        public CultureInfo GetUICulture(string permissionScopeName)
        {
            var stored = Locale(permissionScopeName).Culture;
            if (!string.IsNullOrEmpty(stored))
            {
                CultureInfo parsed;
                try
                {
                    parsed = CultureInfo.GetCultureInfo(stored);
                }
                catch (CultureNotFoundException ex)
                {
                    logger.LogError(ex, "The culture '{Culture}' stored for tenant {Tenant} is not a valid culture name; the host default is used instead.",
                        stored, permissionScopeName);
                    return DefaultCulture();
                }

                if (string.IsNullOrEmpty(parsed.Name))
                {
                    logger.LogWarning("Tenant {Tenant} stores the invariant culture; the host default is used instead.", permissionScopeName);
                    return DefaultCulture();
                }

                // Der gespeicherte Wert gilt, wie er ist, sobald der Host seine SPRACHE anbietet: de-AT bei einem
                // Host mit de-CH/de-DE bekommt die deutschen Texte ueber den Rueckfall des Localizers
                // (de-AT -> de -> neutral) und behaelt sein eigenes Zahl- und Datumsformat.
                if (SupportedCultures.Count == 0 || SupportedCultures.Any(c => SameLanguage(c, parsed)))
                {
                    return parsed;
                }

                // Gespeichert, aber die Sprache wird (nicht mehr) angeboten - z.B. vom Host abgeschaltet.
                // Das ist kein Normalfall: der Beleg erscheint dann nicht in der gewaehlten Sprache.
                logger.LogWarning("The culture '{Culture}' of tenant {Tenant} is not covered by the supported cultures ({Supported}); the host default is used instead.",
                    stored, permissionScopeName, string.Join(", ", SupportedCultures.Select(c => c.Name)));
            }

            return DefaultCulture();
        }

        /// <inheritdoc />
        public CultureInfo GetCulture(string permissionScopeName) => cultureOptions.MapCulture(GetUICulture(permissionScopeName));

        /// <inheritdoc />
        public CultureInfo FindSupportedCulture(CultureInfo culture)
        {
            if (culture == null || string.IsNullOrEmpty(culture.Name))
            {
                return null;
            }

            if (SupportedCultures.Count == 0)
            {
                return culture;
            }

            return SupportedCultures.FirstOrDefault(c => string.Equals(c.Name, culture.Name, StringComparison.OrdinalIgnoreCase))
                   ?? SupportedCultures.FirstOrDefault(c => SameLanguage(c, culture));
        }

        private TenantLocale Locale(string permissionScopeName)
        {
            var key = permissionScopeName ?? string.Empty;
            if (!buffer.TryGetValue(key, out var retVal))
            {
                retVal = securityRepository.GetTenantLocale(permissionScopeName) ?? new TenantLocale();
                buffer[key] = retVal;
            }

            return retVal;
        }

        private CultureInfo DefaultCulture()
        {
            var configured = localizationOptions.DefaultRequestCulture?.UICulture;
            if (configured != null && !string.IsNullOrEmpty(configured.Name))
            {
                return configured;
            }

            if (SupportedCultures.Count != 0)
            {
                return SupportedCultures[0];
            }

            logger.LogWarning("No supported cultures and no default request culture are configured; tenant documents use the invariant culture.");
            return CultureInfo.InvariantCulture;
        }

        private IReadOnlyList<CultureInfo> ReadSupportedCultures()
        {
            var source = localizationOptions.SupportedUICultures ?? (IList<CultureInfo>)Array.Empty<CultureInfo>();
            return source.Where(c => c != null && !string.IsNullOrEmpty(c.Name))
                .Select(c => c.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(CultureInfo.GetCultureInfo)
                .ToList();
        }

        /// <summary>Ob zwei Kulturen dieselbe Sprache meinen: <c>de-CH</c> und <c>de-AT</c> ja, <c>de</c> und <c>fr</c> nicht.</summary>
        private static bool SameLanguage(CultureInfo a, CultureInfo b)
            => string.Equals(Language(a), Language(b), StringComparison.OrdinalIgnoreCase);

        /// <summary>Die neutrale Sprache einer Kultur: <c>de-CH</c> → <c>de</c>.</summary>
        private static string Language(CultureInfo culture)
        {
            while (!culture.IsNeutralCulture && culture.Parent != null && !string.IsNullOrEmpty(culture.Parent.Name))
            {
                culture = culture.Parent;
            }

            return culture.Name;
        }
    }
}
