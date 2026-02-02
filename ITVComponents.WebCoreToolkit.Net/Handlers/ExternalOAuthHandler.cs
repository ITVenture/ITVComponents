using ITVComponents.WebCoreToolkit.ExternalServiceConnect.Helpers;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;
using ITVComponents.WebCoreToolkit.Net.Handlers.Model;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.Handlers
{
    internal class ExternalOAuthHandler
    {
        /// <summary>
        /// Upload-Endpoint for File-operations
        /// </summary>
        /// <param name="context">the http-context in which the query is being executed</param>
        /// <param name="name">the name of the externalService for which the authentication-flow is initialized</param>
        /// <param name="securityRepo">a security repository containing information about the current user and its tenant</param>
        public static async Task<IResult> InitAuthentication(HttpContext context, [FromRoute(Name = "externalServiceName")] string name,
            [FromServices] ISecurityRepository securityRepo)
        {
            var connection = securityRepo.GetExternalService(name);

            var bt32 = new byte[32];
            RandomNumberGenerator.Create().GetBytes(bt32);
            var state = string.Join("", from t in bt32 select t.ToString("X2"));
            var verifier = PkceHelper.CreateCodeVerifier();
            var challenge = PkceHelper.CreateCodeChallenge(verifier);

            securityRepo.PrepareExternalServiceConnect(new OAuthState
            {
                State = state,
                ConnectionName = name,
                CodeVerifier = verifier,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
            });

            var stateX = string.Join("",Encoding.UTF8.GetBytes(state).Select(n => n.ToString("X2")));
            state = $"{stateX}_{state}";
            var url =
                $"{connection.AuthorizationEndpoint}?" +
                $"response_type=code&" +
                $"client_id={Uri.EscapeDataString(connection.ClientId)}&" +
                $"redirect_uri={Uri.EscapeDataString(connection.RedirectUri)}&" +
                $"scope={Uri.EscapeDataString(connection.Scope)}&" +
                $"state={state}"+
                $"code_challenge={challenge}&" +
                "code_challenge_method=S256";

            return Results.Redirect(url);
        }

        /// <summary>
        /// Upload-Endpoint for File-operations
        /// </summary>
        /// <param name="context">the http-context in which the query is being executed</param>
        /// <param name="name">the name of the externalService for which the authentication-flow is initialized</param>
        /// <param name="securityRepo">a security repository containing information about the current user and its tenant</param>
        public static async Task<IResult> Callback(HttpContext context,
            HttpRequest request,
            [FromServices] ISecurityRepository securityRepo,
            [FromServices] IPermissionScope scopeProvider,
            [FromServices] IHttpClientFactory httpClientFactory)
        {
            var code = request.Query["code"].ToString();
            var state = request.Query["state"].ToString().Split("_");
            var translatedState = "";
            for (var i= 0; i< state[0].Length; i+=2)
            {
                var hex = state[0].Substring(i, 2);
                translatedState += (char)Convert.ToByte(hex, 16);
            }

            var stateEntry = securityRepo.GetOAuthRequest(translatedState, state[1]);
            if (stateEntry == null)
                return Results.BadRequest("Invalid state");

            var connection = securityRepo.GetExternalService(stateEntry.ConnectionName);

            var client = httpClientFactory.CreateClient();

            var rawForm = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = connection.RedirectUri,
                ["client_id"] = connection.ClientId,
                ["code_verifyer"] = stateEntry.CodeVerifier
            };

            if (!string.IsNullOrEmpty(connection.ClientSecret))
            {
                rawForm.Add("client_secret",connection.ClientSecret);
            }

            var tokenResponse = await client.PostAsync(
                connection.TokenEndpoint,
                new FormUrlEncodedContent(rawForm));

            tokenResponse.EnsureSuccessStatusCode();

            var token = await tokenResponse.Content
                .ReadFromJsonAsync<TokenResponse>();
            var dbToken = new TranslatedTokenResponse
            {
                ExpiresAt =
                    DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn),
                Scope = token.Scope,
                RefreshToken = token.RefreshToken,
                AccessToken = token.AccessToken,
                TokenType = token.TokenType
            };

            securityRepo.StoreExternalServiceToken(
                stateEntry.ConnectionName,
                dbToken);

            return Results.LocalRedirect("/");
        }
    }
}
