using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Caching;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Routing;
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
        private readonly IAppLink appLink;
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
            // Answers "which module is showing" in the same terms the menu entries are stored in, so the
            // comparison stays immune to whatever prefixes the URL happens to carry. Optional for the same
            // reason the builder's is: a host that never registered one keeps the old, path-based answer.
            this.appLink = services.GetService<IAppLink>();
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
                EnsureBuilt();
                return rootObject.Children;
            }
        }

        /// <summary>
        /// Gets the navigation entry whose Url matches the current request path, or null when the current page
        /// has no navigation entry. Resolved live against the current path on every access (the menu tree itself
        /// stays cached), so it stays correct across client-side navigations within the same circuit.
        /// </summary>
        public NavigationMenu SelectedNavigationItem
        {
            get
            {
                EnsureBuilt();
                return FindByPath(rootObject.Children, CurrentModuleUrl);
            }
        }

        /// <summary>
        /// The module url of the page showing - prefix-free, so it can be compared to a stored menu url.
        /// Falls back to the raw request path where no <see cref="IAppLink"/> is registered, which is what
        /// this used to be unconditionally: correct only as long as no prefix (culture, asset, tenant) stood
        /// in front of the path.
        /// </summary>
        private string CurrentModuleUrl => appLink?.CurrentModuleUrl ?? userProvider.RequestPath;

        private void EnsureBuilt()
        {
            if (rootObject == null || IsStale())
            {
                builtAtUtc = DateTime.UtcNow;
                rootObject = BuildRootObject();
            }
        }

        private bool IsStale()
            => changeSignal != null && changeSignal.GetLastChange(EntityChangeTopics.Navigation) > builtAtUtc;

        /// <summary>
        /// Finds the deepest navigation node whose Url equals <paramref name="path"/> (case-insensitive, tolerant
        /// of a trailing slash). Returns null when the path has no navigation entry.
        /// </summary>
        private static NavigationMenu FindByPath(IEnumerable<NavigationMenu> nodes, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            foreach (var node in nodes)
            {
                var childMatch = FindByPath(node.Children, path);
                if (childMatch != null)
                {
                    return childMatch;
                }

                var nodePath = string.IsNullOrEmpty(node.ModuleUrl) ? node.Url : node.ModuleUrl;
                if (!string.IsNullOrEmpty(nodePath) &&
                    string.Equals(nodePath.TrimEnd('/'), path.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                {
                    return node;
                }
            }

            return null;
        }

        /// <summary>
        /// Creates an cleans the root object for the site-navigation
        /// </summary>
        /// <returns>the root of the site-navigation</returns>
        private NavigationMenu BuildRootObject()
        {
            var currentPath = CurrentModuleUrl;
            var retVal = builder.GetNavigationRoot();
            // Host-neutral: the request-localization middleware (MVC) and the Blazor circuit both set
            // CultureInfo.CurrentUICulture, so we no longer reach into HttpContext.Features here.
            string currentCulture = CultureInfo.CurrentUICulture?.Name;

           retVal.CleanUp(currentPath, currentCulture);
            return retVal;
        }
    }
}
