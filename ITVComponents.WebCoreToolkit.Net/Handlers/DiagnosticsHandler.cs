using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Extensions;
using ITVComponents.WebCoreToolkit.Net.ViewModel;
using ITVComponents.WebCoreToolkit.Routing;
using ITVComponents.WebCoreToolkit.Tokens;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Attributes;
namespace ITVComponents.WebCoreToolkit.Net.Handlers
{
    internal static class DiagnosticsHandler
    {
        /// <summary>
        /// Executes a Diagnostics query and returns the result as json
        /// </summary>
        /// <param name="context">the http-context in which the query is being executed</param>
        /// <param name="area">the area. This parameter can be left empty. Its is used only, if the query requires special Area-driven PlugIns</param>
        /// <param name="diagnosticsQueryName">the diagnostics-query that needs to be executed</param>
        /// <param name="fileHandler">the file-handler name that will export the selected data to a specific file-format</param>
        /// <response code="200">a Json with the result of the diagnostics-query</response>
        /// <response code="307">a Redirect, if a FileHandler-Name is provided</response>
        /// <response code="404">a not-found when the given Diagnostics-Query does not exist</response>
        public static async Task<IResult> Process(HttpContext context, string area, string diagnosticsQueryName, string fileHandler)
        {
            var contextObj = new
            {
                HttpContext = context,
                User = context.User.Identity,
                CurrentClaims = new Dictionary<string, IList<string>>(from c in context.User.Claims
                    group c by c.Type
                    into g
                    select new KeyValuePair<string, IList<string>>(g.Key, g.Select(n => n.Value).ToList()))
            };

            var queryArg = new Dictionary<string, string>(context.Request.Query.Select(n =>
                new KeyValuePair<string, string>(n.Key, Tools.TranslateValue(n.Value.ToString(), contextObj))));
            if (string.IsNullOrEmpty(fileHandler) || !Regex.IsMatch(fileHandler, "^\\w+$"))
            {
                var dbContext = context.RequestServices.ContextForDiagnosticsQuery(diagnosticsQueryName, area, out var diagQuery);
                if (dbContext != null)
                {
                    return Results.Json(dbContext.RunDiagnosticsQuery(diagQuery, queryArg), new JsonSerializerOptions());
                }
            }
            else
            {
                queryArg["$$QUERYNAME"] = diagnosticsQueryName;
                queryArg["$$QUERYAREA"] = area;
                var token = new DownloadToken
                {
                    ContentType = "application/octet-stream",
                    DownloadName = "Error-Handler-Should-Set-File-Name.bin",
                    DownloadReason = diagnosticsQueryName,
                    FileDownload = true,
                    FileIdentifier = queryArg.CompressToken(encrypt: false),
                    HandlerModuleName = fileHandler
                }.CompressToken();
                var urlFormat = context.RequestServices.GetService<IUrlFormat>();
                var url = urlFormat.FormatUrl($"[SlashPermissionScope]/File/{token}");
                return Results.Redirect(url, false);
            }

            return Results.NotFound();
        }
    }
}
