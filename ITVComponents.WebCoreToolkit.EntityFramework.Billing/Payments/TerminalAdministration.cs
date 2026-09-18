using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments
{
    /// <summary>
    /// Anlegen, Ändern und Abschalten von Zahlungsterminals.
    /// </summary>
    /// <remarks>
    /// Der Schluss des Assistenten „Gerät hinzufügen": was in zwei Schritten zusammengetragen wurde,
    /// landet hier als eine Zeile.
    /// </remarks>
    public class TerminalAdministration<TContext> : ITerminalAdministration
        where TContext : DbContext, IPaymentsContext
    {
        private readonly IDbContextFactory<TContext> dbFactory;

        /// <summary>Initializes a new instance of the <see cref="TerminalAdministration{TContext}"/> class.</summary>
        public TerminalAdministration(IDbContextFactory<TContext> dbFactory)
        {
            this.dbFactory = dbFactory;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<TerminalDefinition>> GetAsync(int tenantId,
            CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            return await db.TenantPaymentTerminals.AsNoTracking()
                .Where(t => t.TenantId == tenantId)
                .OrderBy(t => t.DisplayName)
                .Select(t => new TerminalDefinition
                {
                    TerminalId = t.TenantPaymentTerminalId,
                    TenantId = t.TenantId,
                    Provider = t.Provider,
                    ProviderTerminalId = t.ProviderTerminalId,
                    Route = t.Route,
                    DisplayName = t.DisplayName,
                    ConfigurationJson = t.ConfigurationJson,
                    Enabled = t.Enabled
                })
                .ToListAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<int> SaveAsync(TerminalDefinition definition, CancellationToken cancellationToken = default)
        {
            Validate(definition);

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            TenantPaymentTerminal row;
            if (definition.TerminalId > 0)
            {
                row = await db.TenantPaymentTerminals
                          .FirstOrDefaultAsync(t => t.TenantPaymentTerminalId == definition.TerminalId, cancellationToken)
                      ?? throw new TenantPaymentException(PaymentErrorCodes.SaleNotFound,
                          $"Terminal {definition.TerminalId} does not exist.");

                EnsureOwnership(row, definition.TenantId);
                // Der Mandant wird NICHT mitgeaendert: ein Geraet zu einem anderen umzuhaengen hiesse,
                // dass die Verkaeufe darauf plotzlich zu einem fremden gehoeren. Wer das wirklich will,
                // schaltet es ab und legt es neu an.
            }
            else
            {
                row = new TenantPaymentTerminal
                {
                    TenantId = definition.TenantId,
                    Created = DateTime.UtcNow
                };
                db.TenantPaymentTerminals.Add(row);
            }

            row.Provider = definition.Provider.Trim().ToLowerInvariant();
            row.ProviderTerminalId = definition.ProviderTerminalId.Trim();
            row.Route = string.IsNullOrWhiteSpace(definition.Route) ? null : definition.Route.Trim();
            row.DisplayName = definition.DisplayName.Trim();
            row.ConfigurationJson = string.IsNullOrWhiteSpace(definition.ConfigurationJson)
                ? null
                : definition.ConfigurationJson;
            row.Enabled = definition.Enabled;
            row.Updated = DateTime.UtcNow;

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                // Der eindeutige Index ueber (Weg, Geraetekennung) hat zugeschlagen. Das ist kein
                // technisches Detail, sondern der Unfall, den er verhindern soll: dasselbe Geraet bei
                // zwei Mandanten hiesse, dass einer auf dem Terminal des anderen kassiert.
                LogEnvironment.LogEvent(
                    $"Terminal '{row.ProviderTerminalId}' could not be saved for provider '{row.Provider}': it is most likely already registered, possibly for another tenant. {ex.Message}",
                    LogSeverity.Error, TenantSaleWebhookSink<TContext>.LogContext);
                throw new TenantPaymentException(PaymentErrorCodes.ProviderError,
                    $"A terminal with the id '{row.ProviderTerminalId}' is already registered for provider '{row.Provider}'. A device belongs to exactly one tenant.", ex);
            }

            return row.TenantPaymentTerminalId;
        }

        /// <inheritdoc />
        public async Task SetEnabledAsync(int tenantId, int terminalId, bool enabled,
            CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var row = await db.TenantPaymentTerminals
                          .FirstOrDefaultAsync(t => t.TenantPaymentTerminalId == terminalId, cancellationToken)
                      ?? throw new TenantPaymentException(PaymentErrorCodes.SaleNotFound,
                          $"Terminal {terminalId} does not exist.");

            EnsureOwnership(row, tenantId);
            row.Enabled = enabled;
            row.Updated = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>Prüft, was ohne Gerät keinen Sinn ergibt.</summary>
        private static void Validate(TerminalDefinition definition)
        {
            if (definition.TenantId <= 0 || string.IsNullOrWhiteSpace(definition.Provider)
                                         || string.IsNullOrWhiteSpace(definition.ProviderTerminalId)
                                         || string.IsNullOrWhiteSpace(definition.DisplayName))
            {
                throw new TenantPaymentException(PaymentErrorCodes.InvalidAmount,
                    "A terminal needs a tenant, a provider, the id it is known by there, and a name people can recognise it under.");
            }
        }

        /// <summary>
        /// Stellt sicher, dass hier niemand am Gerät eines fremden Mandanten dreht.
        /// </summary>
        /// <remarks>
        /// Dieselbe Prüfung wie beim Kassieren, und aus demselben Grund: die Verwaltung ist der
        /// bequemere Weg, ein fremdes Gerät an sich zu ziehen, als der Zahlungsweg.
        /// </remarks>
        private static void EnsureOwnership(TenantPaymentTerminal row, int tenantId)
        {
            if (row.TenantId == tenantId)
            {
                return;
            }

            LogEnvironment.LogEvent(
                $"Tenant {tenantId} tried to change terminal {row.TenantPaymentTerminalId}, which belongs to tenant {row.TenantId}. Refused.",
                LogSeverity.Error, TenantSaleWebhookSink<TContext>.LogContext);
            throw new TenantPaymentException(PaymentErrorCodes.SaleNotFound,
                $"Terminal {row.TenantPaymentTerminalId} does not belong to tenant {tenantId}.");
        }
    }
}
