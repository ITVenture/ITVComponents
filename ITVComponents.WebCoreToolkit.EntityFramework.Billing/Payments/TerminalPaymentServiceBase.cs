using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.Terminals.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments
{
    /// <summary>
    /// Die Buchführung hinter dem Kassieren am Terminal — für jeden der vier Wege dieselbe.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Dasselbe Verhältnis wie bei <see cref="TenantSaleServiceBase{TContext}"/>: hier steht, was mit dem
    /// Verkauf geschieht, dort in den Ableitungen, wie man mit dem Gerät redet. Und wie dort ist der
    /// gemeinsame Teil der grössere — Mandant und Gerät prüfen, den Verkauf anlegen, die Provision
    /// einfrieren, das Ergebnis buchen, Beobachter benachrichtigen.
    /// </para>
    /// <para>
    /// <b>Gebucht wird über <see cref="TenantSaleWebhookSink{TContext}"/>,</b> dieselbe Senke wie bei
    /// einem Webhook. Das ist kein Zufall: die Lage ist identisch. Eine Antwort, die vielleicht schon
    /// einmal da war, trifft auf einen Verkauf, der vielleicht schon gebucht ist. Wer hier eigene Regeln
    /// schriebe, hätte eine vierte Kopie derselben drei Garantien.
    /// </para>
    /// <para>
    /// <b>Der Kassen-PC ist das Gerät, das ausfällt.</b> Deshalb steht der Stand hier und nicht dort:
    /// nach einem Neustart mitten in einer Zahlung ist die Verkaufszeile das Einzige, was noch weiss,
    /// dass etwas lief — und <see cref="GetPaymentAsync"/> die einzige Möglichkeit herauszufinden, ob
    /// die Karte belastet wurde.
    /// </para>
    /// </remarks>
    public abstract class TerminalPaymentServiceBase<TContext> : ITerminalPaymentService
        where TContext : DbContext, IPaymentsContext
    {
        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly PaymentsRuntime runtime;
        private readonly TenantSaleWebhookSink<TContext> sink;

        /// <summary>Initializes a new instance of the <see cref="TerminalPaymentServiceBase{TContext}"/> class.</summary>
        protected TerminalPaymentServiceBase(IDbContextFactory<TContext> dbFactory, PaymentsRuntime runtime,
            TenantSaleWebhookSink<TContext> sink)
        {
            this.dbFactory = dbFactory;
            this.runtime = runtime;
            this.sink = sink;
        }

        /// <summary>Hauptschalter, Feature-Gate, Verkaufsfähigkeit, Einstellungen.</summary>
        protected PaymentsRuntime Runtime => runtime;

        /// <summary>
        /// Der Name dieses Weges: <c>stripe</c>, <c>payrexx</c>, <c>wallee</c>, <c>agent</c>. Muss zu dem
        /// passen, was am Gerät steht — sonst wird das Gerät hier nicht bedient.
        /// </summary>
        protected abstract string ProviderKey { get; }

        /// <summary>Startet die Zahlung am Gerät.</summary>
        /// <remarks>
        /// Der Verkauf steht bereits, Betrag und Provision sind eingefroren. Diese Methode darf also nichts
        /// mehr rechnen — nur noch reden.
        /// </remarks>
        protected abstract Task<TerminalPaymentOutcome> StartAtDeviceAsync(TenantSale sale,
            TenantPaymentTerminal terminal, TerminalPaymentCommand command, CancellationToken cancellationToken);

        /// <summary>Fragt den Stand eines laufenden Vorgangs beim Gerät bzw. beim Anbieter nach.</summary>
        protected abstract Task<TerminalPaymentOutcome> QueryDeviceAsync(TenantSale sale,
            TenantPaymentTerminal terminal, string operationId, CancellationToken cancellationToken);

        /// <summary>Bricht einen laufenden Vorgang ab, soweit das noch geht.</summary>
        protected abstract Task<TerminalPaymentOutcome> CancelAtDeviceAsync(TenantSale sale,
            TenantPaymentTerminal terminal, string operationId, CancellationToken cancellationToken);

        /// <summary>Meldet, ob das Gerät erreichbar ist.</summary>
        protected abstract Task<TerminalStatus> QueryStatusAsync(TenantPaymentTerminal terminal,
            CancellationToken cancellationToken);

        /// <inheritdoc />
        public async Task<IReadOnlyList<TerminalInfo>> GetTerminalsAsync(int tenantId, bool includeDisabled = false,
            CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var query = db.TenantPaymentTerminals.AsNoTracking().Where(t => t.TenantId == tenantId);
            if (!includeDisabled)
            {
                query = query.Where(t => t.Enabled);
            }

            return await query.OrderBy(t => t.DisplayName)
                .Select(t => new TerminalInfo(t.TenantPaymentTerminalId, t.DisplayName, t.Provider,
                    t.ProviderTerminalId, t.Enabled))
                .ToListAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<TerminalStatus> GetTerminalStatusAsync(int tenantId, int terminalId,
            CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var terminal = await LoadTerminalAsync(db, tenantId, terminalId, cancellationToken);
            var status = await QueryStatusAsync(terminal, cancellationToken);
            if (status.Online)
            {
                terminal.LastSeenUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }

            return status;
        }

        /// <inheritdoc />
        public async Task<TerminalSaleResult> StartPaymentAsync(TerminalSaleRequest request,
            CancellationToken cancellationToken = default)
        {
            runtime.EnsureEnabled();
            await runtime.EnsureFeatureAsync(request.TenantId, cancellationToken);

            var options = runtime.Options;
            var currency = (string.IsNullOrWhiteSpace(request.Currency) ? options.DefaultCurrency : request.Currency)
                .Trim().ToUpperInvariant();
            var amountMinor = CurrencyMinorUnits.ToMinor(request.Amount, currency);
            if (amountMinor <= 0 || string.IsNullOrWhiteSpace(currency) || string.IsNullOrWhiteSpace(request.ExternalReference))
            {
                throw new TenantPaymentException(PaymentErrorCodes.InvalidAmount,
                    $"A terminal sale needs a positive amount, a currency and an external reference (got {request.Amount} {currency}, reference '{request.ExternalReference}').");
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var terminal = await LoadTerminalAsync(db, request.TenantId, request.TerminalId, cancellationToken);

            var account = await db.TenantPaymentAccounts.AsNoTracking()
                .FirstOrDefaultAsync(a => a.TenantId == request.TenantId, cancellationToken);
            // Vor JEDEM Verkauf, nicht nur beim Einrichten: ein Anbieter kann ein laufendes Konto Tage
            // spaeter einschraenken, wenn er weitere Unterlagen sehen will.
            runtime.EnsureCanSell(account);

            var reference = request.ExternalReference.Trim();
            var sale = await db.TenantSales.Include(s => s.Refunds)
                .FirstOrDefaultAsync(s => s.TenantId == request.TenantId && s.ExternalReference == reference,
                    cancellationToken);

            if (sale is { Status: TenantSaleStatus.Paid or TenantSaleStatus.Refunded or TenantSaleStatus.PartiallyRefunded })
            {
                // Schon bezahlt. Nicht noch einmal - das ist der Fall, fuer den es die Referenz gibt.
                return await DescribeAsync(sale, terminal, db, cancellationToken);
            }

            if (sale == null)
            {
                sale = new TenantSale
                {
                    TenantId = request.TenantId,
                    ExternalReference = reference,
                    Description = Trim(request.Description, 256) ?? string.Empty,
                    AmountMinor = amountMinor,
                    Currency = currency,
                    ApplicationFeeMinor = ApplicationFeeMath.Calculate(options.ApplicationFee, amountMinor, currency),
                    Status = TenantSaleStatus.Pending,
                    Provider = ProviderKey,
                    ProviderAccountId = account!.ProviderAccountId,
                    TenantPaymentTerminalId = terminal.TenantPaymentTerminalId,
                    CustomerEmail = Trim(request.CustomerEmail, 256),
                    MetadataJson = request.Metadata is { Count: > 0 }
                        ? System.Text.Json.JsonSerializer.Serialize(request.Metadata)
                        : null,
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow
                };
                db.TenantSales.Add(sale);

                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException ex)
                {
                    // Der eindeutige Index ueber (Mandant, Referenz) hat gerade ein doppeltes Absenden
                    // abgefangen - an einer Kasse der Normalfall, nicht die Ausnahme.
                    LogEnvironment.LogEvent(
                        $"Concurrent attempt to start terminal sale '{reference}' for tenant {request.TenantId}; the existing sale is reused: {ex.OutlineException()}",
                        LogSeverity.Warning, LogContext);
                    db.Entry(sale).State = EntityState.Detached;
                    sale = await db.TenantSales.Include(s => s.Refunds)
                        .FirstOrDefaultAsync(s => s.TenantId == request.TenantId && s.ExternalReference == reference,
                            cancellationToken);
                    if (sale == null)
                    {
                        // Doch nicht der Index. Was auch immer schiefging, gehoert unveraendert dem Aufrufer.
                        throw;
                    }

                    // Der Gewinner laeuft schon am Geraet. Nachfragen statt ein zweites Mal starten.
                    return await GetPaymentAsync(sale.TenantSaleId, cancellationToken);
                }
            }
            else if (sale.ProviderSessionId != null)
            {
                // Ein frueherer Anlauf dieser Bestellung ist noch offen. Nachfragen - ein zweiter Start
                // waere genau die doppelte Belastung, die alle drei Anbieter eigens zu verhindern suchen.
                return await GetPaymentAsync(sale.TenantSaleId, cancellationToken);
            }

            EnsureSameTerminal(sale, terminal);

            var command = new TerminalPaymentCommand
            {
                TerminalId = terminal.ProviderTerminalId,
                // UNSERE Kennung, stabil ueber Wiederholungen: die Verkaufszeile gibt es genau einmal je
                // Bestellung, und sie ueberlebt einen Neustart der Kasse.
                OperationId = sale.TenantSaleId.ToString(),
                AmountMinor = sale.AmountMinor,
                Currency = sale.Currency,
                Description = sale.Description,
                Reference = sale.ExternalReference,
                AllowCustomerCancellation = request.AllowCustomerCancellation,
                ConfigurationJson = terminal.ConfigurationJson
            };

            TerminalPaymentOutcome outcome;
            try
            {
                outcome = await StartAtDeviceAsync(sale, terminal, command, cancellationToken);
            }
            catch (Exception ex) when (ex is not TenantPaymentException)
            {
                // Hier NICHT auf "fehlgeschlagen" buchen. Eine Ausnahme heisst, dass wir keine Antwort
                // bekommen haben - nicht, dass nichts passiert ist. Der Verkauf bleibt offen, damit die
                // Kasse nachfragen kann; wer ihn jetzt schliesst, kassiert beim naechsten Versuch doppelt.
                LogEnvironment.LogEvent(
                    $"Starting the payment for sale {sale.TenantSaleId} on terminal {terminal.DisplayName} ({ProviderKey}/{terminal.ProviderTerminalId}) gave no answer. The sale stays open — it is NOT known whether the card was charged: {ex.OutlineException()}",
                    LogSeverity.Error, LogContext);
                return Describe(sale, terminal, new TerminalPaymentOutcome
                {
                    OperationId = command.OperationId,
                    State = TerminalPaymentState.Unknown,
                    FailureMessage = ex.Message
                });
            }

            return await ApplyAsync(sale, terminal, db, outcome, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<TerminalSaleResult> GetPaymentAsync(int tenantSaleId, CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var (sale, terminal) = await LoadSaleAsync(db, tenantSaleId, cancellationToken);

            if (sale.Status != TenantSaleStatus.Pending)
            {
                // Schon entschieden. Das Geraet zu fragen, koennte es nur noch verwirren.
                return await DescribeAsync(sale, terminal, db, cancellationToken);
            }

            TerminalPaymentOutcome outcome;
            try
            {
                outcome = await QueryDeviceAsync(sale, terminal, sale.TenantSaleId.ToString(), cancellationToken);
            }
            catch (Exception ex) when (ex is not TenantPaymentException)
            {
                LogEnvironment.LogEvent(
                    $"Could not ask terminal {terminal.DisplayName} ({ProviderKey}/{terminal.ProviderTerminalId}) about sale {sale.TenantSaleId}. The sale stays open: {ex.OutlineException()}",
                    LogSeverity.Warning, LogContext);
                return Describe(sale, terminal, new TerminalPaymentOutcome
                {
                    OperationId = sale.TenantSaleId.ToString(),
                    State = TerminalPaymentState.Unknown,
                    FailureMessage = ex.Message
                });
            }

            return await ApplyAsync(sale, terminal, db, outcome, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<TerminalSaleResult> CancelPaymentAsync(int tenantSaleId, CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var (sale, terminal) = await LoadSaleAsync(db, tenantSaleId, cancellationToken);

            if (sale.Status != TenantSaleStatus.Pending)
            {
                return await DescribeAsync(sale, terminal, db, cancellationToken);
            }

            var outcome = await CancelAtDeviceAsync(sale, terminal, sale.TenantSaleId.ToString(), cancellationToken);
            return await ApplyAsync(sale, terminal, db, outcome, cancellationToken);
        }

        /// <summary>
        /// Bucht, was das Gerät gemeldet hat.
        /// </summary>
        /// <remarks>
        /// Die einzige Stelle, an der ein Terminalvorgang den Verkauf verändert — und sie geht durch die
        /// Senke, also mit denselben Garantien wie ein Webhook.
        /// </remarks>
        private async Task<TerminalSaleResult> ApplyAsync(TenantSale sale, TenantPaymentTerminal terminal,
            TContext db, TerminalPaymentOutcome outcome, CancellationToken cancellationToken)
        {
            // Die Vorgangskennung des Anbieters festhalten, sobald sie bekannt ist: ohne sie findet ein
            // spaeterer Webhook diese Zeile nicht, und eine Erstattung hat kein Ziel.
            if (sale.ProviderSessionId == null && !string.IsNullOrWhiteSpace(outcome.ProviderPaymentId))
            {
                sale.ProviderSessionId = outcome.ProviderPaymentId;
            }

            switch (outcome.State)
            {
                case TerminalPaymentState.Succeeded:
                    WarnOnAmountMismatch(sale, terminal, outcome);
                    await sink.MarkPaidAsync(sale, db, outcome.ProviderPaymentId, sale.CustomerEmail, cancellationToken);
                    break;

                case TerminalPaymentState.Failed:
                    await sink.MoveToAsync(sale, db, TenantSaleStatus.Failed, cancellationToken);
                    break;

                case TerminalPaymentState.Canceled:
                    await sink.MoveToAsync(sale, db, TenantSaleStatus.Canceled, cancellationToken);
                    break;

                default:
                    // InProgress, Authorized, Unknown: nichts entscheiden. Bei Authorized ist der Betrag
                    // reserviert, aber nicht eingezogen - wer hier freigaebe, lieferte gegen ein Versprechen.
                    sale.Updated = DateTime.UtcNow;
                    await db.SaveChangesAsync(cancellationToken);
                    break;
            }

            return Describe(sale, terminal, outcome);
        }

        /// <summary>
        /// Meldet, wenn am Gerät ein anderer Betrag herauskam als auf der Zeile steht.
        /// </summary>
        /// <remarks>
        /// Trinkgeld ist der übliche Grund. Der Verkaufsbetrag wird <b>nicht</b> nachgezogen: er und die
        /// Provision daneben sind eingefroren, und eine Provision nachträglich auf einen anderen Betrag
        /// zu rechnen, wäre genau die stille Umschreibung historischer Verkäufe, die wir überall sonst
        /// vermeiden. Wer Trinkgeld führen will, braucht dafür eine eigene Zeile — bis dahin ist die
        /// Abweichung eine Meldung wert.
        /// </remarks>
        private void WarnOnAmountMismatch(TenantSale sale, TenantPaymentTerminal terminal, TerminalPaymentOutcome outcome)
        {
            if (outcome.AmountMinor is not { } charged || charged == sale.AmountMinor)
            {
                return;
            }

            LogEnvironment.LogEvent(
                $"Terminal {terminal.DisplayName} ({ProviderKey}/{terminal.ProviderTerminalId}) charged {charged} for sale {sale.TenantSaleId}, which is booked at {sale.AmountMinor} {sale.Currency}"
                + (outcome.TipMinor is > 0 ? $" (tip {outcome.TipMinor})" : string.Empty)
                + ". The sale amount and the frozen commission are left as they are — reconcile the difference against the provider's books.",
                LogSeverity.Warning, LogContext);
        }

        private TerminalSaleResult Describe(TenantSale sale, TenantPaymentTerminal terminal, TerminalPaymentOutcome outcome)
            => new()
            {
                TenantSaleId = sale.TenantSaleId,
                ExternalReference = sale.ExternalReference,
                TerminalId = terminal.TenantPaymentTerminalId,
                State = outcome.State,
                SaleStatus = sale.Status,
                AmountMinor = sale.AmountMinor,
                Currency = sale.Currency,
                TipMinor = outcome.TipMinor,
                FailureCode = outcome.FailureCode,
                FailureMessage = outcome.FailureMessage,
                Receipt = outcome.Receipt
            };

        /// <summary>Beschreibt einen Verkauf, der bereits entschieden ist, ohne das Gerät zu behelligen.</summary>
        private Task<TerminalSaleResult> DescribeAsync(TenantSale sale, TenantPaymentTerminal terminal, TContext db,
            CancellationToken cancellationToken)
            => Task.FromResult(Describe(sale, terminal, new TerminalPaymentOutcome
            {
                OperationId = sale.TenantSaleId.ToString(),
                State = sale.Status switch
                {
                    TenantSaleStatus.Paid or TenantSaleStatus.Refunded or TenantSaleStatus.PartiallyRefunded
                        => TerminalPaymentState.Succeeded,
                    TenantSaleStatus.Failed => TerminalPaymentState.Failed,
                    TenantSaleStatus.Canceled or TenantSaleStatus.Expired => TerminalPaymentState.Canceled,
                    _ => TerminalPaymentState.InProgress
                },
                ProviderPaymentId = sale.ProviderSessionId,
                AmountMinor = sale.AmountMinor
            }));

        /// <summary>
        /// Lädt ein Gerät und prüft, dass es benutzt werden darf.
        /// </summary>
        /// <remarks>
        /// <b>Die Mandantenprüfung ist der Grund, warum die Kasse unseren Schlüssel schickt und nicht die
        /// Kennung beim Anbieter.</b> Käme die aus der Anfrage, liesse sich auf einem fremden Gerät
        /// kassieren — das Geld landete auf dem Konto eines anderen Mandanten.
        /// </remarks>
        private async Task<TenantPaymentTerminal> LoadTerminalAsync(TContext db, int tenantId, int terminalId,
            CancellationToken cancellationToken)
        {
            var terminal = await db.TenantPaymentTerminals
                               .FirstOrDefaultAsync(t => t.TenantPaymentTerminalId == terminalId, cancellationToken)
                           ?? throw new TenantPaymentException(PaymentErrorCodes.SaleNotFound,
                               $"Terminal {terminalId} is not registered.");

            if (terminal.TenantId != tenantId)
            {
                // Nicht als "nicht gefunden" durchgehen lassen: das hier ist entweder ein Fehler in der
                // Kasse oder ein Versuch, und beides gehoert gesehen.
                LogEnvironment.LogEvent(
                    $"Tenant {tenantId} tried to use terminal {terminalId}, which belongs to tenant {terminal.TenantId}. Refused.",
                    LogSeverity.Error, LogContext);
                throw new TenantPaymentException(PaymentErrorCodes.SaleNotFound,
                    $"Terminal {terminalId} does not belong to tenant {tenantId}.");
            }

            if (!terminal.Enabled)
            {
                throw new TenantPaymentException(PaymentErrorCodes.ProviderError,
                    $"Terminal '{terminal.DisplayName}' is switched off and cannot be used.");
            }

            if (!string.Equals(terminal.Provider, ProviderKey, StringComparison.OrdinalIgnoreCase))
            {
                throw new TenantPaymentException(PaymentErrorCodes.ProviderError,
                    $"Terminal '{terminal.DisplayName}' is registered for provider '{terminal.Provider}', not '{ProviderKey}'.");
            }

            return terminal;
        }

        private async Task<(TenantSale Sale, TenantPaymentTerminal Terminal)> LoadSaleAsync(TContext db,
            int tenantSaleId, CancellationToken cancellationToken)
        {
            var sale = await db.TenantSales.Include(s => s.Refunds)
                           .FirstOrDefaultAsync(s => s.TenantSaleId == tenantSaleId, cancellationToken)
                       ?? throw new TenantPaymentException(PaymentErrorCodes.SaleNotFound,
                           $"Sale {tenantSaleId} not found.");

            if (sale.TenantPaymentTerminalId is not { } terminalId)
            {
                throw new TenantPaymentException(PaymentErrorCodes.SaleNotFound,
                    $"Sale {tenantSaleId} was not made at a terminal.");
            }

            return (sale, await LoadTerminalAsync(db, sale.TenantId, terminalId, cancellationToken));
        }

        /// <summary>
        /// Stellt sicher, dass eine wiederholte Bestellung nicht plötzlich an einem anderen Gerät läuft.
        /// </summary>
        /// <remarks>
        /// Sonst stünde auf der Zeile ein Gerät und der Vorgang liefe an einem zweiten — und die Frage
        /// „wurde die Karte belastet?" wäre an der falschen Kasse gestellt.
        /// </remarks>
        private static void EnsureSameTerminal(TenantSale sale, TenantPaymentTerminal terminal)
        {
            if (sale.TenantPaymentTerminalId is { } existing && existing != terminal.TenantPaymentTerminalId)
            {
                throw new TenantPaymentException(PaymentErrorCodes.ProviderError,
                    $"Sale {sale.TenantSaleId} was started on terminal {existing} and cannot be continued on terminal {terminal.TenantPaymentTerminalId}. Ask the first terminal about it, or use a new reference.");
            }

            sale.TenantPaymentTerminalId = terminal.TenantPaymentTerminalId;
        }

        private static string? Trim(string? value, int max)
            => string.IsNullOrWhiteSpace(value) ? null : value.Length <= max ? value : value[..max];

        /// <summary>Die Protokoll-Kategorie, dieselbe wie bei den übrigen Zahlungswegen.</summary>
        protected const string LogContext = "TenantPayments";
    }
}
