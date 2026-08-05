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
        /// Creates a PageHandler that implements the given interface, resolving its dependencies from the
        /// current HttpContext. Only usable while a request is in flight - prefer the overload taking an
        /// explicit scope.
        /// </summary>
        /// <typeparam name="THandlerInterface">the HandlerInterface for a page</typeparam>
        /// <returns>the demanded Handler instance</returns>
        THandlerInterface CreateHandler<TPageModel, THandlerInterface>()
            where THandlerInterface : IPageHandlerInstance<TPageModel>;

        /// <summary>
        /// Creates a PageHandler that implements the given interface, resolving its dependencies from the
        /// given scope. Required outside a request - a Blazor circuit has no HttpContext, and handlers
        /// usually depend on scoped services such as UserManager or a DbContext.
        /// </summary>
        /// <typeparam name="THandlerInterface">the HandlerInterface for a page</typeparam>
        /// <param name="scopeServices">the scope to resolve the handler's dependencies from</param>
        /// <returns>the demanded Handler instance</returns>
        THandlerInterface CreateHandler<TPageModel, THandlerInterface>(IServiceProvider scopeServices)
            where THandlerInterface : IPageHandlerInstance<TPageModel>;
    }
}
