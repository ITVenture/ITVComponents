using System;
using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models
{
    /// <summary>
    /// Pre-tenant onboarding intent captured during the anonymous "direct onboarding" sign-up: the
    /// Identity user is created (unconfirmed) and the requested tenant/billing-profile payload is parked
    /// here, keyed by the account e-mail. After the user confirms the mail and logs in, the matching
    /// record is consumed to actually create the tenant (see <c>CompletePendingOnboardingAsync</c>).
    /// Not tenant-scoped (there is no tenant yet) and not subject to the onboarding global filters.
    /// </summary>
    public class PendingOnboarding
    {
        [Key]
        public int PendingOnboardingId { get; set; }

        /// <summary>Account e-mail this onboarding belongs to; matched against the user on completion.</summary>
        [Required, MaxLength(256)]
        public string Email { get; set; }

        /// <summary>Serialized billing-profile/tenant request (the create-tenant view model as JSON).</summary>
        public string PayloadJson { get; set; }

        public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

        public DateTime CreatedUtc { get; set; }

        /// <summary>Id of the Identity user created for this onboarding (no FK; plain bookkeeping).</summary>
        [MaxLength(450)]
        public string UserId { get; set; }

        /// <summary>Optional tenant-invitation token this onboarding was started from (used by the tree flow).</summary>
        [MaxLength(256)]
        public string InvitationToken { get; set; }
    }
}
