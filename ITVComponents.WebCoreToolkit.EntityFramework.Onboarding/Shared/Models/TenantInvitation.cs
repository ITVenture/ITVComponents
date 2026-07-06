using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models
{
    /// <summary>
    /// A parent tenant's invitation for someone to onboard a NEW sub-tenant underneath it (tree scenario).
    /// The recipient follows the tokenized link; once accepted, the created child tenant is attached to
    /// <see cref="ParentTenantId"/>. Unlike the employee invitation (which lets a user join an existing
    /// tenant), this drives tenant <i>creation</i> with a pre-pinned parent.
    /// <para>
    /// Deliberately FK-less (scalar id columns only), mirroring <see cref="PendingOnboarding"/>: it is
    /// admin-managed and token-accessed (the accept lookup runs anonymously, before any tenant context
    /// exists), so it stays out of the tenant global filters and avoids SQL-Server multiple-cascade-path
    /// conflicts from several relationships pointing at Tenant/User.
    /// </para>
    /// </summary>
    [Index(nameof(Token), IsUnique = true)]
    public class TenantInvitation
    {
        [Key]
        public int TenantInvitationId { get; set; }

        /// <summary>Id of the inviting (parent) tenant; the accepted child tenant is attached here.</summary>
        public int ParentTenantId { get; set; }

        /// <summary>Recipient e-mail (informative; the link works by token, not by e-mail match).</summary>
        [MaxLength(256)]
        public string Email { get; set; }

        /// <summary>Secure, unique token carried by the invitation link.</summary>
        [Required, MaxLength(256)]
        public string Token { get; set; }

        public DateTime ExpiresUtc { get; set; }

        public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

        /// <summary>Optional role to grant the child tenant's admin once the tenant is created.</summary>
        [MaxLength(256)]
        public string RoleName { get; set; }

        /// <summary>Optional tenant template to apply to the created child tenant.</summary>
        [MaxLength(256)]
        public string TemplateName { get; set; }

        /// <summary>
        /// Id of the user that issued the invitation (no FK; bookkeeping). Deliberately the user id, not a
        /// TenantUser id: under the hierarchy strategy the issuing admin may hold the inviting tenant purely
        /// through an inherited parent membership and have no TenantUser row on it.
        /// </summary>
        [MaxLength(450)]
        public string CreatedByUserId { get; set; }

        public DateTime CreatedUtc { get; set; }

        /// <summary>Id of the user that accepted the invitation (no FK; bookkeeping).</summary>
        [MaxLength(450)]
        public string AcceptedByUserId { get; set; }

        /// <summary>Id of the child tenant created from this invitation (no FK; bookkeeping).</summary>
        public int? ChildTenantId { get; set; }
    }
}
