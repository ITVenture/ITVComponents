using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;

namespace ITVComponents.WebCoreToolkit.AspExtensions.Factories
{
    public interface IPageModelFactory
    {
        /// <summary>
        /// Creates a PageHandler that implements the given interface
        /// </summary>
        /// <typeparam name="THandlerInterface">the HandlerInterface for a page</typeparam>
        /// <returns>the demanded Handler instance</returns>
        THandlerInterface CreateHandler<TPageModel, THandlerInterface>()
            where THandlerInterface : IPageHandlerInstance<TPageModel>;
    }
}
