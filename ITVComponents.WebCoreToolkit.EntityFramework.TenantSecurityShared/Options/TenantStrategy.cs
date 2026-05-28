namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Options
{
    /// <summary>
    /// Selects the tenant-security model a consumer activates. Shared across the
    /// onboarding and (later) the consolidated tenant-security registration so the
    /// strategy is configured in a single place.
    /// </summary>
    public enum TenantStrategy
    {
        /// <summary>
        /// Flat tenants (AspNetCoreTenants).
        /// </summary>
        Flat,

        /// <summary>
        /// Hierarchical tenants (AspNetCoreTreeTenants).
        /// </summary>
        Tree
    }
}
