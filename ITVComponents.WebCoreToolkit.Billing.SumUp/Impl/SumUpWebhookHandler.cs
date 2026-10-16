using System.Text.Json;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.SumUp.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Billing.SumUp.Impl
{
    /// <summary>Was mit einer SumUp-Meldung geschah.</summary>
    public enum SumUpWebhookOutcome
    {
        /// <summary>Der Verkauf wurde bei SumUp nachgefragt und gebucht.</summary>
        Processed,

        /// <summary>Kein Verkauf zu dieser Kennung - noch nicht, oder nie. SumUp soll es noch einmal versuchen.</summary>
        UnknownSale,

        /// <summary>Die Meldung war unbrauchbar oder passte nicht zum Verkauf.</summary>
        Rejected
    }

    /// <summary>
    /// Empfängt, was SumUp an <c>return_url</c> meldet.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Die Meldung trägt keine Signatur</b> - weder die Doku noch die OpenAPI-Beschreibung kennen eine für
    /// <c>return_url</c>. Ihr Inhalt wird darum nicht geglaubt: sie ist nur der Anstoss, den Verkauf bei SumUp mit
    /// dem Zugang des Mandanten nachzufragen. Wer eine Meldung fälscht, erreicht damit genau eine Nachfrage.
    /// </para>
    /// <para>
    /// Gebucht wird über den Terminaldienst (<see cref="ITerminalPaymentService.GetPaymentAsync"/>) - derselbe
    /// Weg wie beim Nachfragen der Kasse, mit denselben Garantien.
    /// </para>
    /// </remarks>
    public class SumUpWebhookHandler<TContext>
        where TContext : DbContext, IPaymentsContext
    {
        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly ITerminalPaymentService terminals;

        /// <summary>Initializes a new instance of the <see cref="SumUpWebhookHandler{TContext}"/> class.</summary>
        public SumUpWebhookHandler(IDbContextFactory<TContext> dbFactory, ITerminalPaymentService terminals)
        {
            this.dbFactory = dbFactory;
            this.terminals = terminals;
        }

        /// <summary>Verarbeitet den Rumpf einer Meldung.</summary>
        public async Task<SumUpWebhookOutcome> HandleAsync(string body, CancellationToken cancellationToken)
        {
            SumUpWebhookNotice? notice;
            try
            {
                notice = JsonSerializer.Deserialize<SumUpWebhookNotice>(body);
            }
            catch (JsonException ex)
            {
                LogEnvironment.LogEvent($"A SumUp notification is not readable JSON and is rejected: {ex.Message}",
                    LogSeverity.Warning, SumUpApiClient.LogContext);
                return SumUpWebhookOutcome.Rejected;
            }

            var clientTransactionId = notice?.Payload?.ClientTransactionId;
            if (string.IsNullOrWhiteSpace(clientTransactionId))
            {
                LogEnvironment.LogEvent(
                    $"A SumUp notification ('{notice?.EventType}', id {notice?.Id}) names no client_transaction_id and is rejected.",
                    LogSeverity.Warning, SumUpApiClient.LogContext);
                return SumUpWebhookOutcome.Rejected;
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var sale = await db.TenantSales.AsNoTracking()
                .Where(s => s.Provider == SumUpAccess.Key && s.ProviderSessionId == clientTransactionId)
                .Select(s => new { s.TenantSaleId, s.TenantId, s.ProviderAccountId })
                .FirstOrDefaultAsync(cancellationToken);
            if (sale == null)
            {
                LogEnvironment.LogEvent(
                    $"A SumUp notification for transaction {clientTransactionId} matches no sale; SumUp is asked to send it again.",
                    LogSeverity.Warning, SumUpApiClient.LogContext);
                return SumUpWebhookOutcome.UnknownSale;
            }

            if (!string.IsNullOrWhiteSpace(notice!.Payload!.MerchantCode)
                && !string.Equals(notice.Payload.MerchantCode, sale.ProviderAccountId, StringComparison.OrdinalIgnoreCase))
            {
                // Eine Meldung eines FREMDEN Haendlers darf diesen Verkauf nicht anstossen.
                LogEnvironment.LogEvent(
                    $"A SumUp notification for transaction {clientTransactionId} names merchant {notice.Payload.MerchantCode}, but sale {sale.TenantSaleId} (tenant {sale.TenantId}) runs on {sale.ProviderAccountId}. Rejected.",
                    LogSeverity.Error, SumUpApiClient.LogContext);
                return SumUpWebhookOutcome.Rejected;
            }

            var result = await terminals.GetPaymentAsync(sale.TenantSaleId, cancellationToken);
            LogEnvironment.LogDebugEvent(
                $"SumUp notified '{notice.Payload.Status}' for sale {sale.TenantSaleId}; asked SumUp, the sale is now '{result.SaleStatus}' ({result.State}).",
                LogSeverity.Report);
            return SumUpWebhookOutcome.Processed;
        }
    }
}
