using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Extensions
{
    public static class ModelBuilderExtensions
    {
        /// <summary>
        /// Maps the billing entities (<see cref="Plan"/>, <see cref="PlanFeature"/>, <see cref="AddOn"/>,
        /// <see cref="AddOnFeature"/>, <see cref="TenantSubscription"/>, <see cref="TenantSubscriptionItem"/>).
        /// Call from the consuming context's <c>OnModelCreating</c>. Keys/indexes/FKs come from data
        /// annotations; this configures decimal precision and safe delete behaviour. No tenant global filter is
        /// applied — Billing is tenant-model-agnostic, the service layer queries by <c>TenantId</c> explicitly.
        /// </summary>
        public static ModelBuilder ConfigureBilling(this ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PlanPrice>().Property(p => p.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<AddOnPrice>().Property(p => p.Amount).HasPrecision(18, 2);

            modelBuilder.Entity<PlanPrice>()
                .HasOne(p => p.Plan).WithMany(p => p.Prices)
                .HasForeignKey(p => p.PlanId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AddOnPrice>()
                .HasOne(p => p.AddOn).WithMany(a => a.Prices)
                .HasForeignKey(p => p.AddOnId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlanFeature>()
                .HasOne(f => f.Plan).WithMany(p => p.Features)
                .HasForeignKey(f => f.PlanId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AddOnFeature>()
                .HasOne(f => f.AddOn).WithMany(a => a.Features)
                .HasForeignKey(f => f.AddOnId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TenantSubscriptionItem>()
                .HasOne(i => i.Subscription).WithMany(s => s.Items)
                .HasForeignKey(i => i.TenantSubscriptionId).OnDelete(DeleteBehavior.Cascade);

            // Catalog references from a purchased item: never cascade-delete a plan/add-on through its items.
            modelBuilder.Entity<TenantSubscriptionItem>()
                .HasOne(i => i.Plan).WithMany()
                .HasForeignKey(i => i.PlanId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TenantSubscriptionItem>()
                .HasOne(i => i.AddOn).WithMany()
                .HasForeignKey(i => i.AddOnId).OnDelete(DeleteBehavior.Restrict);

            return modelBuilder;
        }
    }
}
