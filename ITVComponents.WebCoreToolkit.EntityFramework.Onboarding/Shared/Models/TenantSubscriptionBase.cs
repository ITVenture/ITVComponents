using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models
{
    /// <summary>
    /// Generic base for a tenant's subscription to a <see cref="Plan"/>. Tenant-level (not user-level)
    /// per design — see [[itvcomponents-billing-spec]]. Concrete derivatives bind <typeparamref name="TTenant"/>
    /// to the consumer-specific Tenant derivative.
    /// </summary>
    public abstract class TenantSubscriptionBase<TTenant>
        where TTenant : Tenant
    {
        [Key]
        public int TenantSubscriptionId { get; set; }

        public int TenantId { get; set; }

        public int PlanId { get; set; }

        /// <summary>
        /// Provider-specific customer identifier (e.g. Stripe customer-ID) created on first checkout.
        /// </summary>
        [MaxLength(256)]
        public string ProviderCustomerId { get; set; }

        /// <summary>
        /// Provider-specific subscription identifier (e.g. Stripe subscription-ID), populated via webhook.
        /// </summary>
        [MaxLength(256)]
        public string ProviderSubscriptionId { get; set; }

        public SubscriptionStatus Status { get; set; }

        public DateTime? CurrentPeriodStart { get; set; }

        public DateTime? CurrentPeriodEnd { get; set; }

        public bool CancelAtPeriodEnd { get; set; }

        public DateTime Created { get; set; }

        public DateTime Updated { get; set; }

        [ForeignKey(nameof(TenantId))]
        public virtual TTenant Tenant { get; set; }

        [ForeignKey(nameof(PlanId))]
        public virtual Plan Plan { get; set; }
    }
}
