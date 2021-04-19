using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.DiagnosticsQueries
{
    /// <summary>
    /// Implement this interface to provide a mechanism to select DiagnosticQuery definitions
    /// </summary>
    public interface IDiagnosticsQueryStore
    {
        /// <summary>
        /// Finds the demanded DiagnosticsQuery and returns it including Query-Arguments
        /// </summary>
        /// <param name="queryName">the name of the requested DiagnosticsQuery</param>
        /// <returns>a DiagnosticsQueryDefinition-Object containing all parameters and permissions required to execute it</returns>
        DiagnosticsQueryDefinition GetQuery(string queryName);
    }
}
