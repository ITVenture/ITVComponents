namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Options
{
    /// <summary>
    /// Selects the identity backend a consumer activates. Orthogonal to <see cref="TenantStrategy"/>
    /// (flat vs. hierarchical tenants): both axes are combined to pick the concrete security context.
    /// Configured once, in a single place, alongside the tenant strategy.
    /// </summary>
    public enum IdentityStrategy
    {
        /// <summary>
        /// ASP.NET Core Identity based security (string-keyed users; AspNetSecurityContext /
        /// AspNetTreeSecurityContext).
        /// </summary>
        CoreIdentity,

        /// <summary>
        /// Basic tenant-security model without ASP.NET Core Identity (int-keyed users; SecurityContext).
        /// </summary>
        BasicTenantSecurity
    }
}
