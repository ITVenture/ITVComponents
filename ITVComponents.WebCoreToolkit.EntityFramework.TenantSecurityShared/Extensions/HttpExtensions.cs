using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.DuckTyping.Extensions;
using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions
{
    public static class HttpExtensions
    {
        /// <summary>
        /// Indicates whether there are video-tutorials available for a specific module
        /// </summary>
        /// <param name="httpContext">the http-context that is used for the current request</param>
        /// <param name="location">the location for which to check, whether a tutorial is available. If this value is null, the Request-path is used.</param>
        /// <returns>a value indicating whether the provided path contains video-tutorials</returns>
        public static bool HasVideoTutorials(this HttpContext httpContext, ref string location)
        {
            if (string.IsNullOrEmpty(location))
            {
                location = httpContext.Request.Path;
            }

            var cult = httpContext.Features.Get<IRequestCultureFeature>();
            var db = httpContext.RequestServices
                .GetRequiredService<ICoreSystemContext>();
            var psc = httpContext.RequestServices.GetService<IPermissionScope>();
            string currentCulture = null;
            string baseLang = null;
            var ppf = $"/{psc?.PermissionPrefix}";
            if (psc != null && psc.IsScopeExplicit && location.StartsWith(ppf, StringComparison.OrdinalIgnoreCase))
            {
                location = location.Substring(ppf.Length);
            }

            if (cult != null)
            {
                currentCulture = cult.RequestCulture.UICulture.Name;
                if (currentCulture.Contains("-"))
                {
                    baseLang = currentCulture.Substring(0, currentCulture.IndexOf("-"));
                }
            }

            var loca = location;
            var exists = db.Tutorials.Any(n =>
                n.ModuleUrl.ToLower() == loca.ToLower() &&
                n.Streams.Any(n => n.LanguageTag == currentCulture || n.LanguageTag == baseLang));
            if (!exists)
            {
                exists = db.Tutorials.Any(
                    n => n.ModuleUrl.ToLower() == loca.ToLower() && n.Streams.Any(n => n.LanguageTag == "Default"));
            }

            return exists;
        }

        /// <summary>
        /// Gets all available streams for a specific tutorial
        /// </summary>
        /// <param name="httpContext">the current httpcontext</param>
        /// <param name="tutorialId">the id of the tutorial the user whishes to see</param>
        /// <returns>a list of all streams available for the provided tutorial</returns>
        public static ICollection<TutorialStream> GetVideoStreams(this HttpContext httpContext, int tutorialId)
        {
            var cult = httpContext.Features.Get<IRequestCultureFeature>();
            var db = httpContext.RequestServices.GetRequiredService<ICoreSystemContext>();
            string currentCulture = null;
            string baseLang = null;
            if (cult != null)
            {
                currentCulture = cult.RequestCulture.UICulture.Name;
                if (currentCulture.Contains("-"))
                {
                    baseLang = currentCulture.Substring(0, currentCulture.IndexOf("-"));
                }
            }

            var allStreams = db.TutorialStreams.Where(n =>
                    n.VideoTutorialId == tutorialId && (n.LanguageTag == currentCulture || n.LanguageTag == baseLang)).OrderBy(n => n.ContentType).ThenByDescending(n => n.LanguageTag.Length)
                .Union(db.TutorialStreams.Where(
                    n => n.VideoTutorialId == tutorialId && n.LanguageTag == "Default").OrderBy(n => n.ContentType));
            var li = new List<TutorialStream>();
            var enc = new List<string>();
            foreach (var stream in allStreams)
            {
                var codid = stream.ContentType;
                if (!enc.Contains(codid))
                {
                    enc.Add(codid);
                    li.Add(stream);
                }
            }

            return li;
        }
    }
}