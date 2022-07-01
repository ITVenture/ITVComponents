using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using ITVComponents.WebCoreToolkit.EntityFramework.Extensions;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Net.Handlers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Extensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Handlers.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Handlers
{
    internal static class ForeignKeyHandler
    {
        /// <summary>
        /// Reads ForeignKey data for a specified table on a specific database-Connection in a Kendo-ajax source compilant way
        /// </summary>
        /// <param name="context">the http-context in which the query is being executed</param>
        /// <param name="area">the area. This parameter can be left empty. Its is used only, if the query requires special Area-driven PlugIns</param>
        /// <param name="connection">the name of the Database-connection plugin on which the specified table can be accessed</param>
        /// <param name="table">the table-name from which to read the data</param>
        /// <param name="form">the data-source request as form-data</param>
        /// <response code="200">a Json-Array containing the foreign-key data with custom search-arguments applied</response>
        /// <response code="401">if access to the desired connection or table is denied</response>
        /// <response code="404">a not-found when the requested connection or table does not exist</response>
        public static async Task<IResult> FkWithAuth(HttpContext context, string area, string connection, string table, [FromForm] SearchForm form)
        {
            return await ReadForeignKey(context, true, area, connection, table, form);
        }

        /// <summary>
        /// Reads ForeignKey data for a specified table on a specific database-Connection in a Kendo-ajax source compilant way
        /// </summary>
        /// <param name="context">the http-context in which the query is being executed</param>
        /// <param name="area">the area. This parameter can be left empty. Its is used only, if the query requires special Area-driven PlugIns</param>
        /// <param name="connection">the name of the Database-connection plugin on which the specified table can be accessed</param>
        /// <param name="table">the table-name from which to read the data</param>
        /// <param name="form">the data-source request as form-data</param>
        /// <response code="200">a Json-Array containing the foreign-key data with custom search-arguments applied</response>
        /// <response code="401">if access to the desired connection or table is denied</response>
        /// <response code="404">a not-found when the requested connection or table does not exist</response>
        public static async Task<IResult> FkNoAuth(HttpContext context, string area, string connection, string table, [FromForm]SearchForm form)
        {
            return await ReadForeignKey(context, false, area, connection, table, form);
        }

        private static async Task<IResult> ReadForeignKey(HttpContext context, bool withAuthorization, string area,
            string connection, string table, SearchForm form)
        {
            var ok = !withAuthorization || context.RequestServices.VerifyCurrentUser();

            if (ok)
            {
                if (!string.IsNullOrEmpty(connection))
                {
                    /*var baseHint = ((string)context.Request.RouteValues["dataResolveHint"])?.Split("/")
                        .Select(n => HttpUtility.UrlDecode(n)).ToArray();*/
                    var dbContext = context.RequestServices.ContextForFkQuery(connection, area);
                    if (dbContext != null && Tools.RegexValidate(table,
                            dbContext.CustomFkSettings?.CustomTableValidation ?? "^[\\w_]+$"))
                    {

                        try
                        {
                            return Results.Json(dbContext.ReadForeignKey(table, postedFilter: form.SearchDictionary)
                                .ToDummyDataSourceResult(), new JsonSerializerOptions());
                        }
                        catch (SecurityException)
                        {
                        }

                        return Results.Unauthorized();
                    }
                }
            }
            else
            {
                return Results.Unauthorized();
            }

            return Results.NotFound();
        }
    }
}