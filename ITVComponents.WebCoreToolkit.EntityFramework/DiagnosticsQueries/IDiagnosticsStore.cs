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
    public interface IDiagnosticsStore
    {
        /// <summary>
        /// Finds the demanded DiagnosticsQuery and returns it including Query-Arguments
        /// </summary>
        /// <param name="queryName">the name of the requested DiagnosticsQuery</param>
        /// <returns>a DiagnosticsQueryDefinition-Object containing all parameters and permissions required to execute it</returns>
        DiagnosticsQueryDefinition GetQuery(string queryName);

        /// <summary>
        /// Finds the demanded Dashboard-Item and returns it
        /// </summary>
        /// <param name="dashboardName">the name of the requested dashboard-item</param>
        /// <returns>the definition of the requested dashboard-item including the permissions required to use it</returns>
        DashboardWidgetDefinition GetDashboard(string dashboardName);

        /// <summary>
        /// Sets the User-Widgets for the given user
        /// </summary>
        /// <param name="userWidgets">the target widgets to add</param>
        /// <param name="userName">the user for which to register these widgets</param>
        /// <returns>an empty task</returns>
        Task SetUserWidgets(UserDashboardWidgetDefinition[] userWidgets, string userName);

        /// <summary>
        /// Gets an array containing all defined DashboardWidget-Definitions
        /// </summary>
        /// <returns>an array that contains all known dashboard-templates</returns>
        DashboardWidgetDefinition[] GetWidgetTemplates();

        /// <summary>
        /// Gets an array containing all User-Widgets
        /// </summary>
        /// <returns>an array that contains all assigned user-widgets.</returns>
        UserDashboardWidgetDefinition[] GetUserWidgets(string userName);
    }
}
