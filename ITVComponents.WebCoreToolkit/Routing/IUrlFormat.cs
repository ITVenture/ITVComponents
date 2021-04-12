using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Routing
{
    public interface IUrlFormat
    {
        /// <summary>
        /// Formats a url using the given url-prototype. 
        /// </summary>
        /// <param name="url">the route prototype for a route that needs to be formatted</param>
        /// <returns>the formatted url.</returns>
        string FormatUrl(string url);
    }
}
