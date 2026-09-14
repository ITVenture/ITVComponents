using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments
{
    /// <summary>
    /// Local mirror of the tenant's connected payment account (Stripe Connect). Exactly one row per tenant.
    /// <para>
    /// Like the rest of Billing, <see cref="TenantId"/> is a plain logical reference (int) and NOT a foreign key
    /// to the tenant-security Tenant table — the payments branch stays tenant-model-agnostic.
    /// </para>
    /// <para>
    /// The flags are a mirror, never the source of truth: an account that is live today can be disabled days
    /// later when the provider asks for further documents. They are refreshed from the <c>account.updated</c>
    /// event and by an explicit refresh, and <see cref="ChargesEnabled"/> is re-checked before EVERY sale.
    /// </para>
    /// </summary>
    [Index(nameof(TenantId), IsUnique = true, Name = "IX_UniqueTenantPaymentAccount")]
    [Index(nameof(ProviderAccountId), IsUnique = true, Name = "IX_UniqueProviderPaymentAccount")]
    public class TenantPaymentAccount
    {
        [Key]
        public int TenantPaymentAccountId { get; set; }

        /// <summary>Logical tenant identifier. No FK — see class remarks.</summary>
        public int TenantId { get; set; }

        /// <summary>Provider account identifier (Stripe <c>acct_...</c>).</summary>
        [Required, MaxLength(256)]
        public string ProviderAccountId { get; set; } = string.Empty;

        /// <summary>
        /// Which provider dashboard this account has access to: <c>express</c>, <c>full</c> or <c>none</c>. This
        /// is what became of the old account type - v2 accounts are described by the configurations that are
        /// applied to them, and the dashboard follows from those rather than being chosen up front.
        /// </summary>
        [MaxLength(32)]
        public string DashboardType { get; set; } = "express";

        /// <summary>
        /// ISO-3166 country the account was created in. Fixed at creation — the provider does NOT allow changing
        /// it afterwards; a wrong value means throwing the account away and starting over.
        /// </summary>
        [MaxLength(2)]
        public string? Country { get; set; }

        /// <summary>ISO-4217 default currency of the connected account.</summary>
        [MaxLength(3)]
        public string? DefaultCurrency { get; set; }

        /// <summary>
        /// The account may accept payments. True only while the card-payments capability reads <c>active</c> -
        /// <c>pending</c> and <c>restricted</c> are both "not yet", and treating either as a yes would let a shop
        /// take money the provider has not cleared it for.
        /// </summary>
        public bool ChargesEnabled { get; set; }

        /// <summary>
        /// The account may receive payouts. True only while the payout capability reads <c>active</c>; see the
        /// note on <see cref="ChargesEnabled"/>.
        /// </summary>
        public bool PayoutsEnabled { get; set; }

        /// <summary>
        /// Raw status of the card-payments capability: <c>active</c>, <c>pending</c>, <c>restricted</c> or
        /// <c>unsupported</c>. Kept next to the boolean because the four cases read very differently to the shop
        /// owner - "we are checking" is not "we need something from you" - and a boolean cannot tell them apart.
        /// </summary>
        [MaxLength(32)]
        public string? CardPaymentsStatus { get; set; }

        /// <summary>Raw status of the payout capability; see <see cref="CardPaymentsStatus"/>.</summary>
        [MaxLength(32)]
        public string? PayoutsStatus { get; set; }

        /// <summary>
        /// The tenant has supplied everything the provider asked for. Derived, not mirrored: v2 has no such flag,
        /// so it is the absence of outstanding requirement entries.
        /// </summary>
        public bool DetailsSubmitted { get; set; }

        /// <summary>
        /// The soonest moment an outstanding requirement turns overdue, or null when nothing is pending. Worth
        /// showing: past that date the provider stops the account rather than asking again.
        /// </summary>
        public DateTime? RequirementsDeadline { get; set; }

        /// <summary>
        /// Raw mirror of the provider's outstanding/overdue requirements (JSON), for display. Deliberately not
        /// modelled further: the shape is the provider's and changes without notice.
        /// </summary>
        public string? RequirementsJson { get; set; }

        /// <summary>
        /// Why the account cannot currently do what it should; null when it is fine. Derived, not mirrored: v2
        /// reports the reason per capability (<c>StatusDetails.Code</c>), so this carries the code of whichever
        /// capability is blocking - the one the shop owner has to act on.
        /// </summary>
        [MaxLength(128)]
        public string? DisabledReason { get; set; }

        /// <summary>
        /// True once the tenant (or the provider) severed the connection between the platform and the account —
        /// <c>account.application.deauthorized</c>. Sales are refused while this is set.
        /// </summary>
        public bool Disconnected { get; set; }

        public DateTime Created { get; set; }

        public DateTime Updated { get; set; }
    }
}
