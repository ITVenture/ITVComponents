using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.XPath;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.Help;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Help
{
    public class DbTutorialSource:ITutorialSource
    {
        private readonly SecurityContext dbContext;

        public DbTutorialSource(SecurityContext dbContext)
        {
            this.dbContext = dbContext;
        }


        public bool HasVideoTutorials(HttpContext httpContext, string location = null)
        {
            return httpContext.HasVideoTutorials(location);
        }

        public IEnumerable<TutorialDefinition> GetTutorials(string? pathValue, string culture)
        {
            string baseLang = null;
            string currentCulture = culture;
            if (currentCulture.Contains("-"))
            {
                baseLang = currentCulture.Substring(0, currentCulture.IndexOf("-"));
            }

            return dbContext.Tutorials.Where(n => n.ModuleUrl == pathValue && n.Streams.Any(n => n.LanguageTag == currentCulture || n.LanguageTag == baseLang || n.LanguageTag == "Default")).OrderBy(n => n.SortableName)
                .ToArray()
                .Select(n => new TutorialDefinition
                {
                    DisplayName = n.DisplayName,
                    Description = n.Description,
                    VideoTutorialId = n.VideoTutorialId,
                    Streams = (from t in n.Streams select t.ToViewModel<TutorialStream,TutorialStreamDefinition>()).ToArray()
                }).ToList();
        }
    }
}
