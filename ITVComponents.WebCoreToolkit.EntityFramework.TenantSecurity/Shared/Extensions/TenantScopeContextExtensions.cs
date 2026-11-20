using System;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Extensions
{
    /// <summary>
    /// Guards for code that cannot work without a resolved current tenant.
    /// </summary>
    public static class TenantScopeContextExtensions
    {
        /// <summary>
        /// Gets the id of the current tenant, or throws an exception that names the reason when none is resolved.
        /// </summary>
        /// <param name="context">the tenant-aware context</param>
        /// <param name="currentTenantName">the name of the current tenant, as the context sees it</param>
        /// <param name="operation">what needs the tenant, for the message (e.g. "Loading plugin 'x'")</param>
        /// <returns>the id of the current tenant</returns>
        /// <remarks>
        /// Replaces <c>CurrentTenantId.Value</c>. That threw "Nullable object must have a value", which names
        /// neither the operation nor the cause. The cause sits elsewhere most of the time: no tenant came from the
        /// permission scope of the caller, or the tenant name does not exist. After an upgrade from 4.x it is
        /// typically <c>TrustedFullAccessComponents</c> still holding 4.x type names.
        /// </remarks>
        public static int RequireCurrentTenantId(this ITenantScopeContext context, string currentTenantName, string operation)
        {
            var id = context.CurrentTenantId;
            if (id != null)
            {
                return id.Value;
            }

            var reason = string.IsNullOrEmpty(currentTenantName)
                ? "the context has no current tenant (no tenant provider, or the permission scope of the caller delivered none)"
                : $"no tenant named '{currentTenantName}' exists";
            throw new InvalidOperationException(
                $"{operation} requires a current tenant, but {reason}. Check the permission scope of the caller. After an upgrade from 4.x, also check the type names in TrustedFullAccessComponents and other stored type names: a component that is no longer recognized there can leave the tenant unresolved.");
        }
    }
}
