namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models
{
    public enum InvitationStatus
    {
        None,
        Pending,
        Committed,
        Revoked,

        /// <summary>
        /// Time-expired invitation. Only used by token-based invitations (e.g. <c>TenantInvitationBase</c>)
        /// that carry an <c>ExpiresUtc</c>; appended at the end so existing stored values stay stable.
        /// </summary>
        Expired
    }
}
