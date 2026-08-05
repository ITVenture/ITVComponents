using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.AspExtensions.PageHandler
{
    public interface IPageHandlerProvider<TPageModel, THandlerInterface>
        where THandlerInterface : IPageHandlerInstance<TPageModel>
    {
        /// <summary>
        /// The handler for the ambient scope, created once and kept. Correct wherever that scope is the
        /// operation - which under MVC it is, because the scope is the request. Under Blazor it is the circuit;
        /// use <see cref="BeginOperation"/> there instead.
        /// </summary>
        public THandlerInterface Handler { get; }

        /// <summary>
        /// Opens a handler for a single operation in a scope of its own, and disposes both when the lease ends.
        /// Use wherever the ambient scope outlives one operation - on a Blazor circuit that scope lives as long
        /// as the connection, and the handler's UserManager and DbContext would live with it.
        /// </summary>
        /// <returns>the lease; dispose it when the operation is done</returns>
        IPageHandlerLease<THandlerInterface> BeginOperation();
    }
}
