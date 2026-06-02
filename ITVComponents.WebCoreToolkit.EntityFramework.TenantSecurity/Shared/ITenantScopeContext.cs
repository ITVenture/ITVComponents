namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared
{
    /// <summary>
    /// Lightweight, non-generic view of the ambient tenant scope. Implemented by every tenant-security
    /// context (via <c>IBaseTenantContext&lt;...&gt;</c>) and lets tenant-scoped consumers read the current
    /// tenant without taking on the full generic context signature.
    /// </summary>
    public interface ITenantScopeContext
    {
        /// <summary>Id of the current tenant, or null when no tenant provider resolved one.</summary>
        int? CurrentTenantId { get; }
    }
}
