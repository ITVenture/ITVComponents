using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Help
{
    public interface ITutorialSource
    {
        bool HasVideoTutorials(HttpContext httpContext, string location = null);
        IEnumerable<TutorialDefinition> GetTutorials(string? pathValue, string culture);
    }
}
