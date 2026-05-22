using System;
using System.Security.Claims;

namespace ITVComponents.WebCoreToolkit.ServiceShared.Diagnostics
{
    /// <summary>
    /// Default <see cref="IDiagnosticsQueryContext"/> implementation. Each hosting-model builds one of these from its
    /// own ambient state (MVC: HttpContext.User/RequestServices, Blazor: AuthenticationState/scoped provider).
    /// </summary>
    public class DiagnosticsQueryContext : IDiagnosticsQueryContext
    {
        public DiagnosticsQueryContext(ClaimsPrincipal user, IServiceProvider services)
        {
            User = user;
            Services = services;
        }

        /// <inheritdoc/>
        public ClaimsPrincipal User { get; }

        /// <inheritdoc/>
        public IServiceProvider Services { get; }
    }
}
