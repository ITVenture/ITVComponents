using System;
using System.Collections;
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
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Models.Comparers;
using ITVComponents.WebCoreToolkit.Net.FileHandling;
using ITVComponents.WebCoreToolkit.Net.Handlers;
using ITVComponents.WebCoreToolkit.Net.Handlers.Model;
using ITVComponents.WebCoreToolkit.Net.Options;
using ITVComponents.WebCoreToolkit.Net.ViewModel;
using ITVComponents.WebCoreToolkit.Routing;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.AssetLevelImpersonation;
using ITVComponents.WebCoreToolkit.Tokens;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.CodeAnalysis.Diagnostics;
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
        public static IEndpointConventionBuilder UseDiagnostics(this WebApplication builder, string explicitTenantParam, bool forAreas, bool withAuthorization = true)
        {
            bool forExplicitTenants = !string.IsNullOrEmpty(explicitTenantParam);
            ContextExtensions.Init();
            var tmp = builder.MapGet(
                $"{(forExplicitTenants ? $"/{{{explicitTenantParam}:permissionScope}}" : "")}{(forAreas ? "/{area:exists}" : "")}/Diagnostics/{{diagnosticsQueryName:alpha}}",
                DiagnosticsHandler.Process);

            if (withAuthorization)
            {
                tmp.RequireAuthorization();
            }

            return tmp;
        }

        public static void UseWidgets(this WebApplication builder, string explicitTenantParam, out IEndpointConventionBuilder getAction, out IEndpointConventionBuilder postAction, bool withAuthorization = true)
        {
            bool forExplicitTenants = !string.IsNullOrEmpty(explicitTenantParam);
            ContextExtensions.Init();
            getAction = builder.MapGet($"{(forExplicitTenants ? $"/{{{explicitTenantParam}:permissionScope}}" : "")}/DBW/{{widgetName:alpha}}", WidgetHandler.Get);
            postAction =
                builder.MapPost($"{(forExplicitTenants ? $"/{{{explicitTenantParam}:permissionScope}}" : "")}/DBW",
                    WidgetHandler.Set);
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
        public static IEndpointConventionBuilder UseAutoForeignKeys(this WebApplication builder, string explicitTenantParam, bool forAreas, bool withAuthorization = true)
        {
            bool forExplicitTenants = !string.IsNullOrEmpty(explicitTenantParam);
            ContextExtensions.Init();
            RouteHandlerBuilder tmp;
            if (withAuthorization)
            {
                tmp = builder.MapGet(
                    $"{(forExplicitTenants ? $"/{{{explicitTenantParam}:permissionScope}}" : "")}{(forAreas ? "/{area:exists}" : "")}/ForeignKey/{{connection:regex(^[\\w_]+$)}}/{{table:required}}",
                    ForeignKeyHandler.FkWithAuth).RequireAuthorization();

            }
            else
            {
                tmp = builder.MapGet(
                    $"{(forExplicitTenants ? $"/{{{explicitTenantParam}:permissionScope}}" : "")}{(forAreas ? "/{area:exists}" : "")}/ForeignKey/{{connection:regex(^[\\w_]+$)}}/{{table:required}}",
                    ForeignKeyHandler.FkNoAuth);
            }

            return tmp;
        }

        /// <summary>
        /// Exposes the Global and Tenant-Settings to the browser, so that Settings can also be consumed from JavaScript
        /// </summary>
        /// <param name="builder">the route-builder that confgures the application-routing</param>
        /// <param name="withAuthorization">indicates whether to use authentication for this action</param>
        /// <returns>the EndPointConventionBuilder for further configuration</returns>
        public static IEndpointConventionBuilder ExposeClientSettings(this WebApplication builder, string explicitTenantParam, bool withAuthorization = true)
        {
            bool forExplicitTenants = !string.IsNullOrEmpty(explicitTenantParam);
            var tmp = builder.MapGet($"{(forExplicitTenants?$"/{{{explicitTenantParam}:permissionScope}}":"")}/ClientOptions/{{kind:regex(^(global|tenant)$)}}/{{type:regex(^(json|literal)$)}}/{{key:required}}", ClientSettingsHandler.ReadSettings);
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
        public static IEndpointConventionBuilder UseTenantSwitch(this WebApplication builder)
        {
            var tmp = builder.MapPost($"/SwitchTenant", TenantHandler.SwitchTenant).RequireAuthorization().Accepts<TenantSwitchForm>("Application/x-www-form-urlencoded");
            return tmp;
        }

        /// <summary>
        /// Exposes permissions of the current user to the client application
        /// </summary>
        /// <param name="builder">the endpoint-convention builder that is used to create a presented endpoint</param>
        /// <returns>the endpoint-convention builder for further configuration</returns>
        public static IEndpointConventionBuilder ExposeUserPermissions(this WebApplication builder, string explicitTenantParam)
        {
            var forExplicitTenants = !string.IsNullOrEmpty(explicitTenantParam);
            var tmp = builder.MapGet($"{(forExplicitTenants?$"/{{{explicitTenantParam}:permissionScope}}":"")}/UserPermissions", UserPermissionsHandler.ReadUserPermissions).RequireAuthorization();
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
        public static void UseFileServices(this WebApplication builder, string explicitTenantParam, bool withAuthorization = true, Action<IEndpointConventionBuilder> configureUpload = null, Action<IEndpointConventionBuilder> configureDownload = null)
        {
            var forExplicitTenants = !string.IsNullOrEmpty(explicitTenantParam);
//            Func<HttpContext, Task> dlgP = ;

            RouteHandlerBuilder upload;
            RouteHandlerBuilder download;
            if (withAuthorization)
            {
                upload = builder.MapPost($"{(forExplicitTenants ? $"/{{{explicitTenantParam}:permissionScope}}" : "")}/File/{{UploadModule:alpha}}/{{UploadReason:alpha}}", FileServiceHandler.PostWithAuth)
                    .Accepts<MultipartFileModel>("multipart/form-data").RequireAuthorization();
                download = builder.MapGet($"{(forExplicitTenants ? $"/{{{explicitTenantParam}:permissionScope}}" : "")}/File/{{**FileToken}}", FileServiceHandler.GetWithAuth)
                    .RequireAuthorization();
            }
            else
            {
                upload = builder.MapPost($"{(forExplicitTenants ? $"/{{{explicitTenantParam}:permissionScope}}" : "")}/File/{{UploadModule:alpha}}/{{UploadReason:alpha}}", FileServiceHandler.PostNoAuth)
                    .Accepts<MultipartFileModel>("multipart/form-data");
                download = builder.MapGet($"{(forExplicitTenants ? $"/{{{explicitTenantParam}:permissionScope}}" : "")}/File/{{**FileToken}}", FileServiceHandler.GetNoAuth);
            }

            configureUpload?.Invoke(upload);
            configureDownload?.Invoke(download);
        }

        /// <summary>
        /// Enables the FileSystem Exposal when the application runs in an environmen thatn can not be accessed directly, this may be helpful for Diagnostic purposes
        /// </summary>
        /// <param name="builder">the route-builder that confgures the application-routing</param>
        /// <param name="withAuthorization">indicates whether to use authentication for this action</param>
        /// <returns>the resulting EndpointConvention builder for further configuration of the generated action</returns>
        public static IEndpointConventionBuilder ExposeFileSystem(this WebApplication builder, bool withAuthorization = true)
        {

            if (withAuthorization)
            {
                return builder.MapGet("/FileSys/{Action:regex(^(download|list|search)$)}", FileSystemHandler.FileSystemAccessWithAuth).RequireAuthorization();
            }

            return builder.MapGet("/FileSys/{Action:regex(^(download|list|search)$)}", FileSystemHandler.FileSystemAccessNoAuth).AllowAnonymous();
        }
    }
}
