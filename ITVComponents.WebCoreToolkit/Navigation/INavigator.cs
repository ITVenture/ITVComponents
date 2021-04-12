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
    }
}
