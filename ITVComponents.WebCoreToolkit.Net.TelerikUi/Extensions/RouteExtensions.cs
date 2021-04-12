using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Extensions
{
    public static class RouteExtensions
    {
        public static IEndpointConventionBuilder UseFilteredAutoForeignKeys(this IEndpointRouteBuilder builder, string explicitTenantParam, bool forAreas, bool withAuthorization = true)
        {
            bool forExplicitTenants = !string.IsNullOrEmpty(explicitTenantParam);
            ContextExtensions.Init();
            RequestDelegate dlg = async context =>
            {
                var connection = (string) context.Request.RouteValues["connection"];
                var table = (string) context.Request.RouteValues["table"];
                string area = null;
                if (context.Request.RouteValues.ContainsKey("area"))
                {
                    area = (string) context.Request.RouteValues["area"];
                }

                RouteData routeData = context.GetRouteData();
                ActionDescriptor actionDescriptor = new ActionDescriptor();
                ActionContext actionContext = new ActionContext(context, routeData, actionDescriptor);
                FormReader former = new FormReader(context.Request.Body);
                var formsDictionary = await former.ReadFormAsync();
                var dbContext = context.RequestServices.ContextForFkQuery(connection, area);
                if (dbContext != null)
                {
                    //LogEnvironment.LogEvent(Stringify(formsDictionary), LogSeverity.Report);
                    var newDic = TranslateForm(formsDictionary, true);
                    JsonResult result = new JsonResult(dbContext.ReadForeignKey(table, postedFilter: newDic).ToDummyDataSourceResult());
                    await result.ExecuteResultAsync(actionContext);
                    return;
                }

                StatusCodeResult notFound = new NotFoundResult();
                await notFound.ExecuteResultAsync(actionContext);
            };
            var tmp = builder.MapPost($"{(forExplicitTenants?$"/{{{explicitTenantParam}:permissionScope}}":"")}{(forAreas?"/{area:exists}":"")}/ForeignKey/{{connection:alpha}}/{{table:alpha}}", dlg);

            if (withAuthorization)
            {
                tmp.RequireAuthorization();
            }

            return tmp;
        }

        /// <summary>
        /// Translates a specific filter to a Dictionary that is processable by the Context-Extensions for ForeignKey processing
        /// </summary>
        /// <param name="values">the values that were posted in a forms-dictionary</param>
        /// <returns>a more accurate search-dictioanry</returns>
        private static Dictionary<string,object> TranslateForm(Dictionary<string,StringValues> values, bool expectFilterForm)
        {
            var ret = new Dictionary<string, object>();
            foreach (var v in values)
            {
                if (expectFilterForm)
                {
                    switch (v.Key)
                    {
                        case "sort":
                        case "page":
                        case "group":
                        {
                            LogEnvironment.LogDebugEvent($"Ignoring {v.Key}", LogSeverity.Report);
                            break;
                        }
                        case "filter":
                        {
                            var tmpFilter = v.Value.FirstOrDefault();
                            var st = "Label~contains~'";
                            if (tmpFilter?.StartsWith(st, StringComparison.OrdinalIgnoreCase) ?? false)
                            {
                                var ln = tmpFilter.Length - 1 - st.Length;
                                if (ln > 0)
                                {
                                    tmpFilter = tmpFilter.Substring(st.Length, ln);
                                    ret.Add("Filter", tmpFilter);
                                }
                            }
                            else
                            {
                                LogEnvironment.LogEvent($"Unexpected Search-Filter: {tmpFilter}", LogSeverity.Warning);
                            }

                            break;
                        }
                        default:
                        {
                            ret.Add(v.Key, v.Value.FirstOrDefault());
                            break;
                        }
                    }
                }
                else
                {
                    ret.Add(v.Key, v.Value.FirstOrDefault());
                }
            }

            return ret;
        }
    }
}
