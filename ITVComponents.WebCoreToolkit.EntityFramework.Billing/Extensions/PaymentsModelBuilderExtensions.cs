using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Extensions
{
    public static class PaymentsModelBuilderExtensions
    {
        /// <summary>
        /// Maps the payments entities (<see cref="TenantPaymentAccount"/>, <see cref="TenantSale"/>,
        /// <see cref="TenantSaleRefund"/>, <see cref="TenantFeeWaiver"/>) of axis B. A separate call next to
        /// <c>ConfigureBilling()</c>, so a host can have one axis without the other.
        /// <para>
        /// Keys and indexes come from data annotations; this adds the relationship and its delete behaviour. No
        /// tenant global filter is applied — like the rest of Billing, the service layer addresses the tenant
        /// explicitly by <c>TenantId</c> (a webhook runs entirely outside any tenant scope).
        /// </para>
        /// </summary>
        public static ModelBuilder ConfigurePayments(this ModelBuilder modelBuilder)
        {
            // A refund has no meaning without its sale, so it goes with it. The sale itself is never deleted in
            // normal operation — it is the receipt of a real payment.
            modelBuilder.Entity<TenantSaleRefund>()
                .HasOne(r => r.Sale).WithMany(s => s.Refunds)
                .HasForeignKey(r => r.TenantSaleId).OnDelete(DeleteBehavior.Cascade);

            return modelBuilder;
        }
    }
}
