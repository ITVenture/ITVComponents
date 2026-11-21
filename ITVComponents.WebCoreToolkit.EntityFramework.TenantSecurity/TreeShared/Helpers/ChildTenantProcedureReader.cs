using System.Collections.Generic;
using System.Linq;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Helpers
{
    /// <summary>
    /// Reads the result of the child-tenants-with-permissions procedures (SQL Server) or functions (PostgreSQL)
    /// behind <c>ChildTenantsWith</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why only the ids are read.</b> The tenants used to be mapped straight from the procedure with
    /// <c>Tenants.FromSqlRaw(...)</c>. For that, EF demands every mapped column in the result, computed ones
    /// included - and the procedures are database objects that a consumer rolls out with a migration. When
    /// <c>TenantNameLower</c> was added to the model, the procedures did not return it, and every call threw
    /// "The required column 'TenantNameLower' was not present". Any further tenant column would have done the same.
    /// </para>
    /// <para>
    /// Now the procedure decides only WHICH tenants qualify; the rows themselves are loaded through the model. A
    /// new column - computed or not - comes along without touching the procedures, and the procedures already
    /// rolled out keep working without a migration. The cost is a second round-trip.
    /// </para>
    /// <para>
    /// The query filters are ignored on purpose: visibility is what the procedure computed, it is not decided a
    /// second time by the filter of the context (same as <c>ResolveTenantIdDetached</c>).
    /// </para>
    /// </remarks>
    public static class ChildTenantProcedureReader
    {
        /// <summary>
        /// Runs the given procedure call, then loads the tenants whose ids it returned.
        /// </summary>
        /// <param name="context">the security context the procedure runs on</param>
        /// <param name="tenants">the tenant set of that context</param>
        /// <param name="sql">the raw call of the procedure / function; it must return a column <c>TenantId</c></param>
        /// <param name="parameters">the parameters of the call</param>
        /// <returns>the tenants in the order the procedure returned them</returns>
        public static HierarchyTenant[] ReadTenants(DbContext context, IQueryable<HierarchyTenant> tenants, string sql, params object[] parameters)
        {
            // Materialize first: an "exec" is not composable, and the second query must not run while the
            // reader of the first one is still open on the same connection.
            var ids = context.Database.SqlQueryRaw<ProcedureTenantRow>(sql, parameters)
                .AsEnumerable()
                .Select(r => r.TenantId)
                .Distinct()
                .ToList();
            if (ids.Count == 0)
            {
                return System.Array.Empty<HierarchyTenant>();
            }

            var loaded = tenants.IgnoreQueryFilters()
                .Where(t => ids.Contains(t.TenantId))
                .ToDictionary(t => t.TenantId);
            return ids.Where(loaded.ContainsKey).Select(id => loaded[id]).ToArray();
        }

        /// <summary>
        /// The one column of the procedure result that is read. All other columns of the result are ignored.
        /// </summary>
        /// <remarks>
        /// Not sealed, with a virtual property: <c>SqlQueryRaw</c> maps it as an ad-hoc entity type of the context, and the
        /// security contexts run with proxies (<c>UseLazyLoadingProxies</c>), which reject a sealed entity type -
        /// "Entity type 'ProcedureTenantRow' is sealed".
        /// </remarks>
        public class ProcedureTenantRow
        {
            public virtual int TenantId { get; set; }
        }
    }
}
