using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Models;

namespace ITVComponents.WebCoreToolkit.Navigation
{
    /// <summary>
    /// A navigation object that provides all required information for navigation
    /// </summary>
    public interface INavigator
    {
        /// <summary>
        /// Gets the Navigation-Collection for this site
        /// </summary>
        ICollection<NavigationMenu> SiteNavigation { get; }

        /// <summary>
        /// Gets the navigation entry that matches the current request path (the deepest active node), or null
        /// when the current page has no navigation entry. Its <see cref="NavigationMenu.MetadataValues"/> expose
        /// the per-entry metadata for the current page (e.g. a context-help slug).
        /// </summary>
        NavigationMenu SelectedNavigationItem { get; }
    }
}
