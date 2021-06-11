using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework;
using ITVComponents.WebCoreToolkit.EntityFramework.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Options.ForeignKeys;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Net.FileHandling;
using ITVComponents.WebCoreToolkit.Net.Options;
using ITVComponents.WebCoreToolkit.Net.ViewModel;
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
                RouteData routeData = context.GetRouteData();
                string area = null;
                if (context.Request.RouteValues.ContainsKey("area"))
                {
                    area = (string) context.Request.RouteValues["area"];
                }

                ActionDescriptor actionDescriptor = new ActionDescriptor();
                ActionContext actionContext = new ActionContext(context, routeData, actionDescriptor);
                var dbContext = context.RequestServices.ContextForDiagnosticsQuery(query, area, out var diagQuery);
                if (dbContext != null)
                {
                    JsonResult result = new JsonResult(dbContext.RunDiagnosticsQuery(diagQuery, new Dictionary<string, string>(context.Request.Query.Select(n => new KeyValuePair<string, string>(n.Key, n.Value.ToString())))));
                    await result.ExecuteResultAsync(actionContext);
                    return;
                }

                StatusCodeResult notFound = new NotFoundResult();
                await notFound.ExecuteResultAsync(actionContext);
            };
            
            var tmp = builder.MapGet($"{(forExplicitTenants?$"/{{{explicitTenantParam}:permissionScope}}":"")}{(forAreas?"/{area:exists}":"")}/Diagnostics/{{diagnosticsQueryName:alpha}}", dg);

            if (withAuthorization)
            {
                tmp.RequireAuthorization();
            }

            return tmp;
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
                var connection = (string) context.Request.RouteValues["connection"];
                var table = (string) context.Request.RouteValues["table"];
                string id = null;
                string area = null;
                if (context.Request.RouteValues.ContainsKey("id"))
                {
                    id = (string) context.Request.RouteValues["id"];
                }

                if (context.Request.RouteValues.ContainsKey("area"))
                {
                    area = (string) context.Request.RouteValues["area"];
                }

                RouteData routeData = context.GetRouteData();
                ActionDescriptor actionDescriptor = new ActionDescriptor();
                ActionContext actionContext = new ActionContext(context, routeData, actionDescriptor);
                var dbContext = context.RequestServices.ContextForFkQuery(connection, area);
                if (dbContext != null)
                {

                    JsonResult result;
                    if (string.IsNullOrEmpty(id))
                    {
                        result = new JsonResult(dbContext.ReadForeignKey(table));
                    }
                    else
                    {
                        result = new JsonResult(dbContext.ReadForeignKey(table, id: id).Cast<object>().FirstOrDefault());
                    }

                    await result.ExecuteResultAsync(actionContext);
                    return;
                }

                StatusCodeResult notFound = new NotFoundResult();
                await notFound.ExecuteResultAsync(actionContext);
            };
            var tmp = builder.MapGet($"{(forExplicitTenants?$"/{{{explicitTenantParam}:permissionScope}}":"")}{(forAreas?"/{area:exists}":"")}/ForeignKey/{{connection:regex(^[\\w_]+$)}}/{{table:regex(^[\\w_]+$)}}/{{id:regex(^[-@\\w_\\+\\:]+$)?}}", dlg);

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
                var formsDictionary = TranslateForm(await former.ReadFormAsync());
                if (formsDictionary.ContainsKey("NewTenant"))
                {
                    var newTenant = (string) formsDictionary["NewTenant"];
                    var scopeProvider = actionContext.HttpContext.RequestServices.GetRequiredService<IPermissionScope>();
                    var securityRepo = actionContext.HttpContext.RequestServices.GetRequiredService<ISecurityRepository>();
                    var userProvider = actionContext.HttpContext.RequestServices.GetRequiredService<IUserNameMapper>();
                    var eligibleTenants = securityRepo.GetEligibleScopes(userProvider.GetUserLabels(context.User));
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
                var repo = context.RequestServices.GetService<ISecurityRepository>();
                var mapper = context.RequestServices.GetService<IUserNameMapper>();
                var retVal = repo.GetPermissions(mapper.GetUserLabels(context.User), context.User.Identity.AuthenticationType);
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
                var requiredPermissions = fileHandler.PermissionsForReason(reason);
                if (!withAuthorization || (requiredPermissions != null && context.RequestServices.VerifyUserPermissions(requiredPermissions)))
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
                                    if (!string.IsNullOrEmpty(name))
                                    {
                                        name = WebUtility.HtmlEncode(name);
                                    }

                                    if (VerifyFile(name, content, options, out var deniedReason))
                                    {
                                        if (string.IsNullOrEmpty(uploadHint))
                                        {
                                            fileHandler.AddFile(name, content, ms, context.User?.Identity, (n, c) => VerifyFile(n, c, options, out _));
                                        }
                                        else
                                        {
                                            fileHandler.AddFile(name, content, uploadHint, ms, context.User?.Identity);
                                        }
                                        if (!ms.IsValid)
                                        {
                                            ContentResult br = new ContentResult {Content = string.Join("\r\n",(from t in ms where t.Key== "File" select t.Value.Errors).SelectMany(m => m).Select(i => i.ErrorMessage)), ContentType = "text/plain", StatusCode = (int) HttpStatusCode.BadRequest};
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
                                    if (fileHandler is IFormProcessor former)
                                    {
                                        formContent = former.ProcessForm(section.Headers, section.Body);
                                    }

                                    formContent ??= Encoding.Default.GetString(await section.Body.ToArrayAsync());
                                    logger.LogDebug(new EventId(0, "Found Form-Disposition"), $"Form Content: {formContent})");
                                }
                            }
                        }

                        if (fileHandler is not IRespondingFileHandler rfh)
                        {
                            await new OkResult().ExecuteResultAsync(actionContext);
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
                var requiredPermissions = fileHandler.PermissionsForReason(fileToken.DownloadReason);
                if (!withAuthorization || (requiredPermissions != null && context.RequestServices.VerifyUserPermissions(requiredPermissions)))
                {
                    string downloadName = fileToken.DownloadName;
                    string contentType = fileToken.ContentType;
                    bool forceDownload = fileToken.FileDownload;
                    if (fileHandler.ReadFile(fileToken.FileIdentifier, context.User?.Identity, ref downloadName, ref contentType, ref forceDownload, out var fileContent))
                    {
                        var fileResult = new FileContentResult(fileContent, contentType);
                        
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
                var action = (string) context.Request.RouteValues["Action"];
                RouteData routeData = context.GetRouteData();

                ActionDescriptor actionDescriptor = new ActionDescriptor();
                ActionContext actionContext = new ActionContext(context, routeData, actionDescriptor);
                if (!withAuthorization || context.RequestServices.VerifyUserPermissions(new []{"BrowseFs"}))
                {
                    string path = "/";
                    string pattern = null;
                    if (context.Request.Query.ContainsKey("Path"))
                    {
                        path += context.Request.Query["Path"];
                    }

                    if (context.Request.Query.ContainsKey("Pattern"))
                    {
                        pattern = context.Request.Query["Pattern"];
                    }

                    switch (action.ToLower())
                    {
                        case "download":
                            if (File.Exists(path))
                            {
                                FileResult fsr = new FileContentResult(File.ReadAllBytes(path), "application/octet-stream");
                                await fsr.ExecuteResultAsync(actionContext);
                                return;
                            }
                            break;
                        case "list":
                            if (Directory.Exists(path))
                            {
                                JsonResult result = new JsonResult((from t in Directory.GetFiles(path) select new {Name = Path.GetFileName(t), Type="File", Path = Path.GetDirectoryName(t)}).Union((from t in Directory.GetDirectories(path) select new {Name = Path.GetFileName(t), Type="Directory", Path = Path.GetDirectoryName(t)}).OrderBy(n => n.Name)).ToArray());
                                await result.ExecuteResultAsync(actionContext);
                                return;
                            }

                            break;
                        case "search":
                            if (Directory.Exists(path) && !string.IsNullOrEmpty(pattern))
                            {
                                var directories = new List<string>();
                                var dir = new string[] {path};
                                var files = new List<string>();
                                while (dir.Length != 0)
                                {
                                    foreach (var item in dir)
                                    {
                                        try
                                        {
                                            files.AddRange(Directory.GetFiles(item,pattern));
                                            directories.AddRange(Directory.GetDirectories(item));
                                        }
                                        catch (Exception ex)
                                        {
                                        }
                                    }
                                    dir = directories.ToArray();
                                    directories.Clear();
                                }

                                JsonResult result = new JsonResult((from t in files select new {Name = Path.GetFileName(t), Type="File", Path = Path.GetDirectoryName(t)}).ToArray());
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
        /// Translates a specific filter to a Dictionary that is processable by the Context-Extensions for ForeignKey processing
        /// </summary>
        /// <param name="values">the values that were posted in a forms-dictionary</param>
        /// <returns>a more accurate search-dictioanry</returns>
        private static Dictionary<string,object> TranslateForm(Dictionary<string,StringValues> values)
        {
            var ret = new Dictionary<string, object>();
            foreach (var v in values)
            {
                
                    ret.Add(v.Key, v.Value.FirstOrDefault());
            }

            return ret;
        }
    }
}
