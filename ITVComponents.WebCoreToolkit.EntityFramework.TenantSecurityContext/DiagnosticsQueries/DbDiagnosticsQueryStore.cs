using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.DiagnosticsQueries;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.DiagnosticsQueries
{
    /// <summary>
    /// DiagnosticsQueryStore that is bound to the Security Db-Context
    /// </summary>
    public class DbDiagnosticsQueryStore:IDiagnosticsStore
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

        /// <summary>
        /// Finds the demanded Dashboard-Item and returns it
        /// </summary>
        /// <param name="dashboardName">the name of the requested dashboard-item</param>
        /// <returns>the definition of the requested dashboard-item including the permissions required to use it</returns>
        public DashboardWidgetDefinition GetDashboard(string dashboardName)
        {
            var tmp = dbContext.Widgets.First(n => n.SystemName == dashboardName);
            var retVal = GetDashboardItem(tmp);

            return retVal;
        }

        /// <summary>
        /// Sets the User-Widgets for the given user
        /// </summary>
        /// <param name="userWidgets">the target widgets to add</param>
        /// <param name="userName">the user for which to register these widgets</param>
        /// <returns>an empty task</returns>
        public async Task SetUserWidgets(UserDashboardWidgetDefinition[] userWidgets, string userName)
        {
            var tmp = dbContext.ShowAllTenants;
            try
            {
                dbContext.ShowAllTenants = false;
                if (dbContext.CurrentTenantId != null)
                {
                    var tenantId = dbContext.CurrentTenantId.Value;
                    var dbwidgets = dbContext.UserWidgets.OrderBy(n => n.SortOrder).ToArray();
                    var allIds = dbwidgets.Select(n => n.Widget.SystemName)
                        .Union(userWidgets.Select(n => n.DashboardWidgetName)).Distinct().ToArray();
                    var selection = (from i in allIds
                                     join d in dbwidgets on i equals d.Widget.SystemName into j1
                                     from lj1 in j1.DefaultIfEmpty()
                                     join p in userWidgets on i equals p.DashboardWidgetName into j2
                                     from lj2 in j2.DefaultIfEmpty()
                                     join w in dbContext.Widgets on i equals w.SystemName
                                     select new { Id = w.DashboardWidgetId, Db = lj1, Posted = lj2 }).ToArray();
                    foreach (var item in selection)
                    {
                        if (item.Db == null)
                        {
                            dbContext.UserWidgets.Add(new UserWidget
                            {
                                DashboardWidgetId = item.Id,
                                SortOrder = item.Posted.SortOrder,
                                TenantId = tenantId,
                                UserName = userName
                            });
                        }
                        else if (item.Posted == null)
                        {
                            dbContext.UserWidgets.Remove(item.Db);
                        }
                        else
                        {
                            item.Db.SortOrder = item.Posted.SortOrder;
                        }
                    }

                    await dbContext.SaveChangesAsync();
                    //LogEnvironment.LogEvent(Stringify(formsDictionary), LogSeverity.Report);
                }
            }
            finally
            {
                dbContext.ShowAllTenants = tmp;
            }
        }

        /// <summary>
        /// Gets an array containing all defined DashboardWidget-Definitions
        /// </summary>
        /// <returns>an array that contains all known dashboard-templates</returns>
        public DashboardWidgetDefinition[] GetWidgetTemplates()
        {
            return (from t in dbContext.Widgets.ToArray()
                orderby t.DisplayName
                select GetDashboardItem(t)).ToArray();
        }

        /// <summary>
        /// Gets an array containing all User-Widgets
        /// </summary>
        /// <returns>an array that contains all assigned user-widgets.</returns>
        public UserDashboardWidgetDefinition[] GetUserWidgets(string userName)
        {
            var tmp = dbContext.ShowAllTenants;
            try
            {
                var tmpUw = dbContext.UserWidgets.OrderBy(n => n.SortOrder).ToArray();
                return (from t in tmpUw
                    select new UserDashboardWidgetDefinition
                    {
                        DashboardWidgetName = t.Widget.SystemName,
                        SortOrder = t.SortOrder,
                        UserName = t.UserName,
                        Widget = GetDashboardItem(t.Widget)
                    }).ToArray();
            }
            finally
            {
                dbContext.ShowAllTenants = tmp;
            }
        }

        /// <summary>
        /// Creates a basic-definition of a Widget-Definition from a DashbaordWidget entity
        /// </summary>
        /// <param name="tmp">the entity from which to create the definition</param>
        /// <returns>a complete widget-definition</returns>
        private DashboardWidgetDefinition GetDashboardItem(DashboardWidget tmp)
        {
            return new DashboardWidgetDefinition
            {
                Area = tmp.Area,
                CustomQueryString = tmp.CustomQueryString,
                DiagnosticsQuery = GetQuery(tmp.DiagnosticsQuery.DiagnosticsQueryName),
                DisplayName = tmp.DisplayName,
                SystemName = tmp.SystemName,
                Template = tmp.Template
            };
        }
    }
}
