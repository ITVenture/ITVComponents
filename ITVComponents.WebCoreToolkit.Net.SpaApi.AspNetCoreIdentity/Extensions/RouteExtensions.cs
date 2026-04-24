using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ITVComponents.WebCoreToolkit.Net.SpaApi.AspNetCoreIdentity.Handlers;

namespace ITVComponents.WebCoreToolkit.Net.SpaApi.AspNetCoreIdentity.Extensions
{
    public static class RouteExtensions
    {
        /// <summary>
        /// Add endpoints for registering, logging in, and logging out using ASP.NET Core Identity.
        /// </summary>
        /// <typeparam name="TUser">The type describing the user. This should match the generic parameter in <see cref="UserManager{TUser}"/>.</typeparam>
        /// <param name="endpoints">
        /// The <see cref="IEndpointRouteBuilder"/> to add the identity endpoints to.
        /// Call <see cref="EndpointRouteBuilderExtensions.MapGroup(IEndpointRouteBuilder, string)"/> to add a prefix to all the endpoints.
        /// </param>
        /// <returns>An <see cref="IEndpointConventionBuilder"/> to further customize the added endpoints.</returns>
        public static IEndpointConventionBuilder MapIdentityApi<TUser>(this IEndpointRouteBuilder endpoints)
            where TUser : class, new()
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            // We'll figure out a unique endpoint name based on the final route pattern during endpoint generation.
            string? confirmEmailEndpointName = null;

            var routeGroup = endpoints.MapGroup("");

            // NOTE: We cannot inject UserManager<TUser> directly because the TUser generic parameter is currently unsupported by RDG.
            // https://github.com/dotnet/aspnetcore/issues/47338
            routeGroup.MapPost("/register", LoginHandler.Register<TUser>);

            routeGroup.MapPost("/login", LoginHandler.Login<TUser>);

            routeGroup.MapPost("/refresh", LoginHandler.Refresh<TUser>);

            routeGroup.MapGet("/confirmEmail", LoginHandler.ConfirmEmail<TUser>)
            .Add(endpointBuilder =>
            {
                var finalPattern = ((RouteEndpointBuilder)endpointBuilder).RoutePattern.RawText;
                confirmEmailEndpointName = $"{nameof(MapIdentityApi)}-{finalPattern}";
                endpointBuilder.Metadata.Add(new EndpointNameMetadata(confirmEmailEndpointName));
                LoginHandler.PublishConfirmEndpoint(confirmEmailEndpointName);
            });

            routeGroup.MapPost("/resendConfirmationEmail", LoginHandler.ResendConfirmationEmail<TUser>);

            routeGroup.MapPost("/forgotPassword", LoginHandler.ForgotPassword<TUser>);

            routeGroup.MapPost("/resetPassword", LoginHandler.ResetPassword<TUser>);

            var accountGroup = routeGroup.MapGroup("/manage").RequireAuthorization();

            accountGroup.MapPost("/2fa", LoginHandler.TwoFa<TUser>);

            accountGroup.MapGet("/info", LoginHandler.UserInfo<TUser>);

            accountGroup.MapPost("/info", LoginHandler.UpdateUserInfo<TUser>);

            

            return new IdentityEndpointsConventionBuilder(routeGroup);
        }

        private sealed class IdentityEndpointsConventionBuilder : IEndpointConventionBuilder
        {
            private readonly RouteGroupBuilder inner;

            public IdentityEndpointsConventionBuilder(RouteGroupBuilder inner)
            {
                this.inner = inner;
            }
            private IEndpointConventionBuilder InnerAsConventionBuilder => inner;

            public void Add(Action<EndpointBuilder> convention) => InnerAsConventionBuilder.Add(convention);
            public void Finally(Action<EndpointBuilder> finallyConvention) => InnerAsConventionBuilder.Finally(finallyConvention);
        }
    }
}
