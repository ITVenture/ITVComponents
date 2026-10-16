using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments
{
    /// <summary>
    /// Das Gemeinsame der Anbieter beim Einrichten eines eigenen Zugangs: Pflichtfelder prüfen und Werte lesen.
    /// </summary>
    public abstract class OwnAccessProviderBase : IOwnAccessProvider
    {
        /// <inheritdoc />
        public abstract string Key { get; }

        /// <summary>Die Felder der Maske.</summary>
        protected abstract IReadOnlyList<OwnAccessField> Fields { get; }

        /// <inheritdoc />
        public virtual string? WebhookPath => null;

        /// <inheritdoc />
        public virtual bool CanSetUpWebhooks => false;

        /// <inheritdoc />
        public IReadOnlyList<OwnAccessField> DescribeFields() => Fields;

        /// <inheritdoc />
        public OwnAccessDraft Compose(IReadOnlyDictionary<string, string?> values)
        {
            EnsureRequired(Fields, values);
            return Build(values);
        }

        /// <inheritdoc />
        public IReadOnlyList<OwnAccessSecret> ComposeSecrets(IReadOnlyDictionary<string, string?> values)
        {
            EnsureRequired(Fields.Where(f => f.Secret || f.PartOfCredential), values);
            return BuildSecrets(values);
        }

        /// <inheritdoc />
        public abstract Task<OwnAccessCheck> VerifyAsync(ProviderAccessSelection access,
            CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public virtual Task<OwnAccessCheck> SetUpWebhooksAsync(ProviderAccessSelection access, string webhookUrl,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new OwnAccessCheck(false, $"'{Key}' cannot set up its notifications from here."));

        /// <summary>Baut Zeile und Geheimnisse; die Pflichtfelder sind schon geprüft.</summary>
        protected abstract OwnAccessDraft Build(IReadOnlyDictionary<string, string?> values);

        /// <summary>Baut nur die Geheimnisse; die Pflichtfelder der Anmeldung sind schon geprüft.</summary>
        protected abstract IReadOnlyList<OwnAccessSecret> BuildSecrets(IReadOnlyDictionary<string, string?> values);

        /// <summary>Der getrimmte Wert eines Feldes, oder leer.</summary>
        protected static string? Value(IReadOnlyDictionary<string, string?> values, string name)
            => values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

        /// <summary>Der Wert eines Pflichtfeldes.</summary>
        protected static string Required(IReadOnlyDictionary<string, string?> values, string name)
            => Value(values, name) ?? throw new TenantPaymentException(PaymentErrorCodes.InvalidSettings, $"'{name}' is required.");

        private static void EnsureRequired(IEnumerable<OwnAccessField> fields, IReadOnlyDictionary<string, string?> values)
        {
            var missing = fields.Where(f => f.Required && Value(values, f.Name) == null).Select(f => f.Label ?? f.Name).ToList();
            if (missing.Count != 0)
            {
                throw new TenantPaymentException(PaymentErrorCodes.InvalidSettings,
                    $"These fields are required: {string.Join(", ", missing)}.");
            }
        }
    }
}
