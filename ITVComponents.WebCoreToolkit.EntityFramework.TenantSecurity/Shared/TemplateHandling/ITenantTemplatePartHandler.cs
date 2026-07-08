using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.TemplateHandling
{
    /// <summary>
    /// Optional, decoupled extension point for the tenant-template engine. A handler contributes one named
    /// "part" to a tenant template: during extract it returns a serialized payload (stored under
    /// <see cref="PartKey"/> in <c>TenantTemplateMarkup.Extensions</c>); during apply it recreates that part in
    /// the target tenant. Handlers operate on the template engine's own <see cref="DbContext"/> (passed in) so
    /// they share its scope/transaction, and should resolve cross-references (e.g. roles) by name. Registered in
    /// DI; the engine runs all registered handlers. Apply runs after the built-in sections (roles etc.) have
    /// been persisted, so by-name lookups resolve. The engine stays oblivious to what the part contains, which
    /// lets feature libraries (e.g. onboarding) extend tenant templates without the engine referencing them.
    /// </summary>
    public interface ITenantTemplatePartHandler
    {
        /// <summary>Stable key identifying this part in the template's Extensions bag.</summary>
        string PartKey { get; }

        /// <summary>
        /// Extracts this part from <paramref name="tenantId"/> as a serialized payload, or null/empty to
        /// contribute nothing to the template.
        /// </summary>
        string Extract(DbContext db, int tenantId);

        /// <summary>
        /// Applies a previously extracted payload to <paramref name="tenantId"/>. <paramref name="mode"/> is the
        /// resolved apply mode for this part (never <see cref="TemplateApplyMode.Auto"/>): <see cref="TemplateApplyMode.Additive"/>
        /// should only upsert, <see cref="TemplateApplyMode.Forced"/> may additionally prune entries not in the payload.
        /// </summary>
        void Apply(DbContext db, int tenantId, string payload, TemplateApplyMode mode);
    }
}
