using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.Stripe.Payments.Abstractions;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.EntityFrameworkCore;
using Stripe;
// Alias und nicht "using Stripe.V2.Core": eine using-Direktive importiert die TYPEN eines Namespace,
// nicht seine verschachtelten Namespaces - "V2.Core.X" waere sonst unaufloesbar. Die Typen direkt zu
// importieren ginge auch nicht, weil v1 und v2 dieselben Namen tragen (Account, AccountCreateOptions).
using V2 = Stripe.V2;
// Aus demselben Grund: Stripe.Events ist ein verschachtelter Namespace.
using Events = Stripe.Events;
using Stripe.Checkout;

namespace ITVComponents.WebCoreToolkit.Billing.Stripe.Payments.Impl
{
    /// <summary>
    /// Processes the events of connected accounts. Its own endpoint with its own signing secret, because the
    /// provider treats "events on connected accounts" as a separate subscription — sharing the platform endpoint
    /// would mean trying two secrets blindly on every request and never knowing which one was meant.
    /// <para>
    /// Two rules run through everything here. First, an event's account is checked against the account we have
    /// on file: an event for a FOREIGN account must never touch a sale. Second, status only ever moves forward —
    /// delivery is at-least-once and unordered, so a late event must not undo a newer state.
    /// </para>
    /// </summary>
    public class StripeConnectWebhookHandler<TContext> : IStripeConnectWebhookHandler
        where TContext : DbContext, IPaymentsContext
    {
        private readonly IDbContextFactory<TContext> dbFactory;
        /// <summary>Concrete, not the interface: the account mirror reads through <c>StripeClient.V2</c>.</summary>
        private readonly StripeClient client;
        private readonly IGlobalSettings<TenantPaymentsOptions> settings;
        private readonly TenantSaleNotifier notifier;

        public StripeConnectWebhookHandler(IDbContextFactory<TContext> dbFactory, StripeClient client,
            IGlobalSettings<TenantPaymentsOptions> settings, IEnumerable<ITenantSaleObserver> observers)
        {
            this.dbFactory = dbFactory;
            this.client = client;
            this.settings = settings;
            notifier = new TenantSaleNotifier(observers);
        }

        /// <inheritdoc />
        public async Task HandleAsync(string payload, string signatureHeader, CancellationToken cancellationToken = default)
        {
            var options = settings.Value;

            // Two generations arrive at this endpoint. Sales and refunds are v1 events and stay that way; the
            // connected ACCOUNT is created through v2 and reports itself through v2 event notifications, which
            // are a different shape with a different parser. The payload says which it is - v1 carries
            // "object": "event", v2 carries "object": "v2.core.event" - and guessing by trying one parser and
            // catching its exception would swallow a genuine signature failure along the way.
            if (IsV2Notification(payload))
            {
                await HandleAccountNotificationAsync(payload, signatureHeader, options, cancellationToken);
                return;
            }

            var stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader, options.Stripe.ConnectWebhookSecret);
            var accountId = stripeEvent.Account;

            switch (stripeEvent.Type)
            {
                case EventTypes.AccountUpdated:
                    // Kept although connected accounts are v2 now and report through v2 notifications: should
                    // the provider still emit this for a v2 account, it is a perfectly good trigger. The payload
                    // is not used, only its id - it carries the account in its v1 shape, which no longer holds
                    // what the mirror is made of. The event is the trigger; the truth is fetched.
                    if (stripeEvent.Data.Object is Account account)
                    {
                        await MirrorAccountAsync(account.Id, cancellationToken);
                    }

                    break;
                case EventTypes.AccountApplicationDeauthorized:
                    await MarkDisconnectedAsync(accountId, cancellationToken);
                    break;
                case EventTypes.CheckoutSessionCompleted:
                    if (stripeEvent.Data.Object is Session completed)
                    {
                        await MarkPaidAsync(completed, accountId, options, cancellationToken);
                    }

                    break;
                case EventTypes.CheckoutSessionExpired:
                    if (stripeEvent.Data.Object is Session expired)
                    {
                        await MoveToAsync(expired.ClientReferenceId, expired.PaymentIntentId, accountId, TenantSaleStatus.Expired, cancellationToken);
                    }

                    break;
                case EventTypes.PaymentIntentPaymentFailed:
                    if (stripeEvent.Data.Object is PaymentIntent failed)
                    {
                        await MoveToAsync(null, failed.Id, accountId, TenantSaleStatus.Failed, cancellationToken);
                    }

                    break;
                case EventTypes.ChargeRefunded:
                    if (stripeEvent.Data.Object is Charge charge)
                    {
                        await MirrorRefundsAsync(charge, accountId, cancellationToken);
                    }

                    break;
            }
        }

