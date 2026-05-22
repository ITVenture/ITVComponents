using System.Collections;
using System.Collections.Generic;
using ITVComponents.WebCoreToolkit.EntityFramework.Extensions;

namespace ITVComponents.WebCoreToolkit.ServiceShared.Diagnostics
{
    /// <summary>
    /// Default <see cref="IDiagnosticsQueryService"/> implementation. Stateless: every dependency is taken from the
    /// passed-in <see cref="IDiagnosticsQueryContext"/>, so the same instance is safe to share as a singleton.
    /// </summary>
    public class DiagnosticsQueryService : IDiagnosticsQueryService
    {
        /// <inheritdoc/>
        public IEnumerable Execute(string queryName, string area, IDiagnosticsQueryContext context, IDictionary<string, string> arguments)
        {
            var dataSource = context.Services.ContextForDiagnosticsQuery(queryName, area, out var query);
            if (dataSource == null)
            {
                return null;
            }

            return dataSource.RunDiagnosticsQuery(query, context.User, context.Services, arguments);
        }
    }
}
