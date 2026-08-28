using System;
using System.Collections.Generic;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;

namespace ITVComponents.WebCoreToolkit.BillingViews.Blazor.ViewModels
{
    /// <summary>
    /// State of the tenant's payout account, as the management page needs it. Carries the derived
    /// <see cref="Stage"/> so the page switches on one value instead of re-deriving the same conditions in
    /// markup — where the next state would inevitably be forgotten in one of the branches.
    /// </summary>
    public class PaymentAccountViewModel
    {
        public bool HasAccount { get; set; }

        public string? ProviderAccountId { get; set; }

        public string? AccountType { get; set; }

        public string? Country { get; set; }

        public string? DefaultCurrency { get; set; }

        public bool ChargesEnabled { get; set; }

        public bool PayoutsEnabled { get; set; }

        public bool DetailsSubmitted { get; set; }

        public bool Disconnected { get; set; }

        public string? DisabledReason { get; set; }

        public IReadOnlyList<string> CurrentlyDue { get; set; } = Array.Empty<string>();

        public IReadOnlyList<string> PastDue { get; set; } = Array.Empty<string>();

        public IReadOnlyList<string> PendingVerification { get; set; } = Array.Empty<string>();

        public DateTime? CurrentDeadline { get; set; }

        /// <summary>Whether a sale would be accepted right now — the same predicate the sale service enforces.</summary>
        public bool CanSell { get; set; }

        public DateTime Updated { get; set; }

        /// <summary>Which of the six states the page is in.</summary>
        public PaymentAccountStage Stage => !HasAccount ? PaymentAccountStage.None
            : Disconnected ? PaymentAccountStage.Disconnected
            : !DetailsSubmitted ? PaymentAccountStage.OnboardingIncomplete
            : !string.IsNullOrEmpty(DisabledReason) || PastDue.Count > 0 ? PaymentAccountStage.Restricted
            : CanSell ? PaymentAccountStage.Active
            : PaymentAccountStage.AwaitingReview;
    }

    /// <summary>The states a payout account can be shown in. Each has its own explanation and its own next step.</summary>
    public enum PaymentAccountStage
    {
        /// <summary>Nothing set up yet.</summary>
        None = 0,

        /// <summary>The hosted onboarding was started but not finished.</summary>
        OnboardingIncomplete = 1,

        /// <summary>Everything handed in, provider still checking. A waiting state, not an error.</summary>
        AwaitingReview = 2,

        /// <summary>Live.</summary>
        Active = 3,

        /// <summary>Restricted: the provider wants more, or has disabled the account.</summary>
        Restricted = 4,

        /// <summary>The link between platform and account was severed.</summary>
        Disconnected = 5
    }

    /// <summary>One row of the tenant's sales list.</summary>
    public class SaleListItemViewModel
    {
        public int TenantSaleId { get; set; }

        public DateTime Created { get; set; }

        public DateTime? PaidUtc { get; set; }

        public string ExternalReference { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Currency { get; set; } = string.Empty;

        public long AmountMinor { get; set; }

        public long ApplicationFeeMinor { get; set; }

        public long RefundedMinor { get; set; }

        public long ApplicationFeeRefundedMinor { get; set; }

        public TenantSaleStatus Status { get; set; }

        public string? CustomerEmail { get; set; }

        // Provider identifiers — shown in the detail popup, because that is what a support case is looked up by.
        public string? ProviderSessionId { get; set; }

        public string? ProviderPaymentIntentId { get; set; }

        public string? ProviderChargeId { get; set; }

        /// <summary>What is left of the sale after refunds — the amount an unqualified refund would return.</summary>
        public long RefundableMinor => Math.Max(0, AmountMinor - RefundedMinor);

        /// <summary>What the tenant actually keeps: amount minus commission, both net of refunds.</summary>
        public long NetMinor => AmountMinor - RefundedMinor - (ApplicationFeeMinor - ApplicationFeeRefundedMinor);

        public bool CanRefund => Status is TenantSaleStatus.Paid or TenantSaleStatus.PartiallyRefunded && RefundableMinor > 0;
    }

    /// <summary>The sales page: the rows plus the totals and the waiver progress.</summary>
    public class SalesOverviewViewModel
    {
        public IReadOnlyList<SaleListItemViewModel> Items { get; set; } = Array.Empty<SaleListItemViewModel>();

        public string Currency { get; set; } = string.Empty;

        public long TotalAmountMinor { get; set; }

        public long TotalFeeMinor { get; set; }

        public long TotalRefundedMinor { get; set; }

        public long TotalNetMinor { get; set; }

        /// <summary>Progress towards the waived base fee. Null when the waiver is not configured.</summary>
        public WaiverProgressViewModel? Waiver { get; set; }
    }

    /// <summary>
    /// Progress towards the volume-based waiver. Without this the model is worth nothing — it is meant to
    /// motivate, and a threshold nobody can see motivates nobody.
    /// </summary>
    public class WaiverProgressViewModel
    {
        public string Currency { get; set; } = string.Empty;

        /// <summary>Net turnover of the period currently running, clamped at zero.</summary>
        public long CurrentVolumeMinor { get; set; }

        public long ThresholdMinor { get; set; }

        public DateTime PeriodStartUtc { get; set; }

        public DateTime PeriodEndUtc { get; set; }

        /// <summary>True once the running period is over the threshold — i.e. the NEXT invoice will be waived.</summary>
        public bool Reached => CurrentVolumeMinor >= ThresholdMinor;

        public long RemainingMinor => Math.Max(0, ThresholdMinor - CurrentVolumeMinor);

        public double Percent => ThresholdMinor <= 0 ? 0 : Math.Min(100d, CurrentVolumeMinor * 100d / ThresholdMinor);
    }

    /// <summary>One connected account in the platform-wide overview.</summary>
    public class PaymentAccountAdminViewModel
    {
        public int TenantId { get; set; }

        public string? TenantName { get; set; }

        public string ProviderAccountId { get; set; } = string.Empty;

        public string? AccountType { get; set; }

        public string? Country { get; set; }

        public bool ChargesEnabled { get; set; }

        public bool PayoutsEnabled { get; set; }

        public bool DetailsSubmitted { get; set; }

        public bool Disconnected { get; set; }

        public string? DisabledReason { get; set; }

        public int OpenRequirements { get; set; }

        public string? Currency { get; set; }

        public int SalesCount { get; set; }

        public long VolumeMinor { get; set; }

        public long FeeMinor { get; set; }

        public long RefundedMinor { get; set; }

        public long FeeRefundedMinor { get; set; }

        /// <summary>The platform's actual take: commission earned minus commission given back.</summary>
        public long NetFeeMinor => FeeMinor - FeeRefundedMinor;

        public DateTime Updated { get; set; }
    }
}
