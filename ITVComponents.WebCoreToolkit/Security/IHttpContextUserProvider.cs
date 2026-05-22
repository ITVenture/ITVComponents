using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.Security
{
    /// <summary>
    /// MVC/HTTP-specialization of <see cref="IContextUserProvider"/> that additionally exposes the raw
    /// <see cref="Microsoft.AspNetCore.Http.HttpContext"/>. Only consumers tied to the live HTTP request
    /// (querystring/referer-driven asset impersonation, request features) depend on this; everything else
    /// uses the neutral <see cref="IContextUserProvider"/> so it stays consumable from a Blazor circuit.
    /// </summary>
    public interface IHttpContextUserProvider : IContextUserProvider
    {
        /// <summary>
        /// Gets the current HttpContext. Null when there is no live HTTP request (e.g. a Blazor circuit).
        /// </summary>
        HttpContext HttpContext { get; }
    }
}
