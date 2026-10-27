using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
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
    /// <para>
    /// <b>Zweite Aufgabe: offene Terminal-Verkäufe abgleichen.</b> Ist eine Zahlung am Gerät gestartet und die
    /// Kasse danach weg, fragt niemand mehr nach — und Wege ohne Rückkanal (Agent, Stripe Terminal, Payrexx ECR)
    /// blieben für immer <c>Pending</c>, obwohl der Kunde bezahlt hat. Nach
    /// <see cref="TenantPaymentsOptions.TerminalReconcileAfterSeconds"/> ohne Nachfrage fragt dieser Dienst über
    /// dieselbe Weiche nach wie die Kasse, alle <see cref="TenantPaymentsOptions.TerminalReconcileIntervalSeconds"/>,
    /// bis <see cref="TenantPaymentsOptions.TerminalReconcileMaxAgeHours"/>. Was das Gerät dann sagt, wird gebucht
    /// wie eine Antwort an der Kasse, mit denselben Garantien.
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

            if (options.TerminalReconcileAfterSeconds > 0)
            {
                await ReconcileOpenSalesAsync(services, dbFactory, options, cancellationToken);
            }

            return options.ReceiptPollSeconds;
        }

        /// <summary>Ein Durchlauf über die offenen Terminal-Verkäufe, nach denen niemand mehr fragt.</summary>
        internal static async Task ReconcileOpenSalesAsync(IServiceProvider services,
            IDbContextFactory<TContext> dbFactory, TenantPaymentsOptions options, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var startedBefore = now.AddSeconds(-options.TerminalReconcileAfterSeconds);
            var askedBefore = now.AddSeconds(-ReconcileInterval(options));
            var startedAfter = now.AddHours(-ReconcileMaxAgeHours(options));
            List<int> open;
            await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
            {
                // Updated ist zugleich "zuletzt gefragt": jede Nachfrage der Kasse frischt es auf, und solange sie
                // fragt, bleibt der Verkauf hier draussen. Ohne Mandantenfilter, aus demselben Grund wie oben.
                open = await db.TenantSales.IgnoreQueryFilters().AsNoTracking()
                    .Where(s => s.TenantPaymentTerminalId != null && s.Status == TenantSaleStatus.Pending
                                && s.CompletionPendingSince == null
                                && s.Created <= startedBefore && s.Created >= startedAfter
                                && s.Updated <= askedBefore)
                    .OrderBy(s => s.Updated)
                    .Select(s => s.TenantSaleId)
                    .Take(50)
                    .ToListAsync(cancellationToken);
            }

            foreach (var saleId in open)
            {
                await ReconcileSaleAsync(services, dbFactory, options, saleId, cancellationToken);
            }
        }

        private static async Task ReconcileSaleAsync(IServiceProvider services, IDbContextFactory<TContext> dbFactory,
            TenantPaymentsOptions options, int saleId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await services.GetRequiredService<ITerminalPaymentService>()
                    .GetPaymentAsync(saleId, cancellationToken);
                if (result.SaleStatus != TenantSaleStatus.Pending)
                {
                    LogEnvironment.LogEvent(
                        $"Terminal sale {saleId} had been left open and was settled in the background as '{result.SaleStatus}'.",
                        LogSeverity.Report, LogContext);
                    return;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogEnvironment.LogEvent(
                    $"Could not ask about the open terminal sale {saleId}; it is asked again in {ReconcileInterval(options)} seconds: {ex.OutlineException()}",
                    LogSeverity.Warning, LogContext);
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var sale = await db.TenantSales.IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantSaleId == saleId, cancellationToken);
            if (sale == null)
            {
                LogEnvironment.LogEvent($"Terminal sale {saleId} vanished while it was being reconciled.",
                    LogSeverity.Warning, LogContext);
                return;
            }

            if (sale.Status != TenantSaleStatus.Pending)
            {
                return;
            }

            // Als gefragt markieren, auch wenn das Geraet nicht geantwortet hat - sonst kaeme derselbe Verkauf in
            // jedem Durchlauf wieder dran, alle paar Sekunden an ein Geraet, das gerade nicht erreichbar ist.
            sale.Updated = DateTime.UtcNow;
            if (!await services.GetRequiredService<TenantSaleWebhookSink<TContext>>()
                    .TrySaveAsync(sale, db, cancellationToken))
            {
                // Inzwischen entschieden - genau das, worauf dieser Durchgang wartet.
                return;
            }

            if (sale.Created.AddHours(ReconcileMaxAgeHours(options)) < DateTime.UtcNow.AddSeconds(ReconcileInterval(options)))
            {
                // Der letzte Durchgang fuer diesen Verkauf: beim naechsten ist er zu alt.
                LogEnvironment.LogEvent(
                    $"Terminal sale {sale.TenantSaleId} (tenant {sale.TenantId}, reference '{sale.ExternalReference}', terminal {sale.TenantPaymentTerminalId}) has been open since {sale.Created:u} and the background stops asking about it. It is NOT known whether the card was charged — check it at the provider or the terminal and settle it by hand.",
                    LogSeverity.Error, LogContext);
            }
        }

        private static int ReconcileInterval(TenantPaymentsOptions options)
            => Math.Max(5, options.TerminalReconcileIntervalSeconds);

        private static int ReconcileMaxAgeHours(TenantPaymentsOptions options)
            => Math.Max(1, options.TerminalReconcileMaxAgeHours);

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
