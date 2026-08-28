using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing
{
    /// <summary>
    /// Second, opt-in context contract of the Billing package: the tables a tenant needs to RECEIVE payments
    /// from its own end customers (axis B, connected accounts).
    /// <para>
    /// Deliberately separate from <see cref="IBillingContext"/> rather than an extension of it. A host that only
    /// sells subscriptions implements <see cref="IBillingContext"/> and gets neither the tables nor a migration;
    /// a host that only runs shops implements only this one. Adding the sets to the existing contract would have
    /// been a breaking change for every consumer already on axis A.
    /// </para>
    /// Call <c>modelBuilder.ConfigurePayments()</c> from <c>OnModelCreating</c> to map the entities.
    /// </summary>
    public interface IPaymentsContext
    {
        DbSet<TenantPaymentAccount> TenantPaymentAccounts { get; set; }

        DbSet<TenantSale> TenantSales { get; set; }

        DbSet<TenantSaleRefund> TenantSaleRefunds { get; set; }

        /// <summary>
        /// Audit trail and idempotency guard of the volume-based fee waiver. Present even when the waiver is
        /// switched off — the table costs nothing and switching it on later must not require a second migration.
        /// </summary>
        DbSet<TenantFeeWaiver> TenantFeeWaivers { get; set; }
    }
}
