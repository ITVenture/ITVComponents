using System.Collections.Generic;
using ITVComponents.WebCoreToolkit.Configuration;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options
{
    /// <summary>
    /// Everything axis B needs, in ONE global setting (<c>StripePayments</c>), read through
    /// <c>IGlobalSettings&lt;StripePaymentsOptions&gt;</c>.
    /// <para>
    /// Deliberately GLOBAL and not tenant-scoped: scoped settings are writable through the tenant settings page
    /// (<c>Tenants.WriteSettings</c>), so a tenant could set its own commission to zero. Should per-tenant rates
    /// ever be needed they belong in a table only the platform admin can write — not in the hierarchy settings.
    /// </para>
    /// The provider API key is NOT repeated here: it keeps coming from <c>Billing:Stripe</c>, both axes share
    /// one platform account.
    /// </summary>
    [SettingName("StripePayments")]
    public class StripePaymentsOptions
    {
        /// <summary>
        /// Master switch. With this off, service and views refuse to work even when the feature is active for the
        /// tenant — the deployment always wins over the entitlement.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Account type for new connected accounts: <c>express</c> or <c>standard</c>.</summary>
        public string AccountType { get; set; } = "express";

        /// <summary>
        /// <c>direct</c> or <c>destination</c>. Only <c>direct</c> is implemented — with destination charges the
        /// PLATFORM becomes merchant of record, which is a tax decision and not a configuration switch.
        /// </summary>
        public string ChargeType { get; set; } = "direct";

        /// <summary>ISO-4217 currency used when the caller does not name one.</summary>
        public string DefaultCurrency { get; set; } = "CHF";

        /// <summary>
        /// Country for new connected accounts when it cannot be derived from the tenant's billing profile.
        /// Careful: the provider fixes the country at creation and never lets it change again.
        /// </summary>
        public string DefaultCountry { get; set; } = "CH";

        /// <summary>How the platform's commission per sale is computed.</summary>
        public ApplicationFeeOptions ApplicationFee { get; set; } = new();

        /// <summary>
        /// Signing secret of the CONNECT webhook endpoint (<c>whsec_...</c>). A separate endpoint with its own
        /// secret — mixing connect events into the platform endpoint would mean trying both secrets blindly.
        /// </summary>
        public string ConnectWebhookSecret { get; set; } = string.Empty;

        /// <summary>
        /// When true a sale needs <c>payouts_enabled</c>, not just <c>charges_enabled</c>. Stricter, but keeps
        /// money from piling up on an account that has no way to pay it out.
        /// </summary>
        public bool RequirePayoutsEnabled { get; set; }

        /// <summary>
        /// Give the commission back proportionally when a sale is refunded. Default true: with false the tenant
        /// refunds the full amount to the end customer while the platform keeps its cut of a reversed deal — the
        /// tenant pays for cancelling. Keeping the fee must be a decision, not the fallout of a default.
        /// </summary>
        public bool RefundApplicationFeeByDefault { get; set; } = true;

        /// <summary>Suffix on the end customer's statement, max. 22 characters.</summary>
        public string? StatementDescriptorSuffix { get; set; }

        /// <summary>Lifetime of a hosted payment page in minutes (the provider allows 30..1440).</summary>
        public int CheckoutExpiryMinutes { get; set; } = 60;

        /// <summary>Volume-based waiver of the subscription base fee. Off unless explicitly switched on.</summary>
        public VolumeWaiverOptions VolumeWaiver { get; set; } = new();
    }

    /// <summary>
    /// Commission model: <c>fee = amount * bp / 10000 + fixed</c>, rounded to whole minor units, then clamped to
    /// [<see cref="MinMinor"/>, <see cref="MaxMinor"/>] and hard-clamped to [0, amount].
    /// </summary>
    public class ApplicationFeeOptions
    {
        /// <summary>Share in basis points (250 = 2.5 %). Integer on purpose — no floating-point surprises.</summary>
        public int PercentBasisPoints { get; set; }

        /// <summary>Fixed surcharge in minor units, added on top of the percentage.</summary>
        public long FixedMinor { get; set; }

        /// <summary>Lower bound in minor units (0 = none).</summary>
        public long MinMinor { get; set; }

        /// <summary>Upper bound in minor units (0 = none).</summary>
        public long MaxMinor { get; set; }

        /// <summary>
        /// Deviating rates per currency, key = ISO-4217. A match replaces the base values COMPLETELY, not field
        /// by field — a half-overridden rate is the kind of thing nobody notices until the invoice.
        /// </summary>
        public Dictionary<string, ApplicationFeeOptions>? PerCurrency { get; set; }
    }

    /// <summary>How the base fee is waived once the tenant's turnover reaches the threshold.</summary>
    public enum WaiverMode
    {
        /// <summary>Full waiver from the threshold upwards. Simple to explain; accepts the earnings dent below it.</summary>
        Hard = 0,

        /// <summary>Proportional waiver, linear over a band below the threshold. No dent — if the band is wide enough.</summary>
        Sliding = 1
    }

    /// <summary>
    /// Volume-based waiver of the subscription base fee (axis B feeding back into axis A). Measured exactly ONCE
    /// per invoice, over the period that has closed.
    /// </summary>
    public class VolumeWaiverOptions
    {
        /// <summary>Off unless set.</summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// <see cref="WaiverMode.Hard"/> (default) or <see cref="WaiverMode.Sliding"/>.
        /// </summary>
        public WaiverMode Mode { get; set; } = WaiverMode.Hard;

        /// <summary>Net turnover of the closed period in minor units at which the fee is waived in full.</summary>
        public long ThresholdMinor { get; set; }

        /// <summary>
        /// Start of the sliding band. Only read for <see cref="WaiverMode.Sliding"/> and then it must lie between
        /// 0 and <see cref="ThresholdMinor"/>. An invalid value falls back to <see cref="WaiverMode.Hard"/> with a
        /// warning in the log — a visibly conservative waiver beats a quietly wrong one.
        /// </summary>
        public long WaiverRampStartMinor { get; set; }

        /// <summary>
        /// Keys of the plans/add-ons whose invoice position may be waived. Empty = nothing: fail-closed, so a
        /// whole plan is never given away by omission.
        /// </summary>
        public string[] WaivablePlanKeys { get; set; } = [];

        /// <summary>Deviating thresholds per currency, key = ISO-4217.</summary>
        public Dictionary<string, VolumeWaiverOptions>? PerCurrency { get; set; }
    }
}
