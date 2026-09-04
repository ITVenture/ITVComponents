using ITVComponents.WebCoreToolkit.Models;

namespace ITVComponents.WebCoreToolkit.Routing
{
    /// <summary>
    /// Turns a raw module url - the value as it is stored on a navigation entry, e.g. <c>Workflow/Tasks</c> -
    /// into a link the CURRENT host resolves correctly, and answers the same question backwards for the page
    /// that is showing.
    /// <para>
    /// It exists because "correctly" has two different answers, and the difference is not a matter of taste:
    /// </para>
    /// <list type="bullet">
    ///   <item><description><b>A host that emits a <c>&lt;base href&gt;</c></b> (Blazor, via
    ///   <c>TenantBaseHref</c>) already carries every prefix there is - culture, shared asset, tenant. A link
    ///   that is RELATIVE inherits all of them without knowing a single one, stays inside the base-URI space
    ///   and is therefore intercepted by Blazor as an in-circuit navigation. A root-absolute link is the
    ///   opposite of that: it leaves the base-URI space, the click never reaches the circuit, the browser
    ///   performs an ordinary document load and every prefix that lived in <c>PathBase</c> is gone. That is
    ///   how a fixed language used to get lost on every menu click while it survived a button.</description></item>
    ///   <item><description><b>A host without a base href</b> (classic MVC/Razor Pages) resolves a relative
    ///   href against the CURRENT PAGE, which is not what anyone means. There the link has to be
    ///   root-absolute and carry the full prefix itself.</description></item>
    /// </list>
    /// <para>
    /// Whoever builds a link out of stored data - the navigation builder above all - asks this service
    /// instead of concatenating prefixes, so a prefix that is added later (the culture segment was the third)
    /// does not have to be threaded through every call site once again.
    /// </para>
    /// <para>
    /// Not needed for links the framework builds: an MVC <c>Url.Action</c>/tag helper and a Blazor
    /// <c>NavigateTo</c> with a relative target already do the right thing. See <see cref="IUrlFormat"/> for
    /// the placeholder-based variant used in configured urls.
    /// </para>
    /// </summary>
    public interface IAppLink
    {
        /// <summary>
        /// Builds the link for a raw module url. The result is relative (no leading slash) on a host that
        /// resolves against a base href, and root-absolute with the full prefix everywhere else. An empty or
        /// null input yields an empty string - a navigation entry without a url is a pure grouping node.
        /// </summary>
        /// <param name="moduleUrl">the stored url, with or without a leading slash</param>
        /// <returns>the link to render</returns>
        string Resolve(string moduleUrl);

        /// <summary>
        /// The module url of the page currently showing - the same form <see cref="Resolve"/> takes as its
        /// input, i.e. with every prefix (culture, asset, tenant) removed and a single leading slash. This is
        /// the value to compare a <see cref="NavigationMenu.ModuleUrl"/> against; comparing rendered links
        /// instead is what breaks the moment a prefix appears on one side only.
        /// </summary>
        string CurrentModuleUrl { get; }

        /// <summary>
        /// The culture prefix of the current context (e.g. <c>/c/de-CH</c>), exactly as it stands in the URL,
        /// or an empty string when no language is pinned. Only a root-absolute link needs it - anything
        /// resolved against the base href inherits it.
        /// </summary>
        string CulturePrefix { get; }
    }
}
