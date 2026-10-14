using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments
{
    /// <summary>
    /// Holt die Belege bezahlter Terminal-Verkäufe nach, deren Meldung an die Beobachter noch aussteht.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Der Fall: ein Webhook meldet die Zahlung, bevor das Terminal mit dem Beleg geantwortet hat. Die Meldung
    /// wird dann zurückgehalten (<see cref="Models.Payments.TenantSale.CompletionPendingSince"/>). Meist holt
    /// die Antwort des Terminals sie nach — dieser Dienst ist für den Fall, dass sie ausbleibt.
    /// </para>
    /// <para>
    /// <b>Er kennt keine Anbindung.</b> Er fragt über <see cref="ITerminalPaymentService"/>, also über die
    /// Weiche, die nach dem Weg des Geräts weiterleitet — beim Agenten bis zur Kasse. Damit deckt er jeden
    /// Terminal-Typ ab, auch künftige. Nach Ablauf der Frist wird ohne Beleg gemeldet.
    /// </para>
    /// <para>
    /// Er läuft in jeder Instanz. Doppelt gemeldet wird trotzdem nicht: das Leeren des Merkmals ist ein
    /// Nebenläufigkeits-geschützter Anspruch, den genau einer bekommt.
    /// </para>
    /// </remarks>
    public sealed class TerminalCompletionSweeper<TContext> : BackgroundService
        where TContext : DbContext, IPaymentsContext
    {
        private readonly IServiceScopeFactory scopeFactory;

        /// <summary>Initializes a new instance of the <see cref="TerminalCompletionSweeper{TContext}"/> class.</summary>
        public TerminalCompletionSweeper(IServiceScopeFactory scopeFactory)
        {
            this.scopeFactory = scopeFactory;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var pollSeconds = 5;
                try
                {
                    pollSeconds = await SweepAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // Der Durchlauf als Ganzes ist gescheitert (Datenbank weg o.ae.). Weiterlaufen: beim
                    // naechsten Mal kann es gehen, und ein angehaltener Dienst meldete gar nichts mehr.
                    LogEnvironment.LogEvent(
                        $"The sweep for terminal receipts still pending failed and is retried: {ex.OutlineException()}",
                        LogSeverity.Error, LogContext);
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, pollSeconds)), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        /// <summary>Ein Durchlauf über alle offenen Meldungen.</summary>
        /// <returns>der Abstand bis zum nächsten Durchlauf, in Sekunden</returns>
        internal async Task<int> SweepAsync(CancellationToken cancellationToken)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var options = services.GetRequiredService<IGlobalSettings<TenantPaymentsOptions>>().Value;
            if (!options.Enabled)
            {
                return options.ReceiptPollSeconds;
            }

            var dbFactory = services.GetRequiredService<IDbContextFactory<TContext>>();
            List<int> pending;
            await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
            {
                // Ohne Mandantenfilter: der Dienst laeuft ohne Benutzer und ohne Mandanten im Bereich, und
                // gerade dann darf kein Filter einen Verkauf verstecken, der auf seine Meldung wartet.
                pending = await db.TenantSales.IgnoreQueryFilters().AsNoTracking()
                    .Where(s => s.CompletionPendingSince != null)
                    .OrderBy(s => s.CompletionPendingSince)
                    .Select(s => s.TenantSaleId)
                    .Take(100)
                    .ToListAsync(cancellationToken);
            }

            foreach (var saleId in pending)
            {
                await SweepSaleAsync(services, dbFactory, options, saleId, cancellationToken);
            }

            return options.ReceiptPollSeconds;
        }

        private static async Task SweepSaleAsync(IServiceProvider services, IDbContextFactory<TContext> dbFactory,
            TenantPaymentsOptions options, int saleId, CancellationToken cancellationToken)
        {
            try
            {
                // Fragt das Geraet nach dem Beleg und gibt die Meldung frei - oder nach der Frist ohne Beleg.
                await services.GetRequiredService<ITerminalPaymentService>().GetPaymentAsync(saleId, cancellationToken);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Das Geraet ist nicht mehr nutzbar (abgeschaltet, Weg nicht mehr registriert). Dann bleibt nur
                // die Frist - die gilt hier genauso, sonst wartete der Verkauf fuer immer.
                LogEnvironment.LogEvent(
                    $"Could not collect the receipt of the paid terminal sale {saleId}; it is released without one once the wait period ends: {ex.OutlineException()}",
                    LogSeverity.Warning, LogContext);
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var sale = await db.TenantSales.IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantSaleId == saleId, cancellationToken);
            if (sale == null)
            {
                LogEnvironment.LogEvent($"Terminal sale {saleId} vanished while its completion was pending.",
                    LogSeverity.Warning, LogContext);
                return;
            }

            await TerminalPaymentServiceBase<TContext>.ReleaseIfOverdueAsync(sale, db, options,
                services.GetRequiredService<TenantSaleWebhookSink<TContext>>(), cancellationToken);
        }

        private const string LogContext = "TenantPayments";
    }
}
