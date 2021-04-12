using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Navigation
{
    internal class Navigator:INavigator
    {
        private readonly INavigationBuilder builder;
        private readonly IHttpContextAccessor httpContext;
        private NavigationMenu rootObject;

        /// <summary>
        /// Initializes a new instance of the Navigator class
        /// </summary>
        /// <param name="builder">a navigation builder instance that creates the navigation-raw collection</param>
        /// <param name="httpContext">the accessor to retreive the current http-context</param>
        public Navigator(INavigationBuilder builder, IHttpContextAccessor httpContext)
        {
            this.builder = builder;
            this.httpContext = httpContext;
        }

        /// <summary>
        /// Gets the Navigation-Collection for this site
        /// </summary>
        public ICollection<NavigationMenu> SiteNavigation => (rootObject ??= BuildRootObject()).Children;

        /// <summary>
        /// Creates an cleans the root object for the site-navigation
        /// </summary>
        /// <returns>the root of the site-navigation</returns>
        private NavigationMenu BuildRootObject()
        {
            var currentPath = httpContext.HttpContext.Request.Path;
            var retVal = builder.GetNavigationRoot();
            IRequestCultureFeature cult = null;
            try
            {
                cult = httpContext.HttpContext.Features.Get<IRequestCultureFeature>();
            }
            catch
            {
            }

            string currentCulture = null;
            if (cult != null)
            {
                currentCulture = cult.RequestCulture.UICulture.Name;
            }

           retVal.CleanUp(currentPath, currentCulture);
            return retVal;
        }
    }
}
