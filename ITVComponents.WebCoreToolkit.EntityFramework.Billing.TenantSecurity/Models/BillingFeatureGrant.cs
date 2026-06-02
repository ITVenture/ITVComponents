using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.TenantSecurity.Models
{
    /// <summary>
    /// Adapter-owned bookkeeping row: records which <c>TenantFeatureActivation</c> a billing subscription
    /// created for a given (tenant, feature-key). The provisioner only ever touches activations referenced
    /// here, so it never modifies or revokes manually-granted feature activations.
    /// </summary>
    [Index(nameof(TenantId), nameof(FeatureKey), IsUnique = true, Name = "IX_UniqueBillingFeatureGrant")]
    public class BillingFeatureGrant
    {
        [Key]
        public int BillingFeatureGrantId { get; set; }

        /// <summary>Logical tenant identifier (matches the billing subscription's TenantId).</summary>
        public int TenantId { get; set; }

        /// <summary>The plan/add-on feature key that drove this grant.</summary>
        [Required, MaxLength(256)]
        public string FeatureKey { get; set; } = string.Empty;

        /// <summary>Resolved tenant-security feature id (<c>Feature.FeatureId</c>).</summary>
        public int FeatureId { get; set; }

        /// <summary>Id of the <c>TenantFeatureActivation</c> row this grant created and owns.</summary>
        public int TenantFeatureActivationId { get; set; }
    }
}
