using System.Collections;
using System.Collections.Generic;

namespace ITVComponents.WebCoreToolkit.ServiceShared.Diagnostics
{
    /// <summary>
    /// Host-neutral entry-point for running a Diagnostics-Query. Resolves the configured data-source, executes the
    /// named query and returns its raw result. Both the MVC adapter and the Blazor adapter consume this service so
    /// the lookup/permission/execute sequence lives in exactly one place.
    /// </summary>
    public interface IDiagnosticsQueryService
    {
        /// <summary>
        /// Executes the named Diagnostics-Query.
        /// </summary>
        /// <param name="queryName">the name of the Diagnostics-Query to run</param>
        /// <param name="area">the optional area used for area-driven plugins</param>
        /// <param name="context">the neutral execution context (identity + services)</param>
        /// <param name="arguments">the query arguments</param>
        /// <returns>the query result, or null when the query was not found or access was denied</returns>
        IEnumerable Execute(string queryName, string area, IDiagnosticsQueryContext context, IDictionary<string, string> arguments);
    }
}
