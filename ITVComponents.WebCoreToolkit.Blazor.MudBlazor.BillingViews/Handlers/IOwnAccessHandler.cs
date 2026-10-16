using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;

namespace ITVComponents.WebCoreToolkit.BillingViews.Blazor.Handlers
{
    /// <summary>
    /// Die Nahtstelle der eigenen Anbieter-Zugänge ("Plan B") zur Oberfläche.
    /// </summary>
    /// <remarks>
    /// Wie bei den Terminals: <see cref="CanView"/> und <see cref="CanManage"/> steuern nur die ANZEIGE. Die
    /// Umsetzung prüft in jeder Methode selbst Recht, Hauptschalter und Mandanten-Feature, und der Mandant kommt
    /// immer aus dem Sicherheitsbereich, nie aus einem Argument.
    /// </remarks>
    public interface IOwnAccessHandler
    {
        /// <summary>Darf die eigenen Zugänge sehen.</summary>
        bool CanView();

        /// <summary>Darf eigene Zugänge anlegen, erneuern, prüfen und abschalten.</summary>
        bool CanManage();

        /// <summary>Ist das Zahlungsmodul eingeschaltet?</summary>
        bool IsEnabled();

        /// <summary>Lässt sich hier überhaupt ein eigener Zugang einrichten (Verwaltung und Anbieter registriert)?</summary>
        bool IsAvailable();

        /// <summary>Die Anbieter, für die sich ein eigener Zugang einrichten lässt.</summary>
        IReadOnlyList<string> GetProviders();

        /// <summary>Die Felder der Maske eines Anbieters.</summary>
        IReadOnlyList<OwnAccessField> DescribeFields(string provider);

        /// <summary>Die eigenen Zugänge des aktiven Mandanten.</summary>
        Task<IReadOnlyList<OwnAccessInfo>> GetAsync(CancellationToken cancellationToken = default);

        /// <summary>Verbindet ein Konto des Mandanten.</summary>
        Task<int> CreateAsync(string provider, string displayName, IReadOnlyDictionary<string, string?> values,
            CancellationToken cancellationToken = default);

        /// <summary>Ersetzt den Schlüssel am selben Konto.</summary>
        Task RenewSecretsAsync(int accessId, IReadOnlyDictionary<string, string?> values,
            CancellationToken cancellationToken = default);

        /// <summary>Schaltet einen Zugang ab oder wieder an.</summary>
        Task SetDisabledAsync(int accessId, bool disabled, CancellationToken cancellationToken = default);

        /// <summary>Prüft einen Zugang.</summary>
        Task<OwnAccessCheck> VerifyAsync(int accessId, CancellationToken cancellationToken = default);

        /// <summary>Richtet die Meldungen im Konto des Mandanten ein, wo der Anbieter das kann.</summary>
        Task<OwnAccessCheck> SetUpWebhooksAsync(int accessId, string publicBaseUrl, CancellationToken cancellationToken = default);
    }
}
