using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Formatting;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework;
using ITVComponents.WebCoreToolkit.EntityFramework.DiagnosticsQueries;
using ITVComponents.WebCoreToolkit.EntityFramework.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Options.ForeignKeys;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Net.FileHandling;
using ITVComponents.WebCoreToolkit.Net.Options;
using ITVComponents.WebCoreToolkit.Net.ViewModel;
using ITVComponents.WebCoreToolkit.Routing;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Tokens;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;

namespace ITVComponents.WebCoreToolkit.Net.Extensions
{
    public static class RouteExtensions
    {
        /// <summary>
        /// Exposes an Endpoint that enables a caller to request diagnostics information 
        /// </summary>
        /// <param name="builder">the route builder used to expose the consumer-endpoint</param>
        /// <param name="explicitTenantParam">indicates whether to support explicit tenant-paths</param>
        /// <param name="forAreas">indicates whether to support areas</param>
        /// <param name="withAuthorization">indicates whether this endpoint needs to be secured</param>
        /// <returns>the resulting ConventionBuilder for further configuration</returns>
        public static IEndpointConventionBuilder UseDiagnostics(this IEndpointRouteBuilder builder, string explicitTenantParam, bool forAreas, bool withAuthorization = true)
        {
            bool forExplicitTenants = !string.IsNullOrEmpty(explicitTenantParam);
            ContextExtensions.Init();
            RequestDelegate dg = async context =>
            {
                var query = (string) context.Request.RouteValues["diagnosticsQueryName"];
                var handler = context.Request.RouteValues.ContainsKey("fileHandler")
                    ? (string)context.Request.RouteValues["fileHandler"]
                    : null;
                RouteData routeData = context.GetRouteData();
                string area = null;
                if (context.Request.RouteValues.ContainsKey("area"))
                {
                    area = (string) context.Request.RouteValues["area"];
                }

                ActionDescriptor actionDescriptor = new ActionDescriptor();
                ActionContext actionContext = new ActionContext(context, routeData, actionDescriptor);
                var contextObj = new
                {
                    HttpContext = context,
                    User = context.User.Identity,
                    CurrentClaims = new Dictionary<string, IList<string>>(from c in context.User.Claims
                        group c by c.Type
                        into g
                        select new KeyValuePair<string, IList<string>>(g.Key, g.Select(n => n.Value).ToList())),
                    ActionContext = actionContext
                };

                var queryArg = new Dictionary<string, string>(context.Request.Query.Select(n =>
                    new KeyValuePair<string, string>(n.Key, TranslateValue(n.Value.ToString(), contextObj))));
                if (string.IsNullOrEmpty(handler))
                {
                    var dbContext = context.RequestServices.ContextForDiagnosticsQuery(query, area, out var diagQuery);
                    if (dbContext != null)
                    {

                        JsonResult result = new JsonResult(dbContext.RunDiagnosticsQuery(diagQuery, queryArg));
                        await result.ExecuteResultAsync(actionContext);
                        return;
                    }
                }
                else
                {
                    queryArg["$$QUERYNAME"] = query;
                    queryArg["$$QUERYAREA"] = area;
                    var token = new DownloadToken
                    {
                        ContentType = "application/octet-stream",
                        DownloadName = "Error-Handler-Should-Set-File-Name.bin",
                        DownloadReason = query,
                        FileDownload = true,
                        FileIdentifier = queryArg.CompressToken(encrypt: false),
                        HandlerModuleName = handler
                    }.CompressToken();
                    var urlFormat = context.RequestServices.GetService<IUrlFormat>();
                    var url = urlFormat.FormatUrl($"[SlashPermissionScope]/File/{token}");
                    RedirectResult redir = new RedirectResult(url, false);
                    await redir.ExecuteResultAsync(actionContext);
                    return;
                }

                StatusCodeResult notFound = new NotFoundResult();
                await notFound.ExecuteResultAsync(actionContext);
            };
            
            var tmp = builder.MapGet($"{(forExplicitTenants?$"/{{{explicitTenantParam}:permissionScope}}":"")}{(forAreas?"/{area:exists}":"")}/Diagnostics/{{diagnosticsQueryName:alpha}}/{{fileHandler:alpha?}}", dg);

            if (withAuthorization)
            {
                tmp.RequireAuthorization();
            }

            return tmp;
        }

