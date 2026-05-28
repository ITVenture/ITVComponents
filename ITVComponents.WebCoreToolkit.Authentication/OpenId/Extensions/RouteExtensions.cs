using Microsoft.AspNetCore.Builder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Authentication.OpenId.Handlers;
using ITVComponents.WebCoreToolkit.Authentication.OpenId.Model;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.Authentication.OpenId.Extensions
{
    public static class RouteExtensions
    {
        public static RouteHandlerBuilder UseTokenEndpoints(this WebApplication builder)
        {
            return builder.MapPost("/UserToken/Refresh", WebTokenHandler.RefreshToken)
                .Accepts<RefreshJwtTokenModel>("application/json")
                .Produces<RefreshJwtTokenModel>(contentType: "application/json");
        }
    }
}
