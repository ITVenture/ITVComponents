using System;
using System.Collections.Generic;
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
using Stripe.Checkout;

namespace ITVComponents.WebCoreToolkit.Billing.Stripe.Payments.Impl
{
    /// <summary>
    /// Records end-customer sales on a tenant's connected account (direct charges) and reverses them.
    /// <para>
    /// The payment page is hosted by the provider: a Checkout session with exactly ONE synthetic line item made
    /// from total and caption. That keeps card data out of this application entirely (SAQ-A) and is why a sale
    /// needs no line items of its own.
    /// </para>
    /// </summary>
    public class TenantSaleService<TContext> : ITenantSaleService
        where TContext : DbContext, IPaymentsContext
    {
        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly IStripeClient client;
        private readonly PaymentsRuntime runtime;
        private readonly TenantSaleNotifier notifier;

        public TenantSaleService(IDbContextFactory<TContext> dbFactory, IStripeClient client,
            IGlobalSettings<StripePaymentsOptions> settings, IEnumerable<IPaymentFeatureGate> featureGates,
            IEnumerable<ITenantSaleObserver> observers)
        {
            this.dbFactory = dbFactory;
            this.client = client;
            runtime = new PaymentsRuntime(settings, featureGates.FirstOrDefault());
            notifier = new TenantSaleNotifier(observers);
        }

        /// <inheritdoc />
        public async Task<SaleResult> CreateSaleAsync(SaleRequest request, CancellationToken cancellationToken = default)
        {
            runtime.EnsureEnabled();
            await runtime.EnsureFeatureAsync(request.TenantId, cancellationToken);

            var options = runtime.Options;
            var currency = (string.IsNullOrWhiteSpace(request.Currency) ? options.DefaultCurrency : request.Currency).Trim().ToUpperInvariant();
            var amountMinor = CurrencyMinorUnits.ToMinor(request.Amount, currency);
            if (amountMinor <= 0 || string.IsNullOrWhiteSpace(currency) || string.IsNullOrWhiteSpace(request.ExternalReference))
            {
                throw new TenantPaymentException(PaymentErrorCodes.InvalidAmount,
                    $"A sale needs a positive amount, a currency and an external reference (got {request.Amount} {currency}, reference '{request.ExternalReference}').");
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var account = await db.TenantPaymentAccounts.AsNoTracking()
                .FirstOrDefaultAsync(a => a.TenantId == request.TenantId, cancellationToken);
            // Checked before EVERY sale, not just at onboarding: the provider can restrict a live account days
            // later when it asks for further documents.
            runtime.EnsureCanSell(account);

            var reference = request.ExternalReference.Trim();
            var sale = await db.TenantSales.Include(s => s.Refunds)
                .FirstOrDefaultAsync(s => s.TenantId == request.TenantId && s.ExternalReference == reference, cancellationToken);

            if (sale == null)
            {
                sale = new TenantSale
                {
                    TenantId = request.TenantId,
                    ExternalReference = reference,
                    Description = Trim(request.Description, 256) ?? string.Empty,
                    AmountMinor = amountMinor,
                    Currency = currency,
                    // Frozen here and never recomputed: a later change to the rate must not rewrite what this
                    // sale actually cost.
                    ApplicationFeeMinor = ApplicationFeeMath.Calculate(options.ApplicationFee, amountMinor, currency),
                    Status = TenantSaleStatus.Pending,
                    ProviderAccountId = account!.ProviderAccountId,
                    CustomerEmail = Trim(request.CustomerEmail, 256),
                    MetadataJson = request.Metadata is { Count: > 0 } ? JsonSerializer.Serialize(request.Metadata) : null,
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
                    // The unique index on (tenant, reference) just caught a double submit. Re-read and continue
                    // with the row that won — returning the same sale is the whole point of the reference.
                    LogEnvironment.LogEvent(
                        $"Concurrent attempt to record sale '{reference}' for tenant {request.TenantId}; the existing sale is reused: {ex.OutlineException()}",
                        LogSeverity.Warning, "StripeConnect");
                    db.Entry(sale).State = EntityState.Detached;
                    var winner = await db.TenantSales.Include(s => s.Refunds)
                        .FirstOrDefaultAsync(s => s.TenantId == request.TenantId && s.ExternalReference == reference, cancellationToken);
                    if (winner == null)
                    {
                        // Not the unique index after all — whatever went wrong belongs to the caller, unaltered.
                        throw;
                    }

                    return await EnsureCheckoutAsync(db, winner, account!, request, cancellationToken, wasExisting: true);
                }

                return await EnsureCheckoutAsync(db, sale, account!, request, cancellationToken, wasExisting: false);
            }

            // The reference is already known. Paid or refunded sales are handed back untouched — recording the
            // same order twice must never produce a second payment.
            if (sale.Status is TenantSaleStatus.Paid or TenantSaleStatus.Refunded or TenantSaleStatus.PartiallyRefunded)
            {
                return ToResult(sale, wasExisting: true);
            }

            // Pending, expired, failed or cancelled: the order still has not been paid, so the tenant gets a
            // usable payment page again. That is a deliberate second ATTEMPT at the same sale, not a second sale
            // — amount and frozen fee stay as they were.
            return await EnsureCheckoutAsync(db, sale, account!, request, cancellationToken, wasExisting: true);
        }

        /// <inheritdoc />
        public async Task<SaleResult> GetSaleAsync(int tenantSaleId, CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var sale = await db.TenantSales.AsNoTracking().Include(s => s.Refunds)
                           .FirstOrDefaultAsync(s => s.TenantSaleId == tenantSaleId, cancellationToken)
                       ?? throw new TenantPaymentException(PaymentErrorCodes.SaleNotFound, $"Sale {tenantSaleId} not found.");
            return ToResult(sale, wasExisting: true);
        }

        /// <inheritdoc />
        public async Task<SaleResult?> FindByReferenceAsync(int tenantId, string externalReference, CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var reference = externalReference?.Trim() ?? string.Empty;
            var sale = await db.TenantSales.AsNoTracking().Include(s => s.Refunds)
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.ExternalReference == reference, cancellationToken);
            return sale == null ? null : ToResult(sale, wasExisting: true);
        }

        /// <inheritdoc />
        public async Task<RefundResult> RefundSaleAsync(int tenantSaleId, long? amountMinor, string? reason, bool? refundApplicationFee = null, CancellationToken cancellationToken = default)
        {
            runtime.EnsureEnabled();

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var sale = await db.TenantSales.Include(s => s.Refunds)
                           .FirstOrDefaultAsync(s => s.TenantSaleId == tenantSaleId, cancellationToken)
                       ?? throw new TenantPaymentException(PaymentErrorCodes.SaleNotFound, $"Sale {tenantSaleId} not found.");

            if (sale.Status is not (TenantSaleStatus.Paid or TenantSaleStatus.PartiallyRefunded))
            {
                throw new TenantPaymentException(PaymentErrorCodes.SaleNotRefundable,
                    $"Sale {tenantSaleId} is '{sale.Status}' and was never paid.");
            }

            if (string.IsNullOrEmpty(sale.ProviderChargeId))
            {
                throw new TenantPaymentException(PaymentErrorCodes.MissingCharge,
                    $"Sale {tenantSaleId} has no charge on record; the refund cannot be addressed.");
            }

            var alreadyRefunded = sale.Refunds.Sum(r => r.AmountMinor);
            var remaining = sale.AmountMinor - alreadyRefunded;
            var amount = amountMinor ?? remaining;
            if (amount <= 0 || amount > remaining)
            {
                throw new TenantPaymentException(PaymentErrorCodes.RefundExceedsAmount,
                    $"Sale {tenantSaleId} has {remaining} of {sale.AmountMinor} left; {amount} cannot be refunded.");
            }

            var withFee = refundApplicationFee ?? runtime.Options.RefundApplicationFeeByDefault;
            var accountId = sale.ProviderAccountId
                            ?? (await db.TenantPaymentAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.TenantId == sale.TenantId, cancellationToken))?.ProviderAccountId
                            ?? throw new TenantPaymentException(PaymentErrorCodes.NoAccount, $"Tenant {sale.TenantId} has no payout account.");

            Refund refund;
            try
            {
                refund = await new RefundService(client).CreateAsync(new RefundCreateOptions
                {
                    Charge = sale.ProviderChargeId,
                    Amount = amount,
                    // The provider only accepts its own three reasons; anything else would be rejected, so the
                    // tenant's own wording travels as metadata and is kept on the local row.
                    Reason = MapReason(reason),
                    RefundApplicationFee = withFee,
                    Metadata = BuildRefundMetadata(sale, reason)
                }, PaymentsRuntime.ForAccount(accountId, $"refund:{sale.TenantSaleId}:{alreadyRefunded}:{amount}"), cancellationToken);
            }
            catch (StripeException ex)
            {
                LogEnvironment.LogEvent(
                    $"Refund of {amount} on sale {tenantSaleId} (charge {sale.ProviderChargeId}, account {accountId}) was refused by the provider: {ex.OutlineException()}",
                    LogSeverity.Error, "StripeConnect");
                throw new TenantPaymentException(PaymentErrorCodes.ProviderError, ex.Message, ex);
            }

            // How much of the commission actually went back. The provider returns it proportionally to the
            // refunded share (and in full for a full refund); without recording it here the commission statement
            // would still show a fee that no longer exists.
            var feeAlreadyBack = sale.Refunds.Sum(r => r.ApplicationFeeRefundedMinor);
            var feeBack = withFee
                ? Math.Clamp(ApplicationFeeMath.ProportionalRefund(sale.ApplicationFeeMinor, amount, sale.AmountMinor), 0, Math.Max(0, sale.ApplicationFeeMinor - feeAlreadyBack))
                : 0;

            var row = new TenantSaleRefund
            {
                TenantSaleId = sale.TenantSaleId,
                AmountMinor = amount,
                ApplicationFeeRefundedMinor = feeBack,
                ProviderRefundId = refund.Id,
                Reason = Trim(reason, 512),
                Status = refund.Status,
                Created = DateTime.UtcNow
            };
            sale.Refunds.Add(row);
            sale.Status = DeriveStatus(sale.AmountMinor, alreadyRefunded + amount);
            sale.Updated = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            if (!withFee)
            {
                // Not an error, but worth finding again: the platform keeps its cut of a reversed deal, so the
                // tenant pays for cancelling. That has to be a decision someone can point at.
                LogEnvironment.LogEvent(
                    $"Refund {refund.Id} on sale {sale.TenantSaleId} (tenant {sale.TenantId}) was booked WITHOUT returning the platform commission of {sale.ApplicationFeeMinor} {sale.Currency}.",
                    LogSeverity.Warning, "StripeConnect");
            }

            // The provider will also deliver charge.refunded for this. The webhook mirrors only refunds it does
            // not already know, so the observers are called exactly once — here, because this is where the row
            // came into existence.
            await notifier.NotifyRefundedAsync(sale, row, cancellationToken);

            return new RefundResult
            {
                TenantSaleRefundId = row.TenantSaleRefundId,
                TenantSaleId = sale.TenantSaleId,
                AmountMinor = amount,
                ApplicationFeeRefundedMinor = feeBack,
                ProviderRefundId = refund.Id,
                Status = refund.Status,
                SaleStatus = sale.Status
            };
        }

