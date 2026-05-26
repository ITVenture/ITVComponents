namespace ITVComponents.WebCoreToolkit.Blazor.Security
{
    /// <summary>
    /// Selects where <see cref="ScopedPermissionScope"/> picks up the tenant override for the current circuit.
    /// </summary>
    public enum TenantSource
    {
        /// <summary>
        /// Read the tenant from the URL query (e.g. <c>?tenant=ADM</c>). Default — keeps backward compatibility
        /// with hosts that emit a flat <c>&lt;base href="/" /&gt;</c> and rely on <see cref="TenantUrlGuard"/>
        /// to keep the parameter sticky across in-circuit navigation.
        /// </summary>
        Query = 0,

        /// <summary>
        /// Read the tenant from the first path segment of <c>NavigationManager.BaseUri</c>. The host must emit
        /// a dynamic <c>&lt;base href="/{tenant}/"&gt;</c> in <c>App.razor</c>/<c>_Host.cshtml</c> so the path
        /// segment is part of every URL the circuit produces. Flat <c>@page "/foo"</c> routes inside the libs
        /// stay unchanged — they are matched relative to the base href. Tenant switching still requires a
        /// full reload (<c>NavigateTo("/&lt;new&gt;/", forceLoad: true)</c>) so a fresh circuit picks up the
        /// new base href.
        /// </summary>
        PathSegment = 1
    }
}
