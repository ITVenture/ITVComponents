using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.DiagnosticsQueries;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Net.ViewModel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Net.Handlers
{
    internal static class WidgetHandler
    {
        /// <summary>
        /// Gets a specific Widget as raw definition or as customized User-Widget
        /// </summary>
        /// <param name="context">the http-context in which the Widget is being executed</param>
        /// <param name="widgetName">the system-name of the Widget being fetched.</param>
        /// <param name="userWidgetId">the id of the user-settings to use for displaying the provided widget</param>
        /// <response code="200">a Json with the Widget and its parameters</response>
        /// <response code="404">a not-found when the given Widget (or user-widget) does not exist</response>
        public static async Task<IResult> Get(HttpContext context, string widgetName, int userWidgetId = -1)
        {
            bool hasId = userWidgetId != -1;
            var dbContext = context.RequestServices.GetService<IDiagnosticsStore>();
            if (dbContext != null)
            {
                var model = !hasId
                    ? dbContext.GetDashboard(widgetName)
                    : dbContext.GetDashboard(widgetName, userWidgetId);
                if (model != null && context.RequestServices.VerifyUserPermissions(new[]
                        { model.DiagnosticsQuery.Permission }))
                {
                    return Results.Json(model.ToViewModel<DashboardWidgetDefinition, DBWidget>(
                        (e, v) =>
                        {
                            v.QueryName = e.DiagnosticsQuery.DiagnosticsQueryName;
                            if (e.Params.Count != 0)
                            {
                                v.Params = e.Params.Select(n =>
                                    new DBWidgetParam
                                    {
                                        InputType = n.InputType.ToString(),
                                        InputConfig = n.InputConfig,
                                        ParameterName = n.ParameterName
                                    }).ToArray();
                            }
                        }), new JsonSerializerOptions());
                }
            }

            return Results.NotFound();
        }

        /// <summary>
        /// Saves the Widget-Configuration for the current user
        /// </summary>
        /// <param name="context">the http-context in which the Widget is being executed</param>
        /// <param name="widgets">A Json holding configuration and position of the user-selected widgets.</param>
        /// <response code="200">a Json that represents the resulting widget-configuration after the provided data was saved</response>
        /// <response code="404">a not-found when no data was posted, or when there's no DiagnosticsStore to save the data to.</response>
        public static async Task<IResult> Set(HttpContext context, [FromBody]DBWidget[] widgets)
        {
            var connection = context.RequestServices.GetService<IDiagnosticsStore>();
            //var widgets = await context.Request.ReadFromJsonAsync<DBWidget[]>();
            if (connection != null && widgets != null)
            {
                var tmp = await connection.SetUserWidgets(
                    (from t in widgets
                        select new DashboardWidgetDefinition
                        {
                            Area = t.Area,
                            CustomQueryString = t.CustomQueryString,
                            DisplayName = t.DisplayName,
                            SortOrder = t.SortOrder,
                            SystemName = t.SystemName,
                            Template = t.Template,
                            DashboardWidgetId = t.DashboardWidgetId,
                            UserWidgetId = t.UserWidgetId,
                        }).ToArray(), context.User.Identity.Name);

                var ret = tmp
                    .Select(n => new DBWidget
                    {
                        UserWidgetId = n.UserWidgetId,
                        CustomQueryString = n.CustomQueryString,
                        SortOrder = n.SortOrder,
                        DashboardWidgetId = n.DashboardWidgetId,
                        DisplayName = n.DisplayName,
                        TitleTemplate = n.TitleTemplate,
                        QueryName = n.DiagnosticsQuery.DiagnosticsQueryName,
                        SystemName = n.SystemName,
                        Template = n.Template,
                        Area = n.Area
                    }).ToArray();
                for (int i = 0; i < ret.Length; i++)
                {
                    ret[i].LocalRef = widgets[i].LocalRef;
                }

                //LogEnvironment.LogEvent(Stringify(formsDictionary), LogSeverity.Report);
                return Results.Json(ret, new JsonSerializerOptions());
            }

            return Results.NotFound();
        }
    }
}