        /// <summary>
        /// Ist diese Nutzlast ein v2-Ereignis? Entschieden am Feld <c>object</c>, ohne die Signatur zu pruefen -
        /// das tut gleich der richtige Parser. Gelesen wird nur dieses eine Feld; alles andere waere doppelte
        /// Arbeit an einer Nachricht, die vielleicht gar nicht echt ist.
        /// </summary>
        private static bool IsV2Notification(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return false;
            }

            try
            {
                using var doc = JsonDocument.Parse(payload);
                return doc.RootElement.TryGetProperty("object", out var kind)
                       && kind.ValueKind == JsonValueKind.String
                       && kind.GetString()?.StartsWith("v2.", StringComparison.Ordinal) == true;
            }
            catch (JsonException ex)
            {
                // Keine gueltige JSON-Nutzlast. Der v1-Weg lehnt sie gleich ab; hier faellt nur die Entscheidung,
                // welcher Weg das tut - aber stillschweigend darf das nicht passieren.
                LogEnvironment.LogEvent(
                    $"Could not read the connect webhook payload well enough to tell v1 from v2; treating it as v1: {ex.OutlineException()}",
                    LogSeverity.Warning, "StripeConnect");
                return false;
            }
        }

        /// <summary>
        /// Verarbeitet ein v2-Ereignis zum verbundenen Konto.
        /// <para>
        /// v2-Ereignisse sind duenn: sie tragen nur einen Verweis auf das betroffene Objekt, nicht das Objekt
        /// selbst. Das passt hier gut, weil der Spiegel das Konto ohnehin selbst liest - es aendert sich nur,
        /// woran der Ausloeser erkannt wird. Dass es SO viele Ereignisarten gibt, liegt daran, dass v2 je
        /// Abschnitt des Kontos meldet; unsere Antwort ist auf alle dieselbe.
        /// </para>
        /// </summary>
        private async Task HandleAccountNotificationAsync(string payload, string signatureHeader,
            TenantPaymentsOptions options, CancellationToken cancellationToken)
        {
            // Ein v2-Ereignisziel hat sein eigenes Geheimnis. Wer beides auf denselben Endpunkt legt und
            // dasselbe Geheimnis benutzt, kommt ohne die zweite Einstellung aus - deshalb der Rueckfall.
            var secret = string.IsNullOrWhiteSpace(options.Stripe.ConnectV2WebhookSecret)
                ? options.Stripe.ConnectWebhookSecret
                : options.Stripe.ConnectV2WebhookSecret;

            V2.Core.EventNotification notification;
            try
            {
                notification = client.ParseEventNotification(payload, signatureHeader, secret);
            }
            catch (StripeException ex)
            {
                // Eine abgelehnte Signatur ist entweder ein falsch eingetragenes Geheimnis oder etwas, das gar
                // nicht von Stripe kommt. Beides muss man sehen koennen - und die haeufigste Ursache benennen.
                LogEnvironment.LogEvent(
                    $"A v2 connect notification could not be verified. Check that the event destination's signing secret is in TenantPayments.Stripe.ConnectV2WebhookSecret - it is NOT the same secret as the v1 connect endpoint: {ex.OutlineException()}",
                    LogSeverity.Error, "StripeConnect");
                throw;
            }

            if (IsAccountClosed(notification))
            {
                await MarkDisconnectedAsync(AccountIdOf(notification), cancellationToken);
                return;
            }

            var accountId = AccountIdOf(notification);
            if (string.IsNullOrEmpty(accountId))
            {
                // Etwas, das dieses Toolkit nicht auswertet - Personen, Ereignisziel-Pings, spaeter
                // Hinzugekommenes. Kein Fehler, aber nachvollziehbar, damit "es passiert nichts" eine Ursache hat.
                LogEnvironment.LogEvent(
                    $"v2 connect notification '{notification.Type}' carries no connected account this toolkit mirrors; ignored.",
                    LogSeverity.Report, "StripeConnect");
                return;
            }

            await MirrorAccountAsync(accountId, cancellationToken);
        }

        /// <summary>
        /// Die Kennung des betroffenen Kontos. Der Verweis sitzt erst an den abgeleiteten Arten, nicht an der
        /// Basis - darum die Aufzaehlung. Ausgeschrieben und nicht ueber Reflexion: so faellt beim Uebersetzen
        /// auf, wenn eine Art wegfaellt, statt im Betrieb als ausbleibende Aktualisierung.
        /// </summary>
        private static string? AccountIdOf(V2.Core.EventNotification notification) => notification switch
        {
            Events.V2CoreAccountUpdatedEventNotification n => n.RelatedObject?.Id,
            Events.V2CoreAccountClosedEventNotification n => n.RelatedObject?.Id,
            Events.V2CoreAccountIncludingConfigurationMerchantUpdatedEventNotification n => n.RelatedObject?.Id,
            Events.V2CoreAccountIncludingConfigurationMerchantCapabilityStatusUpdatedEventNotification n => n.RelatedObject?.Id,
            Events.V2CoreAccountIncludingConfigurationRecipientUpdatedEventNotification n => n.RelatedObject?.Id,
            Events.V2CoreAccountIncludingConfigurationRecipientCapabilityStatusUpdatedEventNotification n => n.RelatedObject?.Id,
            Events.V2CoreAccountIncludingRequirementsUpdatedEventNotification n => n.RelatedObject?.Id,
            Events.V2CoreAccountIncludingFutureRequirementsUpdatedEventNotification n => n.RelatedObject?.Id,
            Events.V2CoreAccountIncludingIdentityUpdatedEventNotification n => n.RelatedObject?.Id,
            Events.V2CoreAccountIncludingDefaultsUpdatedEventNotification n => n.RelatedObject?.Id,
            _ => null
        };

        /// <summary>Ein geschlossenes Konto ist fuer uns dasselbe wie ein getrenntes: es kassiert nichts mehr.</summary>
        private static bool IsAccountClosed(V2.Core.EventNotification notification)
            => notification is Events.V2CoreAccountClosedEventNotification;

        /// <summary>
        /// Keeps the local account mirror in step — an account that works today can be restricted tomorrow.
        /// <para>
        /// Takes the id and fetches the account itself instead of mirroring what the event brought. Connected
        /// accounts are created through the v2 API, and only a v2 read returns the configurations and their
        /// capability statuses that the mirror consists of; the v1 payload of this event would leave every one of
        /// them empty and make a working account look dead.
        /// </para>
        /// </summary>
        private async Task MirrorAccountAsync(string accountId, CancellationToken cancellationToken)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var local = await db.TenantPaymentAccounts.FirstOrDefaultAsync(a => a.ProviderAccountId == accountId, cancellationToken);
            if (local == null)
            {
                // An account we never created (or one left over from the other provider mode). Nothing to
                // mirror, but worth seeing: it usually means test and live keys were swapped.
                LogEnvironment.LogEvent(
                    $"Connect event for the unknown account {accountId} — no local payout account matches it. Ignored.",
                    LogSeverity.Warning, "StripeConnect");
                return;
            }

            try
            {
                var remote = await client.V2.Core.Accounts.GetAsync(accountId,
                    new V2.Core.AccountGetOptions { Include = ConnectAccountMirror.MirroredSections },
                    cancellationToken: cancellationToken);
                ConnectAccountMirror.Apply(local, remote);
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (StripeException ex)
            {
                // The mirror stays as it was. That is the safe direction - a capability that was withdrawn keeps
                // reading as withdrawn - but it must not be silent: the next state change is then only picked up
                // by an explicit refresh, and nobody would know why.
                LogEnvironment.LogEvent(
                    $"Could not read the connected account {accountId} after its update event; the local mirror stays unchanged: {ex.OutlineException()}",
                    LogSeverity.Error, "StripeConnect");
            }
        }

        /// <summary>
        /// The tenant (or the provider) cut the link. The account keeps existing at the provider but is out of
        /// our reach, so selling stops immediately rather than failing one payment at a time.
        /// </summary>
        private async Task MarkDisconnectedAsync(string? accountId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(accountId))
            {
                return;
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var local = await db.TenantPaymentAccounts.FirstOrDefaultAsync(a => a.ProviderAccountId == accountId, cancellationToken);
            if (local == null)
            {
                return;
            }

            local.Disconnected = true;
            local.ChargesEnabled = false;
            local.PayoutsEnabled = false;
            local.Updated = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            LogEnvironment.LogEvent(
                $"The connected account {accountId} of tenant {local.TenantId} was deauthorized; further sales are refused until a new payout account is set up.",
                LogSeverity.Warning, "StripeConnect");
        }

        /// <summary>Pending -&gt; Paid, and the ONE place the completion observers are called.</summary>
        private async Task MarkPaidAsync(Session session, string? accountId, TenantPaymentsOptions options,
            CancellationToken cancellationToken)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var sale = await ResolveSaleAsync(db, session.ClientReferenceId, session.PaymentIntentId, session.Id, accountId, cancellationToken);
            if (sale == null)
            {
                return;
            }

            // The payload may be an older snapshot; the current session decides.
            var current = session;
            try
            {
                current = await new SessionService(client).GetAsync(session.Id,
                    cancellationToken: cancellationToken,
                    requestOptions: PaymentsRuntime.ForAccount(accountId ?? sale.ProviderAccountId!));
            }
            catch (StripeException ex)
            {
                LogEnvironment.LogEvent(
                    $"Could not re-read the checkout session {session.Id} of sale {sale.TenantSaleId}; the event payload is used instead: {ex.OutlineException()}",
                    LogSeverity.Warning, "StripeConnect");
            }

            if (!string.Equals(current.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(current.PaymentStatus, "no_payment_required", StringComparison.OrdinalIgnoreCase))
            {
                // A completed session whose payment is still processing (delayed payment methods). It arrives
                // again as async_payment_succeeded; nothing is released on a maybe.
                LogEnvironment.LogEvent(
                    $"Checkout session {current.Id} of sale {sale.TenantSaleId} completed with payment status '{current.PaymentStatus}'; the sale stays pending until the payment settles.",
                    LogSeverity.Report, "StripeConnect");
                return;
            }

            sale.ProviderPaymentIntentId ??= current.PaymentIntentId;
            sale.ProviderChargeId ??= await ResolveChargeAsync(current.PaymentIntentId, accountId ?? sale.ProviderAccountId, cancellationToken);
            CaptureCustomerEmail(sale, current, options);

            if (sale.Status != TenantSaleStatus.Pending)
            {
                // Delivered more than once. The identifiers above may still be new, so they are saved — but the
                // order must not be released a second time.
                sale.Updated = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            sale.Status = TenantSaleStatus.Paid;
            sale.PaidUtc = DateTime.UtcNow;
            sale.CheckoutUrl = null;
            sale.Updated = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            await notifier.NotifyCompletedAsync(sale, cancellationToken);
        }

        /// <summary>Moves an unpaid sale to a terminal state. Never touches one that has already been paid.</summary>
        private async Task MoveToAsync(string? clientReferenceId, string? paymentIntentId, string? accountId, TenantSaleStatus status, CancellationToken cancellationToken)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var sale = await ResolveSaleAsync(db, clientReferenceId, paymentIntentId, null, accountId, cancellationToken);
            if (sale is not { Status: TenantSaleStatus.Pending })
            {
                return;
            }

            sale.Status = status;
            sale.CheckoutUrl = null;
            sale.Updated = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Mirrors refunds the provider knows about but we do not — a refund issued straight from the provider
        /// dashboard arrives only this way. Refunds we booked ourselves are already on file and are skipped,
        /// which is also what keeps the observers from firing twice.
        /// </summary>
        private async Task MirrorRefundsAsync(Charge charge, string? accountId, CancellationToken cancellationToken)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var sale = await db.TenantSales.Include(s => s.Refunds)
                .FirstOrDefaultAsync(s => s.ProviderChargeId == charge.Id, cancellationToken);
            sale ??= await ResolveSaleAsync(db, null, charge.PaymentIntentId, null, accountId, cancellationToken);
            if (sale == null)
            {
                return;
            }

            if (!BelongsToAccount(sale, accountId))
            {
                return;
            }

            sale.ProviderChargeId ??= charge.Id;
            var known = new HashSet<string>(sale.Refunds.Where(r => r.ProviderRefundId != null).Select(r => r.ProviderRefundId!), StringComparer.Ordinal);
            var fresh = (charge.Refunds?.Data ?? new List<Refund>()).Where(r => !known.Contains(r.Id)).ToList();
            if (fresh.Count == 0)
            {
                sale.Updated = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            // How much of the commission the provider actually gave back. Read from the platform's application
            // fee rather than guessed: whether a dashboard refund returned the fee is not visible on the refund.
            var feeRefundedTotal = await ReadRefundedApplicationFeeAsync(charge, sale, cancellationToken);
            var feeAlreadyRecorded = sale.Refunds.Sum(r => r.ApplicationFeeRefundedMinor);
            var feeToDistribute = Math.Max(0, feeRefundedTotal - feeAlreadyRecorded);

            var added = new List<TenantSaleRefund>();
            foreach (var refund in fresh.OrderBy(r => r.Created))
            {
                var share = fresh.Count == 1
                    ? feeToDistribute
                    : Math.Min(feeToDistribute, ApplicationFeeMath.ProportionalRefund(sale.ApplicationFeeMinor, refund.Amount, sale.AmountMinor));
                feeToDistribute -= share;

                var row = new TenantSaleRefund
                {
                    TenantSaleId = sale.TenantSaleId,
                    AmountMinor = refund.Amount,
                    ApplicationFeeRefundedMinor = share,
                    ProviderRefundId = refund.Id,
                    Reason = refund.Reason,
                    Status = refund.Status,
                    Created = refund.Created
                };
                sale.Refunds.Add(row);
                added.Add(row);
            }

            sale.Status = TenantSaleService<TContext>.DeriveStatus(sale.AmountMinor, sale.Refunds.Sum(r => r.AmountMinor));
            sale.Updated = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            foreach (var row in added)
            {
                await notifier.NotifyRefundedAsync(sale, row, cancellationToken);
            }
        }

        /// <summary>
        /// Total commission returned for this charge, in minor units. The application fee lives on the PLATFORM
        /// account (no account header), which is why this is a separate lookup and not a field on the charge.
        /// </summary>
        private async Task<long> ReadRefundedApplicationFeeAsync(Charge charge, TenantSale sale, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(charge.ApplicationFeeId) || sale.ApplicationFeeMinor <= 0)
            {
                return 0;
            }

            try
            {
                var fee = await new ApplicationFeeService(client).GetAsync(charge.ApplicationFeeId, cancellationToken: cancellationToken);
                return Math.Clamp(fee.AmountRefunded, 0, sale.ApplicationFeeMinor);
            }
            catch (StripeException ex)
            {
                // Falling back to the provider's documented proportional rule. Logged, because a wrong number
                // here shows up as a commission statement nobody can reconcile.
                LogEnvironment.LogEvent(
                    $"Could not read the application fee {charge.ApplicationFeeId} for sale {sale.TenantSaleId}; the returned commission is estimated proportionally: {ex.OutlineException()}",
                    LogSeverity.Warning, "StripeConnect");
                return ApplicationFeeMath.ProportionalRefund(sale.ApplicationFeeMinor, charge.AmountRefunded, sale.AmountMinor);
            }
        }

        /// <summary>Charge behind a payment intent — the identifier a refund is issued against.</summary>
        private async Task<string?> ResolveChargeAsync(string? paymentIntentId, string? accountId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(paymentIntentId) || string.IsNullOrEmpty(accountId))
            {
                return null;
            }

            try
            {
                var intent = await new PaymentIntentService(client).GetAsync(paymentIntentId,
                    cancellationToken: cancellationToken,
                    requestOptions: PaymentsRuntime.ForAccount(accountId));
                return intent.LatestChargeId;
            }
            catch (StripeException ex)
            {
                // Not fatal for the payment, but it costs the ability to refund from the interface later.
                LogEnvironment.LogEvent(
                    $"Could not read the charge of payment intent {paymentIntentId} on account {accountId}; the sale is paid but has no charge on record: {ex.OutlineException()}",
                    LogSeverity.Warning, "StripeConnect");
                return null;
            }
        }

        /// <summary>
        /// Finds the sale an event belongs to: by the client reference we stamped on the session first, then by
        /// the provider identifiers. The account is verified in every case.
        /// </summary>
        private async Task<TenantSale?> ResolveSaleAsync(TContext db, string? clientReferenceId, string? paymentIntentId, string? sessionId, string? accountId, CancellationToken cancellationToken)
        {
            TenantSale? sale = null;
            if (int.TryParse(clientReferenceId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var saleId))
            {
                sale = await db.TenantSales.Include(s => s.Refunds).FirstOrDefaultAsync(s => s.TenantSaleId == saleId, cancellationToken);
            }

            if (sale == null && !string.IsNullOrEmpty(paymentIntentId))
            {
                sale = await db.TenantSales.Include(s => s.Refunds).FirstOrDefaultAsync(s => s.ProviderPaymentIntentId == paymentIntentId, cancellationToken);
            }

            if (sale == null && !string.IsNullOrEmpty(sessionId))
            {
                sale = await db.TenantSales.Include(s => s.Refunds).FirstOrDefaultAsync(s => s.ProviderSessionId == sessionId, cancellationToken);
            }

            if (sale == null)
            {
                return null;
            }

            return BelongsToAccount(sale, accountId) ? sale : null;
        }

        /// <summary>
        /// Takes the e-mail the end customer entered on the provider's payment page onto the sale, when the
        /// deployment asked for it (<see cref="TenantPaymentsOptions.CaptureCustomerEmail"/>, off by default).
        /// </summary>
        /// <remarks>
        /// <para>
        /// The session is the one re-read at the start of <c>MarkPaidAsync</c>, so this costs no extra call.
        /// </para>
        /// <para>
        /// <b>Fills, never overwrites.</b> An address the host set at creation is the host's statement —
        /// something may be attached to it that a correction typed on the payment page cannot know about.
        /// </para>
        /// <para>
        /// <b>The address itself is never written to the log</b>, here or anywhere else in this path: the whole
        /// point of the switch is that this datum does not travel further than the deployment asked for.
        /// </para>
        /// </remarks>
        private static void CaptureCustomerEmail(TenantSale sale, Session current, TenantPaymentsOptions options)
        {
            if (!options.CaptureCustomerEmail || !string.IsNullOrWhiteSpace(sale.CustomerEmail))
            {
                return;
            }

            var captured = Trim(current.CustomerDetails?.Email, 256);
            if (captured == null)
            {
                // The deployment switched this on and expects an address; saying nothing here would leave the
                // empty column looking like the capture was never wired up.
                LogEnvironment.LogEvent(
                    $"Sale {sale.TenantSaleId}: customer-email capture is on, but the checkout session {current.Id} carries no customer e-mail.",
                    LogSeverity.Report, "StripeConnect");
                return;
            }

            sale.CustomerEmail = captured;
        }

        private static string? Trim(string? value, int max)
            => string.IsNullOrWhiteSpace(value) ? null : value.Length <= max ? value : value[..max];

        /// <summary>
        /// Guards against an event of one connected account changing another tenant's sale. A mismatch is not a
        /// routine skip — it means either a misconfigured endpoint or a genuinely wrong attribution.
        /// </summary>
        private static bool BelongsToAccount(TenantSale sale, string? accountId)
        {
            if (string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(sale.ProviderAccountId)
                || string.Equals(sale.ProviderAccountId, accountId, StringComparison.Ordinal))
            {
                return true;
            }

            LogEnvironment.LogEvent(
                $"Connect event for account {accountId} matched sale {sale.TenantSaleId} of tenant {sale.TenantId}, which belongs to account {sale.ProviderAccountId}. The sale is NOT touched.",
                LogSeverity.Error, "StripeConnect");
            return false;
        }
    }
}
