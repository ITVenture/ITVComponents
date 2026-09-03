using System.Collections.Generic;
using System.Globalization;

namespace ITVComponents.WebCoreToolkit.Blazor.Localization
{
    /// <summary>
    /// Everything a language picker needs: which languages there are, which one is showing, and the two
    /// ways to switch to another one.
    /// <para>
    /// It exists because a language switch under <c>UseCulturePath()</c> is not an ordinary navigation and
    /// gets three things wrong when it is written by hand: the prefix has to be <b>replaced</b> rather than
    /// prepended (or the second switch produces <c>/c/de/c/fr/…</c>), query and fragment have to survive,
    /// and the navigation has to be a <b>full page load</b> - the <c>&lt;base href&gt;</c> changes, and the
    /// new address lies outside the base-URI space the current circuit lives in.
    /// </para>
    /// </summary>
    public interface ICultureSwitcher
    {
        /// <summary>
        /// The languages that may be offered - the supported UI cultures of the request localization, in
        /// the order they were configured.
        /// </summary>
        IReadOnlyList<CultureInfo> AvailableCultures { get; }

        /// <summary>
        /// The language the current page is showing. Taken from the URL prefix when there is one, so that
        /// what the picker marks as selected is what the address bar says; otherwise the culture the
        /// request localization settled on.
        /// </summary>
        string CurrentCulture { get; }

        /// <summary>
        /// The address of the current page in another language - for a picker made of real
        /// <c>&lt;a href&gt;</c> elements, which is worth preferring: it survives without scripting and can
        /// be opened in a new tab. Root-absolute on purpose, and the one place where that is correct:
        /// leaving the base-URI space is exactly the point.
        /// </summary>
        /// <param name="culture">the culture to switch to, or null/empty to drop the prefix</param>
        /// <returns>the target address, with query and fragment preserved</returns>
        string BuildUrlFor(string culture);

        /// <summary>
        /// Switches the current page to the given language with a full page load.
        /// </summary>
        /// <param name="culture">the culture to switch to, or null/empty to drop the prefix</param>
        void SwitchTo(string culture);
    }
}