        public static void UseWidgets(this IEndpointRouteBuilder builder, string explicitTenantParam, out IEndpointConventionBuilder getAction, out IEndpointConventionBuilder postAction, bool withAuthorization = true)
        {
            bool forExplicitTenants = !string.IsNullOrEmpty(explicitTenantParam);
            ContextExtensions.Init();
            RequestDelegate dg = async context =>
            {
                var widget = (string)context.Request.RouteValues["widgetName"];
                int userWidgetId = -1;
                bool hasId = false;
                if (context.Request.RouteValues.ContainsKey("userWidgetId") &&
                    context.Request.RouteValues["userWidgetId"] is int uwiRoute)
                {
                    userWidgetId = uwiRoute;
                    hasId = true;
                }
                else if (context.Request.RouteValues.ContainsKey("userWidgetId") &&
                         int.TryParse(context.Request.RouteValues["userWidgetId"]?.ToString(),
                             out var uwiConverted))
                {
                    userWidgetId = uwiConverted;
                    hasId = true;
                }

                RouteData routeData = context.GetRouteData();
                ActionDescriptor actionDescriptor = new ActionDescriptor();
                ActionContext actionContext = new ActionContext(context, routeData, actionDescriptor);
                var dbContext = context.RequestServices.GetService<IDiagnosticsStore>();
                if (dbContext != null)
                {
                    var model = !hasId?dbContext.GetDashboard(widget):dbContext.GetDashboard(widget,userWidgetId);
                    if (context.RequestServices.VerifyUserPermissions(new[]
                        { model.DiagnosticsQuery.Permission }))
                    {
                        JsonResult result = new JsonResult(model.ToViewModel<DashboardWidgetDefinition, DBWidget>(
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
                            }));
                        await result.ExecuteResultAsync(actionContext);
                        return;
                    }
                }

                StatusCodeResult notFound = new NotFoundResult();
                await notFound.ExecuteResultAsync(actionContext);
            };

            RequestDelegate dgPost = async context =>
            {
                //{{connection:regex(^[\\w_]+$)}}/{{table:regex(^[\\w_]+$)}}
                RouteData routeData = context.GetRouteData();
                ActionDescriptor actionDescriptor = new ActionDescriptor();
                ActionContext actionContext = new ActionContext(context, routeData, actionDescriptor);

                var connection = context.RequestServices.GetService<IDiagnosticsStore>();
#if NETCOREAPP3_1
                DBWidget[] widgets;
                using (var reader = context.Request.Body)
                {
                    using (var textreader = new StreamReader(reader))
                    {
                        var jtx = new JsonTextReader(textreader);
                        var ser = new JsonSerializer();
                        widgets = ser.Deserialize<DBWidget[]>(jtx);
                    }
                }

#else
                var widgets = await context.Request.ReadFromJsonAsync<DBWidget[]>();
#endif
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
                    var result = new JsonResult(ret);
                    await result.ExecuteResultAsync(actionContext);
                    return;
                }

                StatusCodeResult notFound = new NotFoundResult();
                await notFound.ExecuteResultAsync(actionContext);
            };

