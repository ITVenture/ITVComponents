using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;

namespace ITVComponents.WebCoreToolkit.Billing.Stripe.Payments.Abstractions
{
    /// <summary>
    /// Onboarding and status of a tenant's connected account. Everything here talks to the PROVIDER; the local
    /// mirror is a by-product, never the authority.
    /// </summary>
    public interface ITenantPaymentAccountService
    {
        /// <summary>State of the connected account, or null when the tenant has none yet.</summary>
        Task<TenantPaymentAccountStatus?> GetStatusAsync(int tenantId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a connected account if needed and returns the URL of the provider-hosted onboarding. Safe to
        /// call repeatedly: an expired link is replaced, an existing account is never created twice.
        /// </summary>
        /// <param name="tenantId">Logical tenant identifier.</param>
        /// <param name="returnUrl">Absolute URL (including the tenant path prefix) the provider returns to.</param>
        /// <param name="refreshUrl">Absolute URL the provider calls when the link has expired.</param>
        /// <param name="email">Optional e-mail seeded into the onboarding form.</param>
        /// <param name="country">
        /// ISO-3166 country for a NEW account. Ignored once an account exists — the provider fixes the country
        /// at creation and never allows a change.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<string> StartOnboardingAsync(int tenantId, string returnUrl, string refreshUrl, string? email = null, string? country = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the account fresh from the provider and updates the local mirror. Meant for the return from the
        /// onboarding, where the webhook has usually not arrived yet — showing "done" on the strength of the
        /// redirect alone would be a guess.
        /// </summary>
        Task<TenantPaymentAccountStatus> RefreshAsync(int tenantId, CancellationToken cancellationToken = default);

        /// <summary>
        /// One-time link into the tenant's express dashboard. Null for standard accounts — those have their own
        /// provider login and do not accept a login link.
        /// </summary>
        Task<string?> CreateDashboardLinkAsync(int tenantId, CancellationToken cancellationToken = default);
    }

    /// <summary>Read model of a connected account, as shown on the payout-account page.</summary>
    public sealed class TenantPaymentAccountStatus
    {
        public int TenantId { get; set; }

        public string ProviderAccountId { get; set; } = string.Empty;

        public string AccountType { get; set; } = string.Empty;

        public string? Country { get; set; }

        public string? DefaultCurrency { get; set; }

        public bool ChargesEnabled { get; set; }

        public bool PayoutsEnabled { get; set; }

        public bool DetailsSubmitted { get; set; }

        public bool Disconnected { get; set; }

        public string? DisabledReason { get; set; }

        /// <summary>Requirements the provider wants now.</summary>
        public IReadOnlyList<string> CurrentlyDue { get; set; } = Array.Empty<string>();

        /// <summary>Requirements already overdue — the account is usually restricted while these stand.</summary>
        public IReadOnlyList<string> PastDue { get; set; } = Array.Empty<string>();

        /// <summary>Documents handed in and still being checked.</summary>
        public IReadOnlyList<string> PendingVerification { get; set; } = Array.Empty<string>();

        /// <summary>Deadline the provider set for the outstanding requirements, if any.</summary>
        public DateTime? CurrentDeadline { get; set; }

        /// <summary>
        /// Whether a sale can be made right now — the same predicate the sale service enforces, including the
        /// <c>RequirePayoutsEnabled</c> setting. Display and guard must share it, or the page offers something
        /// the service then refuses.
        /// </summary>
        public bool CanSell { get; set; }

        public DateTime Updated { get; set; }
    }

    /// <summary>Recording and reversing end-customer sales in the name of a tenant.</summary>
    public interface ITenantSaleService
    {
        /// <summary>
        /// Records a sale and returns the hosted payment page. Only total and caption are needed — no line items.
        /// Calling it twice with the same <see cref="SaleRequest.ExternalReference"/> returns the FIRST sale.
        /// </summary>
        Task<SaleResult> CreateSaleAsync(SaleRequest request, CancellationToken cancellationToken = default);

        /// <summary>Reads one sale by its local id.</summary>
        Task<SaleResult> GetSaleAsync(int tenantSaleId, CancellationToken cancellationToken = default);

