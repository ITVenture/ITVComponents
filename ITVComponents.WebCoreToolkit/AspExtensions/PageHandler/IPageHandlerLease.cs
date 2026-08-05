using System;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.AspExtensions.PageHandler
{
    /// <summary>
    /// A handler bound to one operation. Owns the scope its dependencies were resolved from and disposes it -
    /// together with the handler itself - when the operation ends.
    ///
    /// Needed wherever the ambient scope outlives a single operation. Under MVC it does not: the request scope
    /// IS the operation, and <see cref="IPageHandlerProvider{TPageModel,THandlerInterface}.Handler"/> is the
    /// right choice there. Under Blazor the scope is the circuit and lives as long as the connection - a
    /// handler kept for that long keeps its UserManager, and with it a DbContext, alive across every operation
    /// the user performs. That yields overlapping operations on one context and a first-level cache that never
    /// refreshes.
    /// </summary>
    /// <typeparam name="THandlerInterface">the HandlerInterface for a page</typeparam>
    public interface IPageHandlerLease<out THandlerInterface> : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// The handler for this operation. Do not keep it - and do not carry anything it handed out, such as a
        /// UserQueryTicket, into another lease: the ticket only resolves against the handler that issued it.
        /// </summary>
        THandlerInterface Handler { get; }
    }
}
