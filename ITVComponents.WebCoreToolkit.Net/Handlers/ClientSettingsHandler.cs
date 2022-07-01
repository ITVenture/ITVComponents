using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Net.Handlers
{
    internal static class ClientSettingsHandler
    {
        /// <summary>
        /// Reads a settings-value from the configured settings-repository
        /// </summary>
        /// <param name="context">the http-context in which the query is being executed</param>
        /// <param name="kind">the kind of settings to select (global or tenant)</param>
        /// <param name="type">the type of settings to select (json or literal)</param>
        /// <param name="key">the name of the requested settings-record</param>
        /// <param name="contentType">the expected content-type</param>
        /// <response code="200">the raw-value of the configured value with the appropriate content-type when it was found</response>
        /// <response code="404">when the requested setting does not exist in the current users scope</response>
        public static async Task<IResult> ReadSettings(HttpContext context, string kind, string type, string key, string contentType = "text/plain")
        {
            ISettingsProvider settingsProvider;
            if (kind.Equals("global", StringComparison.OrdinalIgnoreCase))
            {
                settingsProvider = context.RequestServices.GetService<IGlobalSettingsProvider>();
            }
            else if (kind.Equals("tenant", StringComparison.OrdinalIgnoreCase))
            {
                settingsProvider = context.RequestServices.GetService<IScopedSettingsProvider>();
            }
            else
            {
                settingsProvider = null;
            }

            if (settingsProvider != null)
            {
                var plainSetting = "";
                if (type.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    plainSetting = settingsProvider.GetJsonSetting(key);
                    contentType = "application/json";
                }
                else if (type.Equals("literal", StringComparison.OrdinalIgnoreCase))
                {
                    plainSetting = settingsProvider.GetLiteralSetting(key);
                }

                if (!string.IsNullOrEmpty(plainSetting))
                {
                    var content = Encoding.Default.GetBytes(plainSetting);
                    return Results.File(content, contentType);
                }
            }

            return Results.NotFound();
        }
    }
}