        /// <summary>Looks a sale up by the host's own reference; null when there is none.</summary>
        Task<SaleResult?> FindByReferenceAsync(int tenantId, string externalReference, CancellationToken cancellationToken = default);

        /// <summary>
        /// Full or partial refund. <paramref name="amountMinor"/> null refunds what is left.
        /// <paramref name="refundApplicationFee"/> null takes the configured default — which returns the
        /// commission proportionally, so the tenant does not pay for cancelling.
        /// </summary>
        Task<RefundResult> RefundSaleAsync(int tenantSaleId, long? amountMinor, string? reason, bool? refundApplicationFee = null, CancellationToken cancellationToken = default);
    }

    /// <summary>What the host has to say to record a sale.</summary>
    public sealed class SaleRequest
    {
        public int TenantId { get; set; }

        /// <summary>Total in major units, e.g. 49.90.</summary>
        public decimal Amount { get; set; }

        /// <summary>ISO-4217; null takes the configured default currency.</summary>
        public string? Currency { get; set; }

        /// <summary>What the end customer reads on the payment page.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>The host's reference (order number). Carries the idempotency.</summary>
        public string ExternalReference { get; set; } = string.Empty;

        /// <summary>Optional, for the receipt only. Creates no customer profile.</summary>
        public string? CustomerEmail { get; set; }

        /// <summary>Absolute URL (incl. tenant path prefix) after a successful payment.</summary>
        public string SuccessUrl { get; set; } = string.Empty;

        /// <summary>Absolute URL (incl. tenant path prefix) after an abandoned payment.</summary>
        public string CancelUrl { get; set; } = string.Empty;

        /// <summary>Free-form data of the host, stored with the sale and passed to the provider.</summary>
        public IDictionary<string, string>? Metadata { get; set; }
    }

    /// <summary>Read model of a sale.</summary>
    public sealed class SaleResult
    {
        public int TenantSaleId { get; set; }

        public int TenantId { get; set; }

        public string ExternalReference { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public TenantSaleStatus Status { get; set; }

        /// <summary>The hosted payment page; null once the sale is no longer payable.</summary>
        public string? CheckoutUrl { get; set; }

        public long AmountMinor { get; set; }

        public long ApplicationFeeMinor { get; set; }

        /// <summary>Sum of all refunds booked so far, in minor units.</summary>
        public long RefundedMinor { get; set; }

        public string Currency { get; set; } = string.Empty;

        public DateTime? PaidUtc { get; set; }

        public DateTime Created { get; set; }

        /// <summary>True when this sale already existed and was returned instead of creating a second one.</summary>
        public bool WasExisting { get; set; }
    }

    /// <summary>Outcome of a refund.</summary>
    public sealed class RefundResult
    {
        public int TenantSaleRefundId { get; set; }

        public int TenantSaleId { get; set; }

        public long AmountMinor { get; set; }

        public long ApplicationFeeRefundedMinor { get; set; }

        public string? ProviderRefundId { get; set; }

        public string? Status { get; set; }

        /// <summary>Status of the sale AFTER the refund, derived from the sum of all its refunds.</summary>
        public TenantSaleStatus SaleStatus { get; set; }
    }

    /// <summary>
    /// Verifies and processes events of the CONNECT webhook endpoint (its own endpoint with its own signing
    /// secret — see the endpoint extension).
    /// </summary>
    public interface IStripeConnectWebhookHandler
    {
        Task HandleAsync(string payload, string signatureHeader, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Evaluates the volume-based waiver when a SUBSCRIPTION invoice is created (platform webhook, not the
    /// connect one). Registered only when the payments branch is wired; the platform webhook resolves it as a
    /// collection so its absence is not a failure.
    /// </summary>
    public interface IVolumeWaiverProcessor
    {
        /// <summary>
        /// Decides and books the waiver for <paramref name="providerInvoiceId"/>. Must be idempotent: the
        /// provider delivers <c>invoice.created</c> more than once, and a second credit is money given away.
        /// </summary>
        Task ProcessInvoiceCreatedAsync(string providerInvoiceId, CancellationToken cancellationToken = default);
    }
}
