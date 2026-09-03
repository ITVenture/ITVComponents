using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;

namespace ITVComponents.WebCoreToolkit.Globalization
{
    /// <summary>
    /// Hands the culture that <c>CulturePathMiddleware</c> read off the URL to
    /// <c>UseRequestLocalization()</c>. Registered as the FIRST provider, so a language in the URL beats
    /// the cookie and <c>Accept-Language</c>: it is the one the sender of a link chose explicitly.
    /// <para>
    /// A provider rather than a directly written <c>IRequestCultureFeature</c>, because the prefix has to
    /// be off the path before static files and routing see it, which is far earlier in the pipeline than
    /// localization runs. Splitting it this way keeps both halves in the position they belong in, and
    /// <c>ThreadCultureMiddleware</c> with its <c>CultureOptions</c> mapping stays untouched behind it.
    /// </para>
    /// </summary>
    public sealed class CulturePathRequestCultureProvider : RequestCultureProvider
    {
        /// <inheritdoc/>
        public override Task<ProviderCultureResult> DetermineProviderCultureResult(HttpContext httpContext)
        {
            var culture = httpContext?.Items[Global.CulturePathCultureItemKey] as string;
            return Task.FromResult(string.IsNullOrEmpty(culture)
                ? (ProviderCultureResult)null
                : new ProviderCultureResult(culture, culture));
        }
    }
}
