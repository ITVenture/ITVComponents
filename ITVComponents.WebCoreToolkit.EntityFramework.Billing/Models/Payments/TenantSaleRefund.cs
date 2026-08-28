using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments
{
    /// <summary>
    /// One refund booked against a <see cref="TenantSale"/>. Partial refunds are supported; the sale's status is
    /// derived from the SUM of its refunds, never from a single row.
    /// </summary>
    [Index(nameof(ProviderRefundId), Name = "IX_TenantSaleRefund_ProviderRefund")]
    public class TenantSaleRefund
    {
        [Key]
        public int TenantSaleRefundId { get; set; }

        public int TenantSaleId { get; set; }

        /// <summary>Refunded amount in minor units.</summary>
        public long AmountMinor { get; set; }

        /// <summary>
        /// How much of the platform commission actually went back with this refund, in minor units. Without this
        /// field the commission statement is wrong: the provider does NOT return the fee automatically, and
        /// whether it did is a per-refund decision (see <c>RefundApplicationFeeByDefault</c>).
        /// </summary>
        public long ApplicationFeeRefundedMinor { get; set; }

        /// <summary>Provider refund identifier (<c>re_...</c>).</summary>
        [MaxLength(256)]
        public string? ProviderRefundId { get; set; }

        /// <summary>Free-text reason recorded by the tenant (the provider only accepts a fixed set of its own).</summary>
        [MaxLength(512)]
        public string? Reason { get; set; }

        /// <summary>Provider refund status (<c>pending</c> / <c>succeeded</c> / <c>failed</c> / <c>canceled</c>).</summary>
        [MaxLength(32)]
        public string? Status { get; set; }

        public DateTime Created { get; set; }

        public virtual TenantSale? Sale { get; set; }
    }
}
