using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Caching;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Navigation
{
    internal class Navigator:INavigator
    {
        private readonly INavigationBuilder builder;
        private readonly IContextUserProvider userProvider;
        private readonly IEntityChangeSignal changeSignal;
        private NavigationMenu rootObject;
        private DateTime builtAtUtc;

        /// <summary>
        /// Initializes a new instance of the Navigator class
        /// </summary>
        /// <param name="builder">a navigation builder instance that creates the navigation-raw collection</param>
        /// <param name="userProvider">the ambient context (user, services, route) to resolve against</param>
        /// <param name="services">the request services, used to resolve the optional change-signal</param>
        public Navigator(INavigationBuilder builder, IContextUserProvider userProvider, IServiceProvider services)
        {
            this.builder = builder;
            this.userProvider = userProvider;
            // Optional: only present when the EntityWriteTracker is active (ActivationSettings.UseEntityTracker).
            this.changeSignal = services.GetService<IEntityChangeSignal>();
        }

        /// <summary>
        /// Gets the Navigation-Collection for this site. The built tree is cached for the lifetime of the
        /// scope (per circuit / per request) but rebuilt when a navigation-relevant entity changed since it
        /// was built, so permission/role/menu changes take effect without a fresh circuit.
        /// </summary>
        public ICollection<NavigationMenu> SiteNavigation
        {
            get
            {
                if (rootObject == null || IsStale())
                {
                    builtAtUtc = DateTime.UtcNow;
                    rootObject = BuildRootObject();
                }

                return rootObject.Children;
            }
        }

        private bool IsStale()
            => changeSignal != null && changeSignal.GetLastChange(EntityChangeScope.Navigation) > builtAtUtc;

        /// <summary>
        /// Creates an cleans the root object for the site-navigation
        /// </summary>
        /// <returns>the root of the site-navigation</returns>
        private NavigationMenu BuildRootObject()
        {
            var currentPath = userProvider.RequestPath;
            var retVal = builder.GetNavigationRoot();
            // Host-neutral: the request-localization middleware (MVC) and the Blazor circuit both set
            // CultureInfo.CurrentUICulture, so we no longer reach into HttpContext.Features here.
            string currentCulture = CultureInfo.CurrentUICulture?.Name;

           retVal.CleanUp(currentPath, currentCulture);
            return retVal;
        }
    }
}