        /// <summary>
        /// Makes sure the (still unpaid) sale has a usable payment page, creating a provider session when it has
        /// none or its previous one is spent. Split out because the session can fail AFTER the row was written —
        /// without this the retry would hand back a sale that can never be paid.
        /// </summary>
        private async Task<SaleResult> EnsureCheckoutAsync(TContext db, TenantSale sale, TenantPaymentAccount account, SaleRequest request, CancellationToken cancellationToken, bool wasExisting)
        {
            if (sale.Status == TenantSaleStatus.Pending && !string.IsNullOrEmpty(sale.CheckoutUrl) && !string.IsNullOrEmpty(sale.ProviderSessionId))
            {
                return ToResult(sale, wasExisting);
            }

            var options = runtime.Options;
            var expiryMinutes = Math.Clamp(options.CheckoutExpiryMinutes, 30, 1440);
            var attempt = sale.Status == TenantSaleStatus.Pending && string.IsNullOrEmpty(sale.ProviderSessionId) ? 0 : 1;

            var create = new SessionCreateOptions
            {
                Mode = "payment",
                // Deliberately NO Customer and NO SetupFutureUsage: the purchase is ad hoc. Handing the provider
                // a customer would turn an anonymous buyer into a stored profile with saved payment details —
                // see section 2.5 of the Connect plan. The e-mail below is for the receipt only.
                // Still to be verified against a live account: customer_creation behaves differently on a
                // connected account than on the platform account, so it is not sent at all here.
                LineItems = new List<SessionLineItemOptions>
                {
                    new()
                    {
                        Quantity = 1,
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = sale.Currency.ToLowerInvariant(),
                            UnitAmount = sale.AmountMinor,
                            ProductData = new SessionLineItemPriceDataProductDataOptions { Name = sale.Description }
                        }
                    }
                },
                PaymentIntentData = new SessionPaymentIntentDataOptions
                {
                    ApplicationFeeAmount = sale.ApplicationFeeMinor > 0 ? sale.ApplicationFeeMinor : null,
                    StatementDescriptorSuffix = Trim(options.StatementDescriptorSuffix, 22),
                    Metadata = new Dictionary<string, string>
                    {
                        ["tenantId"] = sale.TenantId.ToString(),
                        ["tenantSaleId"] = sale.TenantSaleId.ToString(),
                        ["externalReference"] = sale.ExternalReference
                    }
                },
                // The way back from the webhook to this row without depending on the provider ids.
                ClientReferenceId = sale.TenantSaleId.ToString(),
                CustomerEmail = string.IsNullOrWhiteSpace(sale.CustomerEmail) ? null : sale.CustomerEmail,
                SuccessUrl = request.SuccessUrl,
                CancelUrl = request.CancelUrl,
                ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes),
                Metadata = new Dictionary<string, string>
                {
                    ["tenantId"] = sale.TenantId.ToString(),
                    ["tenantSaleId"] = sale.TenantSaleId.ToString(),
                    ["externalReference"] = sale.ExternalReference
                }
            };

            Session session;
            try
            {
                session = await new SessionService(client).CreateAsync(create,
                    // Keyed on the sale and the attempt: a repeated click returns the same session, a deliberate
                    // second attempt after an expiry gets a fresh one.
                    PaymentsRuntime.ForAccount(account.ProviderAccountId, $"sale:{sale.TenantId}:{sale.ExternalReference}:{attempt}"),
                    cancellationToken);
            }
            catch (StripeException ex)
            {
                LogEnvironment.LogEvent(
                    $"Could not create a payment page for sale {sale.TenantSaleId} (tenant {sale.TenantId}, reference '{sale.ExternalReference}', account {account.ProviderAccountId}): {ex.OutlineException()}",
                    LogSeverity.Error, "StripeConnect");
                throw new TenantPaymentException(PaymentErrorCodes.ProviderError, ex.Message, ex);
            }

            sale.ProviderSessionId = session.Id;
            sale.CheckoutUrl = session.Url;
            sale.Status = TenantSaleStatus.Pending;
            sale.ProviderAccountId = account.ProviderAccountId;
            sale.Updated = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            return ToResult(sale, wasExisting);
        }

        /// <summary>Sale status from the refunded total — never from a single refund row.</summary>
        internal static TenantSaleStatus DeriveStatus(long amountMinor, long refundedMinor)
            => refundedMinor <= 0 ? TenantSaleStatus.Paid
                : refundedMinor >= amountMinor ? TenantSaleStatus.Refunded
                : TenantSaleStatus.PartiallyRefunded;

        /// <summary>The provider accepts a fixed set of reasons; everything else has to travel as metadata.</summary>
        private static string? MapReason(string? reason) => reason?.Trim().ToLowerInvariant() switch
        {
            "duplicate" => "duplicate",
            "fraudulent" => "fraudulent",
            "requested_by_customer" => "requested_by_customer",
            _ => null
        };

        private static Dictionary<string, string> BuildRefundMetadata(TenantSale sale, string? reason)
        {
            var metadata = new Dictionary<string, string>
            {
                ["tenantId"] = sale.TenantId.ToString(),
                ["tenantSaleId"] = sale.TenantSaleId.ToString(),
                ["externalReference"] = sale.ExternalReference
            };

            if (!string.IsNullOrWhiteSpace(reason))
            {
                metadata["reason"] = Trim(reason, 500)!;
            }

            return metadata;
        }

        private static SaleResult ToResult(TenantSale sale, bool wasExisting) => new()
        {
            TenantSaleId = sale.TenantSaleId,
            TenantId = sale.TenantId,
            ExternalReference = sale.ExternalReference,
            Description = sale.Description,
            Status = sale.Status,
            CheckoutUrl = sale.Status == TenantSaleStatus.Pending ? sale.CheckoutUrl : null,
            AmountMinor = sale.AmountMinor,
            ApplicationFeeMinor = sale.ApplicationFeeMinor,
            RefundedMinor = sale.Refunds?.Sum(r => r.AmountMinor) ?? 0,
            Currency = sale.Currency,
            PaidUtc = sale.PaidUtc,
            Created = sale.Created,
            WasExisting = wasExisting
        };

        private static string? Trim(string? value, int max)
            => string.IsNullOrWhiteSpace(value) ? null : value.Length <= max ? value : value[..max];
    }
}
