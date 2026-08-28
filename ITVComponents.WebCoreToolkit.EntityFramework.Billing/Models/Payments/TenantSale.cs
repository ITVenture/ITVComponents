using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments
{
    /// <summary>
    /// One end-customer purchase made in the name of a tenant. Only the total, a caption and the host's own
    /// reference are needed — line items are deliberately not modelled (see the Connect plan, section 2.4).
    /// <para>
    /// All amounts are in MINOR units (Rappen/cents) because that is what the provider bills in; converting
    /// back and forth per operation is what produces rounding drift in the fee.
    /// </para>
    /// </summary>
    [Index(nameof(TenantId), nameof(ExternalReference), IsUnique = true, Name = "IX_UniqueTenantSaleReference")]
    [Index(nameof(ProviderPaymentIntentId), Name = "IX_TenantSale_ProviderPaymentIntent")]
    [Index(nameof(ProviderSessionId), Name = "IX_TenantSale_ProviderSession")]
    public class TenantSale
    {
        [Key]
        public int TenantSaleId { get; set; }

        /// <summary>Logical tenant identifier. No FK — Billing stays tenant-model-agnostic.</summary>
        public int TenantId { get; set; }

        /// <summary>
        /// The host's own reference (order number). Together with <see cref="TenantId"/> this is unique and
        /// therefore carries the idempotency: recording the same reference twice returns the FIRST sale rather
        /// than creating a second one.
        /// </summary>
        [Required, MaxLength(128)]
        public string ExternalReference { get; set; } = string.Empty;

        /// <summary>What the end customer reads on the hosted payment page.</summary>
        [Required, MaxLength(256)]
        public string Description { get; set; } = string.Empty;

        /// <summary>Total in minor units.</summary>
        public long AmountMinor { get; set; }

        /// <summary>ISO-4217 currency of the sale.</summary>
        [Required, MaxLength(3)]
        public string Currency { get; set; } = string.Empty;

        /// <summary>
        /// The platform's commission in minor units, computed when the sale was recorded and then FROZEN. Never
        /// recomputed on read — otherwise a later configuration change would silently rewrite historic sales.
        /// </summary>
        public long ApplicationFeeMinor { get; set; }

        public TenantSaleStatus Status { get; set; }

        /// <summary>Provider checkout-session identifier (<c>cs_...</c>).</summary>
        [MaxLength(256)]
        public string? ProviderSessionId { get; set; }

        /// <summary>Provider payment-intent identifier (<c>pi_...</c>) — the way back from a webhook event.</summary>
        [MaxLength(256)]
        public string? ProviderPaymentIntentId { get; set; }

        /// <summary>Provider charge identifier (<c>ch_...</c>) — what a refund is issued against.</summary>
        [MaxLength(256)]
        public string? ProviderChargeId { get; set; }

        /// <summary>
        /// Connected account the sale was made on, copied from <see cref="TenantPaymentAccount"/> at creation.
        /// Kept on the sale so a webhook event can be checked against it: an event for a FOREIGN account must
        /// never touch this row.
        /// </summary>
        [MaxLength(256)]
        public string? ProviderAccountId { get; set; }

        /// <summary>Optional e-mail for the receipt. Does NOT create a customer profile at the provider.</summary>
        [MaxLength(256)]
        public string? CustomerEmail { get; set; }

        /// <summary>Additional data the host passed through, serialized as JSON.</summary>
        public string? MetadataJson { get; set; }

        /// <summary>The hosted payment page for this sale, as returned by the provider.</summary>
        [MaxLength(2048)]
        public string? CheckoutUrl { get; set; }

        public DateTime Created { get; set; }

        public DateTime Updated { get; set; }

        /// <summary>UTC moment the payment completed; null while unpaid.</summary>
        public DateTime? PaidUtc { get; set; }

        public virtual ICollection<TenantSaleRefund> Refunds { get; set; } = new List<TenantSaleRefund>();
    }
}
