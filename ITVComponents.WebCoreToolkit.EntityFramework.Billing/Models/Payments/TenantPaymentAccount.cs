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

        /// <summary>Account type as created at the provider (<c>express</c> / <c>standard</c>).</summary>
        [MaxLength(32)]
        public string AccountType { get; set; } = "express";

        /// <summary>
        /// ISO-3166 country the account was created in. Fixed at creation — the provider does NOT allow changing
        /// it afterwards; a wrong value means throwing the account away and starting over.
        /// </summary>
        [MaxLength(2)]
        public string? Country { get; set; }

        /// <summary>ISO-4217 default currency of the connected account.</summary>
        [MaxLength(3)]
        public string? DefaultCurrency { get; set; }

        /// <summary>Mirror of the provider flag: the account may accept payments.</summary>
        public bool ChargesEnabled { get; set; }

        /// <summary>Mirror of the provider flag: the account may receive payouts.</summary>
        public bool PayoutsEnabled { get; set; }

        /// <summary>Mirror of the provider flag: the tenant finished the hosted onboarding form.</summary>
        public bool DetailsSubmitted { get; set; }

        /// <summary>
        /// Raw mirror of the provider's outstanding/overdue requirements (JSON), for display. Deliberately not
        /// modelled further: the shape is the provider's and changes without notice.
        /// </summary>
        public string? RequirementsJson { get; set; }

        /// <summary>Provider reason why the account is currently disabled; null when it is fine.</summary>
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
