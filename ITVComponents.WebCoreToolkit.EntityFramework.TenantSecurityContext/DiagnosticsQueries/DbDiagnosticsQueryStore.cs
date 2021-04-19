using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.DiagnosticsQueries;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.DiagnosticsQueries
{
    /// <summary>
    /// DiagnosticsQueryStore that is bound to the Security Db-Context
    /// </summary>
    public class DbDiagnosticsQueryStore:IDiagnosticsQueryStore
    {
        private readonly SecurityContext dbContext;

        public DbDiagnosticsQueryStore(SecurityContext dbContext)
        {
            this.dbContext = dbContext;
        }

        /// <summary>
        /// Finds the demanded DiagnosticsQuery and returns it including Query-Arguments
        /// </summary>
        /// <param name="queryName">the name of the requested DiagnosticsQuery</param>
        /// <returns>a DiagnosticsQueryDefinition-Object containing all parameters and permissions required to execute it</returns>
        public DiagnosticsQueryDefinition GetQuery(string queryName)
        {
            var dbQuery = dbContext.DiagnosticsQueries.FirstOrDefault(n => n.DiagnosticsQueryName == queryName);
            if (dbQuery != null)
            {
                var retVal = new DiagnosticsQueryDefinition
                {
                    AutoReturn = dbQuery.AutoReturn,
                    DbContext = dbQuery.DbContext,
                    DiagnosticsQueryName = dbQuery.DiagnosticsQueryName,
                    Permission = dbQuery.Permission.PermissionName,
                    QueryText = dbQuery.QueryText
                };
                foreach (var diagnosticsQueryParameter in dbQuery.Parameters)
                {
                    retVal.Parameters.Add(new DiagnosticsQueryParameterDefinition
                    {
                        DefaultValue = diagnosticsQueryParameter.DefaultValue,
                        Format = diagnosticsQueryParameter.Format,
                        Optional = diagnosticsQueryParameter.Optional,
                        ParameterName = diagnosticsQueryParameter.ParameterName,
                        ParameterType = diagnosticsQueryParameter.ParameterType
                    });
                }

                return retVal;
            }

            return null;
        }
    }
}
