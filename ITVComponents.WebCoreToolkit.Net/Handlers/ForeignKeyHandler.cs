using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.Extensions;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;

namespace ITVComponents.WebCoreToolkit.Net.Handlers
{
    internal static class ForeignKeyHandler
    {
        /// <summary>
        /// Reads ForeignKey data for a specified table on a specific database-Connection
        /// </summary>
        /// <param name="context">the http-context in which the query is being executed</param>
        /// <param name="area">the area. This parameter can be left empty. Its is used only, if the query requires special Area-driven PlugIns</param>
        /// <param name="connection">the name of the Database-connection plugin on which the specified table can be accessed</param>
        /// <param name="table">the table-name from which to read the data</param>
        /// <param name="id">the id, if one single record must be resolved.</param>
        /// <response code="200">a Json-Array containing the foreign-key data if no id is specified</response>
        /// <response code="200">a single foreign-key data item if an id is specified</response>
        /// <response code="401">if access to the desired connection or table is denied</response>
        /// <response code="404">a not-found when the requested connection or table does not exist</response>
        public static async Task<IResult> FkWithAuth(HttpContext context, string area, string connection, string table,
            string id = null)
        {
            return await ReadForeignKey(context, true, area, connection, table, id);
        }

        /// <summary>
        /// Reads ForeignKey data for a specified table on a specific database-Connection
        /// </summary>
        /// <param name="context">the http-context in which the query is being executed</param>
        /// <param name="area">the area. This parameter can be left empty. Its is used only, if the query requires special Area-driven PlugIns</param>
        /// <param name="connection">the name of the Database-connection plugin on which the specified table can be accessed</param>
        /// <param name="table">the table-name from which to read the data</param>
        /// <param name="id">the id, if one single record must be resolved.</param>
        /// <response code="200">a Json-Array containing the foreign-key data if no id is specified</response>
        /// <response code="200">a single foreign-key data item if an id is specified</response>
        /// <response code="401">if access to the desired connection or table is denied</response>
        /// <response code="404">a not-found when the requested connection or table does not exist</response>
        public static async Task<IResult> FkNoAuth(HttpContext context, string area, string connection, string table,
            string id = null)
        {
            return await ReadForeignKey(context, false, area, connection, table, id);
        }

        private static async Task<IResult> ReadForeignKey(HttpContext context, bool withAuthorization, string area, string connection, string table, string id = null)
        {
            bool ok = !withAuthorization || context.RequestServices.VerifyCurrentUser();
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
                        bool valid = !string.IsNullOrEmpty(connection) && !string.IsNullOrEmpty(table);
                        valid = valid && (id == null || Tools.RegexValidate(id,
                            dbContext.CustomFkSettings?.CustomIdValidation ?? "^[-@\\w_\\+\\:]+$"));

                        if (valid)
                        {
                            try
                            {
                                if (string.IsNullOrEmpty(id))
                                {
                                    return Results.Json(dbContext.ReadForeignKey(table), new JsonSerializerOptions());
                                }


                                return Results.Json(dbContext.ReadForeignKey(table, id: id)
                                    .Cast<object>()
                                    .FirstOrDefault(), new JsonSerializerOptions());
                            }
                            catch (SecurityException ex)
                            {
                                LogEnvironment.LogEvent($"ForeignKey failed: {ex.Message}",LogSeverity.Error);
                            }

                            return Results.Unauthorized();
                        }
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
