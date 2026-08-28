using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.Stripe.Payments.Abstractions;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace ITVComponents.WebCoreToolkit.Billing.Stripe.Payments.Impl
{
    /// <summary>
    /// Waives the subscription base fee when the tenant's own turnover reached the threshold. The only place
    /// where axis B feeds back into axis A, hence the double context constraint.
    /// <para>
    /// Two things make this trickier than it reads. Subscription invoices are raised IN ADVANCE, so at invoice
    /// time nobody knows the turnover of the period being billed — the waiver is therefore earned backwards
    /// (over the period that just closed) and granted forwards. And the window between invoice creation and its
    /// finalization is roughly an hour: miss it and the invoice is immutable, which is why an ungranted credit
    /// is carried over to the next one instead of being lost.
    /// </para>
    /// The credit is a NEGATIVE invoice item, never a coupon: a coupon would discount the whole invoice, so a
    /// larger plan sitting next to the payments add-on would be given away with it.
    /// </summary>
    public class VolumeWaiverProcessor<TContext> : IVolumeWaiverProcessor
        where TContext : DbContext, IPaymentsContext, IBillingContext
    {
        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly IStripeClient client;
        private readonly IGlobalSettings<StripePaymentsOptions> settings;

        public VolumeWaiverProcessor(IDbContextFactory<TContext> dbFactory, IStripeClient client, IGlobalSettings<StripePaymentsOptions> settings)
        {
            this.dbFactory = dbFactory;
            this.client = client;
            this.settings = settings;
        }

        /// <inheritdoc />
        public async Task ProcessInvoiceCreatedAsync(string providerInvoiceId, CancellationToken cancellationToken = default)
        {
            var options = settings.Value;
            if (!options.VolumeWaiver.Enabled || string.IsNullOrEmpty(providerInvoiceId))
            {
                return;
            }

            Invoice invoice;
            try
            {
                invoice = await new InvoiceService(client).GetAsync(providerInvoiceId,
                    new InvoiceGetOptions { Expand = new List<string> { "lines.data" } }, cancellationToken: cancellationToken);
            }
            catch (StripeException ex)
            {
                LogEnvironment.LogEvent(
                    $"Could not read invoice {providerInvoiceId} for the volume waiver; no waiver is granted on it: {ex.OutlineException()}",
                    LogSeverity.Error, "StripeWaiver");
                return;
            }

            var subscriptionId = invoice.Parent?.SubscriptionDetails?.SubscriptionId;
            if (string.IsNullOrEmpty(subscriptionId))
            {
                return; // not a subscription invoice — nothing this feature is about
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var subscription = await db.TenantSubscriptions.AsNoTracking()
                .FirstOrDefaultAsync(s => s.ProviderSubscriptionId == subscriptionId, cancellationToken);
            var tenantId = subscription?.TenantId ?? ParseTenantId(invoice.Parent?.SubscriptionDetails?.Metadata);
            if (tenantId == null)
            {
                LogEnvironment.LogEvent(
                    $"Invoice {providerInvoiceId} belongs to subscription {subscriptionId}, which cannot be attributed to a tenant; no waiver is evaluated.",
                    LogSeverity.Warning, "StripeWaiver");
                return;
            }

            // Delivered more than once, and a second credit line is money given away. The unique index is the
            // hard guarantee; this read keeps the normal case from even trying.
            if (await db.TenantFeeWaivers.AnyAsync(w => w.TenantId == tenantId.Value && w.ProviderInvoiceId == providerInvoiceId, cancellationToken))
            {
                return;
            }

            if (string.Equals(invoice.BillingReason, "subscription_create", StringComparison.OrdinalIgnoreCase))
            {
                // The very first invoice covers the first period, and there is no closed period behind it. This
                // is not an edge case to work around — it is the direct consequence of billing in advance, and
                // it belongs in the wording on the page as much as here.
                return;
            }

            var currency = invoice.Currency?.ToUpperInvariant();
            var waiverOptions = VolumeWaiver.Resolve(options.VolumeWaiver, currency);
            var position = await FindWaivablePositionAsync(db, invoice, waiverOptions, cancellationToken);
            if (position == null)
            {
                return;
            }

            if (waiverOptions.Mode == WaiverMode.Sliding && !VolumeWaiver.HasUsableBand(waiverOptions))
            {
                LogEnvironment.LogEvent(
                    $"The sliding waiver band [{waiverOptions.WaiverRampStartMinor}, {waiverOptions.ThresholdMinor}] is not usable; the hard threshold is applied instead. A quietly wrong waiver would be worse than a visibly conservative one.",
                    LogSeverity.Warning, "StripeWaiver");
            }
            else if (waiverOptions.Mode == WaiverMode.Sliding
                     && !VolumeWaiver.IsBandWideEnough(waiverOptions.ThresholdMinor, waiverOptions.WaiverRampStartMinor, options.ApplicationFee.PercentBasisPoints, position.AmountMinor))
            {
                // A band narrower than base fee / rate does not remove the earnings dent, it only stretches it.
                // Accepting that in silence would mean rolling out a price model that does not do what the mode
                // promises.
                LogEnvironment.LogEvent(
                    $"The sliding waiver band is too narrow: (threshold {waiverOptions.ThresholdMinor} - ramp {waiverOptions.WaiverRampStartMinor}) x {options.ApplicationFee.PercentBasisPoints}bp does not reach the base fee of {position.AmountMinor}. The revenue dent is stretched, not removed.",
                    LogSeverity.Warning, "StripeWaiver");
            }

            var (periodStart, periodEnd) = ClosedPeriod(invoice);
            var netVolume = await NetVolumeAsync(db, tenantId.Value, currency, periodStart, periodEnd, cancellationToken);
            var waived = VolumeWaiver.Waived(waiverOptions, netVolume, position.AmountMinor);

            // Anything carried over from an invoice we could not reach in time rides along on this one.
            var carryOver = await db.TenantFeeWaivers
                .Where(w => w.TenantId == tenantId.Value && w.CarryOverPending)
                .ToListAsync(cancellationToken);
            var carryOverAmount = carryOver.Sum(w => w.WaivedAmountMinor);

            var record = new TenantFeeWaiver
            {
                TenantId = tenantId.Value,
                PeriodStartUtc = periodStart,
                PeriodEndUtc = periodEnd,
                NetVolumeMinor = netVolume,
                ThresholdMinor = waiverOptions.ThresholdMinor,
                Currency = currency,
                Granted = waived > 0,
                WaivedAmountMinor = waived,
                ProviderInvoiceId = providerInvoiceId,
                Created = DateTime.UtcNow
            };

            var total = waived + carryOverAmount;
            if (total > 0)
            {
                var itemId = await BookCreditAsync(invoice, total, waived, carryOverAmount, netVolume, periodStart, periodEnd, cancellationToken);
                if (itemId == null)
                {
                    // The window closed before we got there. The decision stays on record and is settled on the
                    // next invoice — planned for from the start, because "the webhook was late" is a normal day.
                    record.CarryOverPending = waived > 0;
                    LogEnvironment.LogEvent(
                        $"The waiver of {total} {currency} for tenant {tenantId} could not be placed on invoice {providerInvoiceId}; it is carried over to the next invoice.",
                        LogSeverity.Warning, "StripeWaiver");
                }
                else
                {
                    record.ProviderInvoiceItemId = itemId;
                    foreach (var pending in carryOver)
                    {
                        pending.CarryOverPending = false;
                    }
                }
            }

            db.TenantFeeWaivers.Add(record);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                // The unique index caught a concurrent delivery of the same event. The other run booked the
                // credit; ours must not add a second one.
                LogEnvironment.LogEvent(
                    $"A waiver record for tenant {tenantId} and invoice {providerInvoiceId} already exists; this evaluation is discarded: {ex.OutlineException()}",
                    LogSeverity.Warning, "StripeWaiver");
            }
        }

        /// <summary>
        /// Books the credit as a negative invoice item on the very invoice being created. Returns null when the
        /// provider refuses — which in practice means the invoice was finalized in the meantime.
        /// </summary>
        private async Task<string?> BookCreditAsync(Invoice invoice, long total, long current, long carryOver, long netVolume, DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken)
        {
            var currency = invoice.Currency;
            var description = carryOver > 0
                ? $"Base fee waived — turnover {CurrencyMinorUnits.ToMajor(netVolume, currency):0.00} {currency?.ToUpperInvariant()} between {periodStart:d} and {periodEnd:d}, including {CurrencyMinorUnits.ToMajor(carryOver, currency):0.00} carried over"
                : $"Base fee waived — turnover {CurrencyMinorUnits.ToMajor(netVolume, currency):0.00} {currency?.ToUpperInvariant()} between {periodStart:d} and {periodEnd:d}";

            try
            {
                var item = await new InvoiceItemService(client).CreateAsync(new InvoiceItemCreateOptions
                {
                    Customer = invoice.CustomerId,
                    Invoice = invoice.Id,
                    Amount = -total,
                    Currency = currency,
                    Description = description
                }, cancellationToken: cancellationToken);
                return item.Id;
            }
            catch (StripeException ex)
            {
                LogEnvironment.LogEvent(
                    $"Could not book the waiver credit of {total} {currency} on invoice {invoice.Id} (current {current}, carried over {carryOver}): {ex.OutlineException()}",
                    LogSeverity.Error, "StripeWaiver");
                return null;
            }
        }

        /// <summary>
        /// The invoice position that may be waived: the line whose price belongs to a plan or add-on named in
        /// <c>WaivablePlanKeys</c>. Its ACTUAL amount is used, not the list price — on a mid-period plan change
        /// the position is prorated, and crediting the list price would hand out more than was charged.
        /// </summary>
        private async Task<WaivablePosition?> FindWaivablePositionAsync(TContext db, Invoice invoice, VolumeWaiverOptions waiverOptions, CancellationToken cancellationToken)
        {
            if (waiverOptions.WaivablePlanKeys is not { Length: > 0 })
            {
                // Fail-closed: with no key configured nothing is waivable. The alternative — treating "empty" as
                // "everything" — would give away a whole plan by omission.
                return null;
            }

            var keys = new HashSet<string>(waiverOptions.WaivablePlanKeys, StringComparer.OrdinalIgnoreCase);
            var lines = invoice.Lines?.Data ?? new List<InvoiceLineItem>();
            var priceIds = lines.Select(l => l.Pricing?.PriceDetails?.PriceId).Where(p => !string.IsNullOrEmpty(p)).Select(p => p!).ToList();
            if (priceIds.Count == 0)
            {
                return null;
            }

            var planPrices = await db.PlanPrices.AsNoTracking().Include(p => p.Plan)
                .Where(p => p.ProviderPriceId != null && priceIds.Contains(p.ProviderPriceId))
                .ToListAsync(cancellationToken);
            var addOnPrices = await db.PlanAddOnPrices.AsNoTracking().Include(p => p.PlanAddOn).ThenInclude(pa => pa!.AddOn)
                .Where(p => p.ProviderPriceId != null && priceIds.Contains(p.ProviderPriceId))
                .ToListAsync(cancellationToken);

            foreach (var line in lines)
            {
                var priceId = line.Pricing?.PriceDetails?.PriceId;
                if (string.IsNullOrEmpty(priceId))
                {
                    continue;
                }

                var planName = planPrices.FirstOrDefault(p => p.ProviderPriceId == priceId)?.Plan?.Name;
                var addOnName = addOnPrices.FirstOrDefault(p => p.ProviderPriceId == priceId)?.PlanAddOn?.AddOn?.Name;
                var key = planName ?? addOnName;
                if (key != null && keys.Contains(key) && line.Amount > 0)
                {
                    return new WaivablePosition(key, line.Amount);
                }
            }

            return null;
        }

        /// <summary>
        /// The period the waiver is measured over: the one that CLOSED, taken backwards from the start of the
        /// period this invoice bills. Same length, so a yearly subscription measures a year.
        /// </summary>
        private static (DateTime Start, DateTime End) ClosedPeriod(Invoice invoice)
        {
            var end = invoice.PeriodStart;
            var length = invoice.PeriodEnd - invoice.PeriodStart;
            if (length <= TimeSpan.Zero)
            {
                length = TimeSpan.FromDays(30);
            }

            return (DateTime.SpecifyKind(end - length, DateTimeKind.Utc), DateTime.SpecifyKind(end, DateTimeKind.Utc));
        }

        /// <summary>
        /// Net turnover of the closed period: paid sales minus the refunds BOOKED in that same window. The
        /// booking date of the refund decides, not the date of the sale it reverses — that is the only rule that
        /// needs no retrospective correction, and it evens out over time.
        /// </summary>
        private static async Task<long> NetVolumeAsync(TContext db, int tenantId, string? currency, DateTime start, DateTime end, CancellationToken cancellationToken)
        {
            var paid = await db.TenantSales
                .Where(s => s.TenantId == tenantId
                            && (currency == null || s.Currency == currency)
                            && s.PaidUtc != null && s.PaidUtc >= start && s.PaidUtc < end)
                .SumAsync(s => (long?)s.AmountMinor, cancellationToken) ?? 0;

            var refunded = await db.TenantSaleRefunds
                .Where(r => r.Sale != null && r.Sale.TenantId == tenantId
                            && (currency == null || r.Sale.Currency == currency)
                            && r.Created >= start && r.Created < end)
                .SumAsync(r => (long?)r.AmountMinor, cancellationToken) ?? 0;

            return paid - refunded;
        }

        private static int? ParseTenantId(IDictionary<string, string>? metadata)
        {
            if (metadata != null
                && metadata.TryGetValue("tenantId", out var raw)
                && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                return id;
            }

            return null;
        }

        private sealed record WaivablePosition(string Key, long AmountMinor);
    }
}
