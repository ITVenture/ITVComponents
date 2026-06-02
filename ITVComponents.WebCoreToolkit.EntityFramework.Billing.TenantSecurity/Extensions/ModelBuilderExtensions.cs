using ITVComponents.WebCoreToolkit.EntityFramework.Billing.TenantSecurity.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.TenantSecurity.Extensions
{
    public static class ModelBuilderExtensions
    {
        /// <summary>
        /// Maps the adapter-owned <see cref="BillingFeatureGrant"/> bookkeeping table. Call from the
        /// consuming context's <c>OnModelCreating</c> (alongside <c>ConfigureBilling()</c>). No tenant global
        /// filter — the provisioner queries by explicit TenantId and ignores filters.
        /// </summary>
        public static ModelBuilder ConfigureBillingFeatureGrants(this ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BillingFeatureGrant>();
            return modelBuilder;
        }
    }
}
