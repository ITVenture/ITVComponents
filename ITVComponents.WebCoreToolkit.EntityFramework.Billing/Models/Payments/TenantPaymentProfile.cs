using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments
{
    /// <summary>
    /// What the provider needs to know about a tenant BEFORE a connected account can be created, and what the
    /// tenant's own billing profile cannot answer. Exactly one row per tenant, written through the payout tab of
    /// the billing profile.
    /// <para>
    /// This is the input side; <see cref="TenantPaymentAccount"/> is the output side (what the provider says
    /// about the account afterwards). They are separate on purpose: this row survives an account that had to be
    /// thrown away, and it exists before there is any account at all.
    /// </para>
    /// <para>
    /// Only what is genuinely missing lives here. Name, e-mail, phone, address and VAT number are already on the
    /// billing profile and are read from there - duplicating them would mean two truths about the same company
    /// and a second place to keep current. Like the rest of Billing, <see cref="TenantId"/> is a plain logical
    /// reference (int) and NOT a foreign key to the tenant-security Tenant table.
    /// </para>
    /// </summary>
    [Index(nameof(TenantId), IsUnique = true, Name = "IX_UniqueTenantPaymentProfile")]
    public class TenantPaymentProfile
    {
        [Key]
        public int TenantPaymentProfileId { get; set; }

        /// <summary>Logical tenant identifier. No FK - see class remarks.</summary>
        public int TenantId { get; set; }

        /// <summary>
        /// ISO-3166 country the connected account is to be created in. Kept apart from the billing address on
        /// purpose: the account country is a legal fact about where the business is established, and it decides
        /// which payout rails exist. The provider fixes it at creation and never lets it change again - a wrong
        /// value means throwing the account away and starting over.
        /// </summary>
        [MaxLength(2)]
        public string? Country { get; set; }

        /// <summary>
        /// <c>individual</c> or <c>company</c>. Defaults from the billing profile's ProfileType, but stays
        /// separate: a one-person business can be registered as a company, and which of the two applies is the
        /// tenant's legal situation, not a consequence of how they signed up.
        /// </summary>
        [MaxLength(32)]
        public string? EntityType { get; set; }

        /// <summary>
        /// The address the provider writes to about THIS account - verification requests, payout problems. May
        /// differ from the invoice e-mail of the billing profile; empty means the billing profile's address is
        /// used.
        /// </summary>
        [MaxLength(320)]
        public string? ContactEmail { get; set; }

        /// <summary>
        /// The name the end customer sees on the payment page and on their statement. Empty means the billing
        /// profile's company or person name.
        /// </summary>
        [MaxLength(256)]
        public string? DisplayName { get; set; }

        /// <summary>
        /// Merchant category code (four digits) describing what is being sold. The provider asks for it during
        /// verification; supplying it up front saves the tenant a round trip through the hosted form.
        /// </summary>
        [MaxLength(4)]
        public string? MerchantCategoryCode { get; set; }

        /// <summary>
        /// The publicly reachable address of the shop. Part of what the provider verifies a business against;
        /// without it the hosted form asks for it.
        /// </summary>
        [MaxLength(512)]
        public string? BusinessUrl { get; set; }

        public DateTime Created { get; set; }

        public DateTime Updated { get; set; }
    }
}
