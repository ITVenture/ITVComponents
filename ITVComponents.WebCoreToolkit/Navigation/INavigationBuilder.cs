using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Models;

namespace ITVComponents.WebCoreToolkit.Navigation
{
    /// <summary>
    /// Builds the navigation dependent on a source
    /// </summary>
    public interface INavigationBuilder
    {
        /// <summary>
        /// Creates a Root-object that contains all navigation-entries for the site
        /// </summary>
        /// <returns></returns>
        NavigationMenu GetNavigationRoot();
    }
}
