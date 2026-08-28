using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments
{
    /// <summary>
    /// One volume-based waiver decision on a subscription invoice: "the tenant turned over X in the period that
    /// just ended, therefore the base fee on the invoice created now is (partly) waived". This is the single
    /// place where axis B (end customers pay the tenant) feeds back into axis A (the tenant pays the platform).
    /// <para>
    /// The row is written for EVERY decision, granted or not — it is both the audit trail and the idempotency
    /// guard. The provider delivers <c>invoice.created</c> more than once, and a second credit line would be
    /// real money given away, hence the unique index on (tenant, invoice).
    /// </para>
    /// <para>
    /// The measurement period is the one that CLOSED, not the one being billed: subscription invoices are raised
    /// in advance, so at invoice time nobody can know the turnover of the period the invoice covers. The waiver
    /// is therefore earned backwards and granted forwards.
    /// </para>
    /// </summary>
    [Index(nameof(TenantId), nameof(ProviderInvoiceId), IsUnique = true, Name = "IX_UniqueTenantFeeWaiverInvoice")]
    public class TenantFeeWaiver
    {
        [Key]
        public int TenantFeeWaiverId { get; set; }

        /// <summary>Logical tenant identifier. No FK — Billing stays tenant-model-agnostic.</summary>
        public int TenantId { get; set; }

        /// <summary>Start of the CLOSED period the turnover was measured over (UTC).</summary>
        public DateTime PeriodStartUtc { get; set; }

        /// <summary>End of the CLOSED period the turnover was measured over (UTC).</summary>
        public DateTime PeriodEndUtc { get; set; }

        /// <summary>
        /// Net turnover of that period in minor units: paid sales minus the refunds BOOKED in the same period
        /// (booking date of the refund, not the date of the sale it reverses).
        /// </summary>
        public long NetVolumeMinor { get; set; }

        /// <summary>The threshold that applied at decision time, in minor units.</summary>
        public long ThresholdMinor { get; set; }

        /// <summary>ISO-4217 currency the turnover and the threshold are expressed in.</summary>
        [MaxLength(3)]
        public string? Currency { get; set; }

        /// <summary>True when a credit was actually booked. False rows document the decision "not reached".</summary>
        public bool Granted { get; set; }

        /// <summary>Amount actually credited, in minor units. Never more than the invoice position itself.</summary>
        public long WaivedAmountMinor { get; set; }

        /// <summary>
        /// Provider invoice the decision belongs to. Required — it is half of the idempotency key, and a nullable
        /// column in a unique index does not behave the same on every database (SQL Server filters nulls out,
        /// PostgreSQL does not).
        /// </summary>
        [Required, MaxLength(256)]
        public string ProviderInvoiceId { get; set; } = string.Empty;

        /// <summary>The negative invoice item that carries the credit; null when nothing was granted.</summary>
        [MaxLength(256)]
        public string? ProviderInvoiceItemId { get; set; }

        /// <summary>
        /// Set when the credit could not be placed on the invoice itself (the window between creation and
        /// finalization is roughly an hour) and has to be carried over to the next invoice.
        /// </summary>
        public bool CarryOverPending { get; set; }

        public DateTime Created { get; set; }
    }
}
