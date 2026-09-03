using System.Collections.Generic;
using ITVComponents.WebCoreToolkit.Globalization;

namespace ITVComponents.WebCoreToolkit.Options
{
    /// <summary>
    /// Configures the culture prefix <c>/c/{culture}/…</c> that <c>UseCulturePath()</c> reads off the
    /// beginning of a request path.
    /// </summary>
    public class CulturePathOptions
    {
        /// <summary>
        /// Gets or sets the name of the leading segment that introduces the culture. Defaults to
        /// <c>c</c>. Whatever it is set to must not be used as a tenant name that has a top-level page
        /// named like a culture - see <see cref="CulturePath"/> for why that is the only collision left.
        /// </summary>
        public string SegmentName { get; set; } = CulturePath.DefaultSegmentName;

        /// <summary>
        /// Gets the cultures the prefix may select. Leave it empty - the normal case - and the supported
        /// UI cultures of <c>RequestLocalizationOptions</c> are used, so there is one list instead of two
        /// that can disagree. Fill it only to allow a set in the URL that differs from the one the other
        /// culture providers may deliver.
        /// </summary>
        public IList<string> SupportedCultures { get; } = new List<string>();
    }
}
