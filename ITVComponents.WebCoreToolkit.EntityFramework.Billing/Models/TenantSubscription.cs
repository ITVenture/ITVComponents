using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models
{
    /// <summary>
    /// A tenant's subscription, mirroring the payment provider's subscription. Tenant-level (not user-level)
    /// per design. <see cref="TenantId"/> is a plain logical reference (int), NOT a foreign key to the
    /// tenant-security Tenant table — Billing is intentionally decoupled from the tenant model. One active
    /// subscription per tenant; its purchased line-items (base plan + add-ons) live in <see cref="Items"/>.
    /// </summary>
    [Index(nameof(TenantId), Name = "IX_TenantSubscription_Tenant")]
    [Index(nameof(ProviderSubscriptionId), Name = "IX_TenantSubscription_ProviderSubscription")]
    public class TenantSubscription
    {
        [Key]
        public int TenantSubscriptionId { get; set; }

        /// <summary>Logical tenant identifier. No FK — see class remarks.</summary>
        public int TenantId { get; set; }

        /// <summary>Provider customer identifier (e.g. Stripe customer-ID), created on first checkout.</summary>
        [MaxLength(256)]
        public string? ProviderCustomerId { get; set; }

        /// <summary>Provider subscription identifier (e.g. Stripe subscription-ID), populated via webhook.</summary>
        [MaxLength(256)]
        public string? ProviderSubscriptionId { get; set; }

        public SubscriptionStatus Status { get; set; }

        public DateTime? CurrentPeriodStart { get; set; }

        public DateTime? CurrentPeriodEnd { get; set; }

        public bool CancelAtPeriodEnd { get; set; }

        public DateTime Created { get; set; }

        public DateTime Updated { get; set; }

        public virtual ICollection<TenantSubscriptionItem> Items { get; set; } = new List<TenantSubscriptionItem>();
    }
}
