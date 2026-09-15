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
            // Der Maschinen-Weg: ein Geraet tauscht seinen Schluessel gegen einen Bearer. AllowAnonymous,
            // weil der Schluessel SELBST der Ausweis ist - er kommt im X-Api-Key-Kopf, nicht als
            // Anmeldung. Bis PRE239 gab es diesen Endpunkt an keinem Ende, eine Maschine kam also gar
            // nicht an ein erstes Token.
            builder.MapPost("/ClientAppToken", ClientAppTokenHandler.IssueToken)
                .AllowAnonymous()
                .Produces<ClientAppTokenResult>(contentType: "application/json");

            return builder.MapPost("/UserToken/Refresh", WebTokenHandler.RefreshToken)
                .Accepts<RefreshJwtTokenModel>("application/json")
                .Produces<RefreshJwtTokenModel>(contentType: "application/json");
        }
    }
}
