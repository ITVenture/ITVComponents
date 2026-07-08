using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing
{
    /// <summary>
    /// Contract a consuming <see cref="DbContext"/> implements to host the billing tables. Standalone — does
    /// NOT require a tenant-security context, keeping Billing reusable. Call
    /// <c>modelBuilder.ConfigureBilling()</c> from <c>OnModelCreating</c> to map the entities.
    /// </summary>
    public interface IBillingContext
    {
        DbSet<Plan> Plans { get; set; }

        DbSet<PlanPrice> PlanPrices { get; set; }

        DbSet<PlanFeature> PlanFeatures { get; set; }

        DbSet<AddOn> AddOns { get; set; }

        DbSet<PlanAddOn> PlanAddOns { get; set; }

        DbSet<PlanAddOnPrice> PlanAddOnPrices { get; set; }

        DbSet<AddOnFeature> AddOnFeatures { get; set; }

        DbSet<TenantSubscription> TenantSubscriptions { get; set; }

        DbSet<TenantSubscriptionItem> TenantSubscriptionItems { get; set; }
    }
}