            getAction = builder.MapGet($"{(forExplicitTenants ? $"/{{{explicitTenantParam}:permissionScope}}" : "")}/DBW/{{widgetName:alpha}}/{{userWidgetId:int?}}", dg);
            postAction = builder.MapPost($"{(forExplicitTenants ? $"/{{{explicitTenantParam}:permissionScope}}" : "")}/DBW", dgPost);
            if (withAuthorization)
            {
                getAction.RequireAuthorization();
                postAction.RequireAuthorization();
            }
        }

        /// <summary>
        /// Configures the ForeignKey-Endpoint for the current Web-Application
        /// </summary>
        /// <param name="builder">the route-builder that confgures the application-routing</param>
        /// <param name="withAuthorization">indicates whether to secure the foreign-key endpoint</param>
        /// <returns>the EndPointConventionBuilder for further configuration</returns>
        public static IEndpointConventionBuilder UseAutoForeignKeys(this IEndpointRouteBuilder builder, string explicitTenantParam, bool forAreas, bool withAuthorization = true)
        {
            bool forExplicitTenants = !string.IsNullOrEmpty(explicitTenantParam);
            ContextExtensions.Init();
            RequestDelegate dlg = async context =>
            {
                RouteData routeData = context.GetRouteData();
                ActionDescriptor actionDescriptor = new ActionDescriptor();
                ActionContext actionContext = new ActionContext(context, routeData, actionDescriptor);
                bool ok = !withAuthorization || context.RequestServices.VerifyCurrentUser();
                if (ok)
                {
                    if (context.Request.RouteValues.ContainsKey("dataResolveHint"))
                    {
                        var baseHint = ((string)context.Request.RouteValues["dataResolveHint"])?.Split("/")
                            .Select(n => HttpUtility.UrlDecode(n)).ToArray();
                        if (baseHint is { Length: >= 2 })
                        {
                            var connection =
                                RegexValidate(baseHint[0], "^[\\w_]+$")
                                    ? baseHint[0]
                                    : null; //(string) context.Request.RouteValues["connection"];
                            if (connection != null)
                            {
                                string area = null;
                                if (context.Request.RouteValues.ContainsKey("area"))
                                {
                                    area = (string)context.Request.RouteValues["area"];
                                }

                                var dbContext = context.RequestServices.ContextForFkQuery(connection, area);
                                if (dbContext != null)
                                {
                                    var table = RegexValidate(baseHint[1],
                                        dbContext.CustomFkSettings?.CustomTableValidation ?? "^[\\w_]+$")
                                        ? baseHint[1]
                                        : null; //(string) context.Request.RouteValues["table"];
                                    string id = null;
                                    bool valid = !string.IsNullOrEmpty(connection) && !string.IsNullOrEmpty(table);
                                    if (baseHint.Length > 2)
                                    {
                                        id = RegexValidate(baseHint[2],
                                            dbContext.CustomFkSettings?.CustomIdValidation ?? "^[-@\\w_\\+\\:]+$")
                                            ? baseHint[2]
                                            : null;
                                        valid &= !string.IsNullOrEmpty(id);
                                    }

                                    if (valid)
                                    {
                                        bool authorized = true;
                                        JsonResult result = null;
                                        try
                                        {
                                            if (string.IsNullOrEmpty(id))
                                            {
                                                result = new JsonResult(dbContext.ReadForeignKey(table));
                                            }
                                            else
                                            {
                                                result = new JsonResult(dbContext.ReadForeignKey(table, id: id)
                                                    .Cast<object>()
                                                    .FirstOrDefault());
                                            }
                                        }
                                        catch (SecurityException)
                                        {
                                            authorized = false;
                                        }

                                        if (authorized)
                                        {
                                            await result.ExecuteResultAsync(actionContext);
                                            return;
                                        }

                                        UnauthorizedResult ill = new UnauthorizedResult();
                                        await ill.ExecuteResultAsync(actionContext);
                                        return;
                                    }
                                }
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
            var tmp = builder.MapGet($"{(forExplicitTenants ? $"/{{{explicitTenantParam}:permissionScope}}" : "")}{(forAreas ? "/{area:exists}" : "")}/ForeignKey/{{**dataResolveHint}}", dlg);

            if (withAuthorization)
            {
                tmp.RequireAuthorization();
            }

            return tmp;
        }

        /// <summary>
        /// Exposes the Global and Tenant-Settings to the browser, so that Settings can also be consumed from JavaScript
        /// </summary>
        /// <param name="builder">the route-builder that confgures the application-routing</param>
        /// <param name="withAuthorization">indicates whether to use authentication for this action</param>
        /// <returns>the EndPointConventionBuilder for further configuration</returns>
        public static IEndpointConventionBuilder ExposeClientSettings(this IEndpointRouteBuilder builder, string explicitTenantParam, bool withAuthorization = true)
        {
            bool forExplicitTenants = !string.IsNullOrEmpty(explicitTenantParam);
            var tmp = builder.MapGet($"{(forExplicitTenants?$"/{{{explicitTenantParam}:permissionScope}}":"")}/ClientOptions/{{kind:alpha}}/{{type:alpha}}/{{key:alpha}}", async context =>
            {
                var settingsKind = (string) context.Request.RouteValues["kind"];
                RouteData routeData = context.GetRouteData();

                ActionDescriptor actionDescriptor = new ActionDescriptor();
                ActionContext actionContext = new ActionContext(context, routeData, actionDescriptor);
                ISettingsProvider settingsProvider;
                if (settingsKind.Equals("global", StringComparison.OrdinalIgnoreCase))
                {
                    settingsProvider = context.RequestServices.GetService<IGlobalSettingsProvider>();
                }
                else if (settingsKind.Equals("tenant", StringComparison.OrdinalIgnoreCase))
                {
                    settingsProvider = context.RequestServices.GetService<IScopedSettingsProvider>();
                }
                else
                {
                    settingsProvider = null;
                }

                if (settingsProvider != null)
                {
                    var type = (string) context.Request.RouteValues["type"];
                    var key = (string) context.Request.RouteValues["key"];
                    var plainSetting = "";
                    var contentType = "text/plain";
                    if (type.Equals("json", StringComparison.OrdinalIgnoreCase))
                    {
                        plainSetting = settingsProvider.GetJsonSetting(key);
                        contentType = "application/json";
                    }
                    else if (type.Equals("literal", StringComparison.OrdinalIgnoreCase))
                    {
                        plainSetting = settingsProvider.GetLiteralSetting(key);
                    }

                    if (context.Request.Query.ContainsKey("contentType"))
                    {
                        contentType = context.Request.Query["contentType"];
                    }

                    if (!string.IsNullOrEmpty(plainSetting))
                    {
                        var content = Encoding.Default.GetBytes(plainSetting);
                        FileResult fsr = new FileContentResult(content, contentType);
                        await fsr.ExecuteResultAsync(actionContext);
                        return;
                    }
                }

                StatusCodeResult notFound = new NotFoundResult();
                await notFound.ExecuteResultAsync(actionContext);
            });

            if (withAuthorization)
            {
                tmp.RequireAuthorization();
            }

            return tmp;
        }

        /// <summary>
        /// Exposes an endpoint enabling a client to change the current tenant
        /// </summary>
        /// <param name="builder">the route-builder used to expose the endpoint</param>
        /// <returns>the created conventionbuilder for further configuration</returns>
        public static IEndpointConventionBuilder UseTenantSwitch(this IEndpointRouteBuilder builder)
        {
            var tmp = builder.MapPost($"/SwitchTenant", async context =>
            {
                RouteData routeData = context.GetRouteData();
                ActionDescriptor actionDescriptor = new ActionDescriptor();
                ActionContext actionContext = new ActionContext(context, routeData, actionDescriptor);
                FormReader former = new FormReader(context.Request.Body);
                var formsDictionary = TranslateForm(await former.ReadFormAsync(), false);
                if (formsDictionary.ContainsKey("NewTenant"))
                {
                    var newTenant = (string) formsDictionary["NewTenant"];
                    var scopeProvider = actionContext.HttpContext.RequestServices.GetRequiredService<IPermissionScope>();
                    var securityRepo = actionContext.HttpContext.RequestServices.GetRequiredService<ISecurityRepository>();
                    var userProvider = actionContext.HttpContext.RequestServices.GetRequiredService<IUserNameMapper>();
                    var eligibleTenants = securityRepo.GetEligibleScopes(userProvider.GetUserLabels(context.User), context.User.Identity.AuthenticationType);
                    if (eligibleTenants.Any(n => n.ScopeName == newTenant))
                    {
                        OkResult ok = new OkResult();
                        scopeProvider.ChangeScope(newTenant);
                        await ok.ExecuteResultAsync(actionContext);
                        return;
                    }
    
                    UnauthorizedResult no = new UnauthorizedResult();
                    await no.ExecuteResultAsync(actionContext);
                    return;
                }

                StatusCodeResult notFound = new NotFoundResult();
                await notFound.ExecuteResultAsync(actionContext);
            }).RequireAuthorization();
            return tmp;
        }

        /// <summary>
        /// Exposes permissions of the current user to the client application
        /// </summary>
        /// <param name="builder">the endpoint-convention builder that is used to create a presented endpoint</param>
        /// <returns>the endpoint-convention builder for further configuration</returns>
        public static IEndpointConventionBuilder ExposeUserPermissions(this IEndpointRouteBuilder builder, string explicitTenantParam)
        {
            var forExplicitTenants = !string.IsNullOrEmpty(explicitTenantParam);
            var tmp = builder.MapGet($"{(forExplicitTenants?$"/{{{explicitTenantParam}:permissionScope}}":"")}/UserPermissions", async context =>
            {
                RouteData routeData = context.GetRouteData();
                ActionDescriptor actionDescriptor = new ActionDescriptor();
                ActionContext actionContext = new ActionContext(context, routeData, actionDescriptor);
                var retVal = Array.Empty<Models.Permission>();
                ISecurityRepository repo;
                string[] userLabels;
                string authType;
                if (context.RequestServices.IsLegitSharedAssetPath(out repo, out userLabels, out authType)||context.RequestServices.IsUserAuthenticated(out repo, out userLabels, out authType))
                {
                    //var repo = context.RequestServices.GetService<ISecurityRepository>();
                    //var mapper = context.RequestServices.GetService<IUserNameMapper>();
                    //var userLabels = mapper.GetUserLabels(context.User);
                    //var authType = context.User.Identity.AuthenticationType;
                    retVal = repo.GetPermissions(userLabels, authType).ToArray();
                }

                JsonResult result = new JsonResult(retVal);
                await result.ExecuteResultAsync(actionContext);
            }).RequireAuthorization();
            return tmp;
        }

        /// <summary>
        /// Exposes File-Services for Up-and Downloading files through a standardized interface
        /// </summary>
        /// <param name="builder">the endpoint-builder used for adding the route</param>
        /// <param name="explicitTenantParam">indicates whether to accept an explicit tenant parameter</param>
        /// <param name="withAuthorization">indicates whether tho secure the endpoint</param>
        /// <param name="configureUpload">indicates whether to use upload-features</param>
        /// <param name="configureDownload">indicates whether to use download features</param>
        public static void UseFileServices(this IEndpointRouteBuilder builder, string explicitTenantParam, bool withAuthorization = true, Action<IEndpointConventionBuilder> configureUpload = null, Action<IEndpointConventionBuilder> configureDownload = null)
        {
            var forExplicitTenants = !string.IsNullOrEmpty(explicitTenantParam);
            var upload = builder.MapPost($"{(forExplicitTenants?$"/{{{explicitTenantParam}:permissionScope}}":"")}/File/{{UploadModule:alpha}}/{{UploadReason:alpha}}/{{**UploadHint}}", async context =>
            {
                RouteData routeData = context.GetRouteData();

                ActionDescriptor actionDescriptor = new ActionDescriptor();
                ActionContext actionContext = new ActionContext(context, routeData, actionDescriptor);

                if (!(context.Request.ContentType?.Contains("multipart/", StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    ContentResult br = new ContentResult {Content = "Multipart Upload expected!", ContentType = "text/plain", StatusCode = (int) HttpStatusCode.BadRequest};
                    await br.ExecuteResultAsync(actionContext);
                    return;
                }

                var handlerName = (string) context.Request.RouteValues["UploadModule"];
                var reason = (string) context.Request.RouteValues["UploadReason"];
                var uploadHint = (string) context.Request.RouteValues["UploadHint"];
                var fileHandler = context.RequestServices.GetFileHandler(handlerName);
                var syncHandler = fileHandler as IFileHandler;
                var asyncHandler = fileHandler as IAsyncFileHandler;
                var handlerOptionsName = $"{handlerName}UploadSettings";
                var handlerReasonOptionsName = $"{reason}_{handlerOptionsName}";
                var logger = context.RequestServices.GetService<ILogger<IFileHandler>>();
                var scopeOptions = context.RequestServices.GetService<IScopedSettingsProvider>();
                var globalOptions = context.RequestServices.GetService<IGlobalSettingsProvider>();
                var handlerSettingsRaw = scopeOptions?.GetJsonSetting(handlerOptionsName) ?? globalOptions?.GetJsonSetting(handlerOptionsName);
                var reasonSettingsRaw = scopeOptions?.GetJsonSetting(handlerReasonOptionsName) ?? globalOptions?.GetJsonSetting(handlerReasonOptionsName);
                var finalSettingsRaw = reasonSettingsRaw ?? handlerSettingsRaw;
                var options = new UploadOptions();
                if (!string.IsNullOrEmpty(finalSettingsRaw))
                {
                    options = JsonHelper.FromJsonString<UploadOptions>(finalSettingsRaw);
                }

                var maxSize = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
                maxSize.MaxRequestBodySize = options.MaxUploadSize;
                var requiredPermissions = asyncHandler?.PermissionsForReason(reason) ??
                                          syncHandler.PermissionsForReason(reason);
                if (!withAuthorization || (requiredPermissions != null && requiredPermissions.Length != 0 && context.RequestServices.VerifyUserPermissions(requiredPermissions)))
                {
                    var boundary = context.Request.GetMultipartBoundary();
                    var partReader = new MultipartReader(boundary, context.Request.Body) {BodyLengthLimit = null};
                    MultipartSection section;
                    var ms = new ModelStateDictionary();
                    try
                    {
                        while ((section = await partReader.ReadNextSectionAsync()) != null)
                        {
                            var hasContentDispositionHeader =
                                ContentDispositionHeaderValue.TryParse(
                                    section.ContentDisposition, out var contentDisposition);
                            if (hasContentDispositionHeader)
                            {
                                if (contentDisposition.IsFileDisposition())
                                {
                                    logger.LogDebug(new EventId(0, "Found File-Disposition"), $"Found a File: {contentDisposition.Name.Value} ({contentDisposition.FileName.Value},{contentDisposition.FileNameStar.Value})");
                                    var content = await section.Body.ToArrayAsync();
                                    string name = contentDisposition.FileName.Value ?? contentDisposition.FileNameStar.Value ?? contentDisposition.Name.Value;
                                    if (VerifyFile(name, content, options, out var deniedReason))
                                    {
                                        if (string.IsNullOrEmpty(uploadHint))
                                        {
                                            if (asyncHandler != null)
                                            {
                                                await asyncHandler.AddFile(name, content, ms, context.User?.Identity,
                                                    (n, c) => VerifyFile(n, c, options, out _));
                                            }
                                            else
                                            {
                                                syncHandler.AddFile(name, content, ms, context.User?.Identity,
                                                    (n, c) => VerifyFile(n, c, options, out _));
                                            }
                                        }
                                        else
                                        {
                                            if (asyncHandler != null)
                                            {
                                                await asyncHandler.AddFile(name, content, uploadHint, ms,
                                                    context.User?.Identity);
                                            }
                                            else
                                            {
                                                syncHandler.AddFile(name, content, uploadHint, ms,
                                                    context.User?.Identity);
                                            }
                                        }
                                        if (!ms.IsValid)
                                        {
                                            ContentResult br = new ContentResult {Content = string.Join(Environment.NewLine,(from t in ms where t.Key== "File" select t.Value.Errors).SelectMany(m => m).Select(i => i.ErrorMessage)), ContentType = "text/plain", StatusCode = (int) HttpStatusCode.BadRequest};
                                            await br.ExecuteResultAsync(actionContext);
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        ContentResult br = new ContentResult {Content = deniedReason, ContentType = "text/plain", StatusCode = (int) HttpStatusCode.BadRequest};
                                        await br.ExecuteResultAsync(actionContext);
                                        return;
                                    }
                                }
                                else if (contentDisposition.IsFormDisposition())
                                {
                                    string formContent = null;
                                    if (asyncHandler != null && asyncHandler is IAsyncFormProcessor asyncFormer)
                                    {
                                        formContent = await asyncFormer.ProcessForm(section.Headers, section.Body);
                                    }
                                    else if (syncHandler != null && syncHandler is IFormProcessor syncFormer)
                                    {
                                        formContent = syncFormer.ProcessForm(section.Headers, section.Body);
                                    }

                                    formContent ??= Encoding.Default.GetString(await section.Body.ToArrayAsync());
                                    logger.LogDebug(new EventId(0, "Found Form-Disposition"), $"Form Content: {formContent})");
                                }
                            }
                        }

                        var rfh = syncHandler as IRespondingFileHandler;
                        var arh = asyncHandler as IAsyncRespondingFileHandler;
                        if (rfh == null && arh == null)
                        {
                            await new OkResult().ExecuteResultAsync(actionContext);
                        }
                        else if (arh != null)
                        {
                            var r = await arh.GetUploadResult();
                            await r.ExecuteResultAsync(actionContext);
                        }
                        else
                        {
                            await rfh.GetUploadResult().ExecuteResultAsync(actionContext);
                        }

                        return;
                    }
                    catch (Exception ex)
                    {
                        ContentResult br = new ContentResult {Content = ex.Message, ContentType = "text/plain", StatusCode = (int) HttpStatusCode.BadRequest};
                        await br.ExecuteResultAsync(actionContext);
                        return;
                    }
                }

                await new UnauthorizedResult().ExecuteResultAsync(actionContext);
            });

            if (withAuthorization)
            {
                upload.RequireAuthorization();
            }

            configureUpload?.Invoke(upload);

            var download = builder.MapGet($"{(forExplicitTenants?$"/{{{explicitTenantParam}:permissionScope}}":"")}/File/{{**FileToken}}", async context =>
            {
                RouteData routeData = context.GetRouteData();
                ActionDescriptor actionDescriptor = new ActionDescriptor();
                ActionContext actionContext = new ActionContext(context, routeData, actionDescriptor);
                var token = (string) context.Request.RouteValues["FileToken"];
                var forceRefreshIndicator = token.IndexOf("?");
                if (forceRefreshIndicator != -1)
                {
                    token = token.Substring(0, forceRefreshIndicator);
                }

                var fileToken = token.DecompressToken<DownloadToken>();
                var fileHandler = context.RequestServices.GetFileHandler(fileToken.HandlerModuleName);
                var syncHandler = fileHandler as IFileHandler;
                var asyncHandler = fileHandler as IAsyncFileHandler;
                var requiredPermissions = asyncHandler?.PermissionsForReason(fileToken.DownloadReason) ??
                                          syncHandler.PermissionsForReason(fileToken.DownloadReason);
                if (!withAuthorization || (requiredPermissions != null && requiredPermissions.Length != 0 && context.RequestServices.VerifyUserPermissions(requiredPermissions)))
                {
                    string downloadName = fileToken.DownloadName;
                    string contentType = fileToken.ContentType;
                    bool forceDownload = fileToken.FileDownload;
                    byte[] fileContent = null;
                    Stream fileStreamContent = null;
                    bool asyncOk = false;
                    if (asyncHandler != null)
                    {
                        var tmp = await asyncHandler.ReadFile(fileToken.FileIdentifier, context.User?.Identity);
                        // ReSharper disable once AssignmentInConditionalExpression
                        if (asyncOk = tmp.Success)
                        {
                            downloadName = tmp.DownloadName ?? downloadName;
                            contentType = tmp.ContentType ?? contentType;
                            forceDownload = tmp.FileDownload ?? forceDownload;
                            fileStreamContent = tmp.FileContent;
                            tmp.DeferredDisposals.ForEach(context.Response.RegisterForDispose);
                        }
                    }
                    if ((asyncOk && fileStreamContent != null) ||
                        (syncHandler != null && syncHandler.ReadFile(fileToken.FileIdentifier, context.User?.Identity, ref downloadName, ref contentType, ref forceDownload, out fileContent)))
                    {
                        FileResult fileResult;
                        if (fileStreamContent != null)
                        {
                            fileResult = new FileStreamResult(fileStreamContent, contentType) { EnableRangeProcessing = true };
                        }
                        else
                        {
                            fileResult = new FileContentResult(fileContent, contentType);
                        }

                        fileResult.FileDownloadName = "";
                        if (forceDownload)
                        {
                            fileResult.FileDownloadName = downloadName;
                        }
                        else
                        {
                            ContentDispositionHeaderValue hv = new ContentDispositionHeaderValue("inline");
                            hv.SetHttpFileName(downloadName);
                            context.Response.Headers["content-disposition"] = hv.ToString();
                        }

                        await fileResult.ExecuteResultAsync(actionContext);
                        return;
                    }

                    await new NotFoundResult().ExecuteResultAsync(actionContext);
                    return;
                }

                await new UnauthorizedResult().ExecuteResultAsync(actionContext);
            });

            if (withAuthorization)
            {
                download.RequireAuthorization();
            }

            configureDownload?.Invoke(download);
        }

        /// <summary>
        /// Enables the FileSystem Exposal when the application runs in an environmen thatn can not be accessed directly, this may be helpful for Diagnostic purposes
        /// </summary>
        /// <param name="builder">the route-builder that confgures the application-routing</param>
        /// <param name="withAuthorization">indicates whether to use authentication for this action</param>
        /// <returns>the resulting EndpointConvention builder for further configuration of the generated action</returns>
        public static IEndpointConventionBuilder ExposeFileSystem(this IEndpointRouteBuilder builder, bool withAuthorization = true)
        {
            var tmp = builder.MapGet("/FileSys/{Action:alpha}", async context =>
            {
                var action = (string)context.Request.RouteValues["Action"];
                RouteData routeData = context.GetRouteData();

                ActionDescriptor actionDescriptor = new ActionDescriptor();
                ActionContext actionContext = new ActionContext(context, routeData, actionDescriptor);
                if (!withAuthorization || context.RequestServices.VerifyUserPermissions(new[] { "BrowseFs" }))
                {
                    StringBuilder path = new StringBuilder("/");
                    string pattern = null;
                    if (context.Request.Query.ContainsKey("Path"))
                    {
                        path.Append(context.Request.Query["Path"]);
                    }

                    if (context.Request.Query.ContainsKey("Pattern"))
                    {
                        pattern = context.Request.Query["Pattern"];
                    }

                    var pth = path.ToString();
                    switch (action.ToLower())
                    {
                        case "download":
                            if (File.Exists(pth))
                            {
                                FileResult fsr = new FileContentResult(File.ReadAllBytes(pth), "application/octet-stream");
                                await fsr.ExecuteResultAsync(actionContext);
                                return;
                            }
                            break;
                        case "list":
                            if (Directory.Exists(pth))
                            {
                                JsonResult result = new JsonResult((from t in Directory.GetFiles(pth) select new { Name = Path.GetFileName(t), Type = "File", Path = Path.GetDirectoryName(t) }).Union((from t in Directory.GetDirectories(pth) select new { Name = Path.GetFileName(t), Type = "Directory", Path = Path.GetDirectoryName(t) }).OrderBy(n => n.Name)).ToArray());
                                await result.ExecuteResultAsync(actionContext);
                                return;
                            }

                            break;
                        case "search":
                            if (Directory.Exists(pth) && !string.IsNullOrEmpty(pattern))
                            {
                                var directories = new List<string>();
                                var dir = new string[] { pth };
                                var files = new List<string>();
                                while (dir.Length != 0)
                                {
                                    foreach (var item in dir)
                                    {
                                        try
                                        {
                                            files.AddRange(Directory.GetFiles(item, pattern));
                                            directories.AddRange(Directory.GetDirectories(item));
                                        }
                                        catch (Exception ex)
                                        {
                                        }
                                    }
                                    dir = directories.ToArray();
                                    directories.Clear();
                                }

                                JsonResult result = new JsonResult((from t in files select new { Name = Path.GetFileName(t), Type = "File", Path = Path.GetDirectoryName(t) }).ToArray());
                                await result.ExecuteResultAsync(actionContext);
                                return;
                            }

                            break;
                    }
                }

                StatusCodeResult notFound = new NotFoundResult();
                await notFound.ExecuteResultAsync(actionContext);
            });

            if (withAuthorization)
            {
                tmp.RequireAuthorization();
            }

            return tmp;
        }

        /// <summary>
        /// Verifies the name and content of an uploaded file
        /// </summary>
        /// <param name="name">the name of the file</param>
        /// <param name="content">the content of the file</param>
        /// <param name="options">the configured options for the upload</param>
        /// <param name="deniedReason">the reason why the file should not be accepted</param>
        /// <returns>a value indicating whether the file can be accepted by the uploader</returns>
        private static bool VerifyFile(string name, byte[] content, UploadOptions options, out string deniedReason)
        {
            if (options.UncheckedFileExtensions != null && options.UncheckedFileExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase))
            {
                deniedReason = "";
                return true;
            }
            
            if (!(options.FileExtensions == null || options.FileExtensions.Count == 0 || options.FileExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase)))
            {
                deniedReason = "File-Extension not allowed";
                return false;
            }

            if (!(options.MagicNumbers == null || options.MagicNumbers.Count == 0 || options.MagicNumbers.Any(n => n.SequenceEqual(content.Take(n.Length)))))
            {
                deniedReason = "File-Format not supported";
                return false;
            }

            deniedReason = "";
            return true;
        }

        /// <summary>
        /// Translates the given input field. If a value was prefixed with a dollar-sign, it will be formatted using ITVComponents.Formatting feature
        /// </summary>
        /// <param name="input">the query-value that needs to be translated</param>
        /// <param name="actionContext">the context of the current action</param>
        /// <returns></returns>
        private static string TranslateValue(string input, object actionContext)
        {
            if (!string.IsNullOrEmpty(input) && input.StartsWith("$"))
            {
                return actionContext.FormatText(input.Substring(1));
            }

            return input;
        }

        /// <summary>
        /// Translates a specific filter to a Dictionary that is processable by the Context-Extensions for ForeignKey processing
        /// </summary>
        /// <param name="values">the values that were posted in a forms-dictionary</param>
        /// <returns>a more accurate search-dictioanry</returns>
        private static Dictionary<string, object> TranslateForm(Dictionary<string, StringValues> values, bool expectFilterForm)
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

        private static bool RegexValidate(string value, string regexPattern)
        {
            return Regex.IsMatch(value, regexPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }
    }
}
