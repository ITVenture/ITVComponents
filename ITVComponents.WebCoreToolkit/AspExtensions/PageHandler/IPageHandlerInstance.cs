using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.AspExtensions.PageHandler
{
    public interface IPageHandlerInstance<TPageModel>
    {
        /// <summary>
        /// gets a value indicating whether the handled page should actually return content or should return a 404-message
        /// </summary>
        bool UsePage { get; }
    }
}
