using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using ITVComponents.InterProcessCommunication.Shared.WatchDogs;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.Extensions;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Handlers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Handlers.Model;
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
        public static IEndpointConventionBuilder UseFilteredAutoForeignKeys(this WebApplication builder, string explicitTenantParam, bool forAreas, bool withAuthorization = true)
        {
            bool forExplicitTenants = !string.IsNullOrEmpty(explicitTenantParam);
            ContextExtensions.Init();
            RouteHandlerBuilder tmp;
            if (withAuthorization)
            {
                tmp = builder.MapPost(
                    $"{(forExplicitTenants ? $"/{{{explicitTenantParam}:permissionScope}}" : "")}{(forAreas ? "/{area:exists}" : "")}/ForeignKey/{{connection:regex(^[\\w_]+$)}}/{{table:required}}",
                    ForeignKeyHandler.FkWithAuth).RequireAuthorization().Accepts<SearchForm>("Application/x-www-form-urlencoded");

            }
            else
            {
                tmp = builder.MapPost(
                    $"{(forExplicitTenants ? $"/{{{explicitTenantParam}:permissionScope}}" : "")}{(forAreas ? "/{area:exists}" : "")}/ForeignKey/{{connection:regex(^[\\w_]+$)}}/{{table:required}}",
                    ForeignKeyHandler.FkNoAuth).Accepts<SearchForm>("Application/x-www-form-urlencoded");
            }

            return tmp;
        }

        private static bool RegexValidate(string value, string regexPattern)
        {
            return Regex.IsMatch(value, regexPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }
    }
}
