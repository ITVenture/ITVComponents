using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models
{
    /// <summary>
    /// A single purchased line-item of a <see cref="TenantSubscription"/> — either the base
    /// <see cref="Plan"/> (then <see cref="PlanId"/> is set) or an <see cref="AddOn"/> (then
    /// <see cref="AddOnId"/> is set). Mirrors one provider subscription-item.
    /// </summary>
    [Index(nameof(ProviderSubscriptionItemId), Name = "IX_TenantSubscriptionItem_ProviderItem")]
    public class TenantSubscriptionItem
    {
        [Key]
        public int TenantSubscriptionItemId { get; set; }

        public int TenantSubscriptionId { get; set; }

        /// <summary>Provider subscription-item identifier (e.g. Stripe subscription-item-ID).</summary>
        [MaxLength(256)]
        public string? ProviderSubscriptionItemId { get; set; }

        /// <summary>Set when this item is the base plan. Mutually exclusive with <see cref="AddOnId"/>.</summary>
        public int? PlanId { get; set; }

        /// <summary>Set when this item is an add-on. Mutually exclusive with <see cref="PlanId"/>.</summary>
        public int? AddOnId { get; set; }

        public int Quantity { get; set; } = 1;

        [ForeignKey(nameof(TenantSubscriptionId))]
        public virtual TenantSubscription? Subscription { get; set; }

        [ForeignKey(nameof(PlanId))]
        public virtual Plan? Plan { get; set; }

        [ForeignKey(nameof(AddOnId))]
        public virtual AddOn? AddOn { get; set; }
    }
}
