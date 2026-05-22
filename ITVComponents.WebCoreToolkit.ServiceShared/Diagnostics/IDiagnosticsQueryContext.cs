using System;
using System.Security.Claims;

namespace ITVComponents.WebCoreToolkit.ServiceShared.Diagnostics
{
    /// <summary>
    /// Neutral execution-context for a Diagnostics-Query. Carries the identity and the service-provider that a query
    /// requires, without binding the caller to a specific hosting-model (MVC HttpContext or Blazor circuit).
    /// </summary>
    public interface IDiagnosticsQueryContext
    {
        /// <summary>
        /// Gets the principal on whose behalf the query is being executed. May be null for anonymous execution.
        /// </summary>
        ClaimsPrincipal User { get; }

        /// <summary>
        /// Gets the service-provider that the query uses to resolve identity/permission services.
        /// </summary>
        IServiceProvider Services { get; }
    }
}
