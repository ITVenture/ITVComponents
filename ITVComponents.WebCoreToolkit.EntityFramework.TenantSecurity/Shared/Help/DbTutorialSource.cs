using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.Help;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Help
{
    public class DbTutorialSource:ITutorialSource
    {
        private readonly IToolkitContextFactory contextFactory;

        public DbTutorialSource(IToolkitContextFactory contextFactory)
        {
            this.contextFactory = contextFactory;
        }


        public bool HasVideoTutorials(HttpContext httpContext, ref string location)
        {
            return httpContext.HasVideoTutorials(ref location);
        }

        public bool HasVideoTutorials(HttpContext httpContext, string location = null)
        {
            string dummy = null;
            return HasVideoTutorials(httpContext, ref dummy);
        }

        public IEnumerable<TutorialDefinition> GetTutorials(string? pathValue, string culture)
        {
            string baseLang = null;
            string currentCulture = culture;
            if (currentCulture.Contains("-"))
            {
                baseLang = currentCulture.Substring(0, currentCulture.IndexOf("-"));
            }

            using var lease = contextFactory.Lease<ICoreSystemContext>();
            var dbContext = lease.Context;
            return dbContext.Tutorials.Where(n => n.ModuleUrl.ToLower() == pathValue.ToLower() && n.Streams.Any(n => n.LanguageTag == currentCulture || n.LanguageTag == baseLang || n.LanguageTag == "Default")).OrderBy(n => n.SortableName)
                .ToArray()
                .Select(n => new TutorialDefinition
                {
                    DisplayName = n.DisplayName,
                    Description = n.Description,
                    VideoTutorialId = n.VideoTutorialId,
                    Streams = TruncateCultures((from t in n.Streams.Where(n => n.LanguageTag == currentCulture || n.LanguageTag == baseLang || n.LanguageTag == "Default") select t.ToViewModel<TutorialStream,TutorialStreamDefinition>()).ToList(), culture, baseLang)
                }).ToList();
        }

        private TutorialStreamDefinition[] TruncateCultures(List<TutorialStreamDefinition> orig, string targetCulture, string baseCulture)
        {
            List<TutorialStreamDefinition> retVal = new List<TutorialStreamDefinition>();
            retVal.AddRange(orig.Where(v => v.LanguageTag == targetCulture));
            if (baseCulture != targetCulture)
            {
                retVal.AddRange(orig.Where(n => n.LanguageTag == baseCulture && retVal.All(v => v.ContentType != n.ContentType)));
            }

            retVal.AddRange(orig.Where(n => n.LanguageTag == "Default" && retVal.All(v => v.ContentType != n.ContentType)));
            return retVal.ToArray();
        }
    }
}
