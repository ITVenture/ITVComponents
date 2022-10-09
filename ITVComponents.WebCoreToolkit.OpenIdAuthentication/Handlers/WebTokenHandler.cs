using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using ITVComponents.WebCoreToolkit.OpenIdAuthentication.JWT;
using ITVComponents.WebCoreToolkit.OpenIdAuthentication.Model;
using System.DirectoryServices.AccountManagement;
using ITVComponents.WebCoreToolkit.Security.ApplicationToken;

namespace ITVComponents.WebCoreToolkit.OpenIdAuthentication.Handlers
{
    internal static class WebTokenHandler
    {
        /// <summary>
        /// Gets a refresh-Token for the provided user
        /// </summary>
        /// <param name="context">the http-context in which the query is being executed</param>
        /// <param name="refreshModel">the refresh-query containing the expired web-token and the refresh-token</param>
        /// <response code="200">an empty OK-result, when the token could be refreshed successfully</response>
        /// <response code="401">when either the access-token or the refresh-token is invalid</response>
        /// <response code="400">for any unexpected errors</response>
        public static async Task<IResult> RefreshToken(HttpContext context, [FromBody]RefreshJwtTokenModel refreshModel,
            [FromServices]IJwtService jwtService, [FromServices] IApplicationTokenService tokenService)
        {
            if (refreshModel is null)
                return Results.BadRequest("Invalid client request");
            var refreshIdentity = jwtService.GetPrincipalFromExpiredToken(refreshModel.AccessToken, out var applicationKey);
            if (refreshIdentity != null &&
                tokenService.VerifyRefreshToken(refreshIdentity, applicationKey, refreshModel.RefreshToken))
            {
                var newAccessToken = jwtService.GetJwtTokenFor(refreshIdentity, applicationKey);
                var newRefreshToken = jwtService.GetRefreshToken();
                tokenService.UpdateRefreshToken(refreshIdentity, applicationKey, newRefreshToken,
                    refreshModel.RefreshToken);
                return Results.Json(new RefreshJwtTokenModel
                {
                    AccessToken = newAccessToken,
                    RefreshToken = newRefreshToken
                });
            }

            return Results.Unauthorized();
        }
    }
}
