using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.Extensions;
using ITVComponents.WebCoreToolkit.Extensions;
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
                //{{connection:regex(^[\\w_]+$)}}/{{table:regex(^[\\w_]+$)}}
                RouteData routeData = context.GetRouteData();
                ActionDescriptor actionDescriptor = new ActionDescriptor();
                ActionContext actionContext = new ActionContext(context, routeData, actionDescriptor);
                var ok = !withAuthorization || context.RequestServices.VerifyCurrentUser();
                if (ok)
                {
                    if (context.Request.RouteValues.ContainsKey("dataResolveHint"))
                    {
                        var baseHint = ((string)context.Request.RouteValues["dataResolveHint"])?.Split("/")
                            .Select(n => HttpUtility.UrlDecode(n)).ToArray();
                        if (baseHint is { Length: 2 })
                        {
                            string area = null;
                            if (context.Request.RouteValues.ContainsKey("area"))
                            {
                                area = (string)context.Request.RouteValues["area"];
                            }

                            var connection =
                                RegexValidate(baseHint[0], "^[\\w_]+$")
                                    ? baseHint[0]
                                    : null; //(string) context.Request.RouteValues["connection"];
                            var dbContext = context.RequestServices.ContextForFkQuery(connection, area);
                            if (dbContext != null)
                            {
                                var table = RegexValidate(baseHint[1], dbContext.CustomFkSettings?.CustomTableValidation??"^[\\w_]+$")
                                    ? baseHint[1]
                                    : null; //(string) context.Request.RouteValues["table"];

                                FormReader former = new FormReader(context.Request.Body);
                                var formsDictionary = await former.ReadFormAsync();
                                //LogEnvironment.LogEvent(Stringify(formsDictionary), LogSeverity.Report);
                                var newDic = TranslateForm(formsDictionary, true);
                                JsonResult result = new JsonResult(dbContext.ReadForeignKey(table, postedFilter: newDic)
                                    .ToDummyDataSourceResult());
                                await result.ExecuteResultAsync(actionContext);
                                return;
                            }
                        }
                    }
                }
                else
                {
                    UnauthorizedResult ill = new UnauthorizedResult();
                    await ill.ExecuteResultAsync(actionContext);
                    return;
                }

                StatusCodeResult notFound = new NotFoundResult();
                await notFound.ExecuteResultAsync(actionContext);
            };
            var tmp = builder.MapPost($"{(forExplicitTenants ? $"/{{{explicitTenantParam}:permissionScope}}" : "")}{(forAreas ? "/{area:exists}" : "")}/ForeignKey/{{**dataResolveHint}}", dlg);

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
                            var st = "~contains~'";
                            if (tmpFilter?.Contains(st, StringComparison.OrdinalIgnoreCase) ?? false)
                            {
                                var id = tmpFilter.IndexOf(st, StringComparison.OrdinalIgnoreCase);
                                id += st.Length;
                                var ln = tmpFilter.Length - 1 - id;
                                if (ln > 0)
                                {
                                    tmpFilter = tmpFilter.Substring(id, ln);
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

        private static bool RegexValidate(string value, string regexPattern)
        {
            return Regex.IsMatch(value, regexPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }
    }
}
