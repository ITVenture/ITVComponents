using System.Collections.Generic;
using System.Linq;

namespace ITVComponents.WebCoreToolkit.Blazor.Resources
{
    /// <summary>
    /// A single client-side script reference to be emitted by <c>ITVentureReferences</c>.
    /// </summary>
    public sealed record ClientScript(string Src, bool Module = false, bool Defer = false, bool Async = false, string? Integrity = null, string? CrossOrigin = null);

    /// <summary>
    /// A single client-side stylesheet reference to be emitted by <c>ITVentureReferences</c>.
    /// </summary>
    public sealed record ClientStyleSheet(string Href, string? Integrity = null, string? CrossOrigin = null);

    /// <summary>
    /// Aggregates every client-side script and stylesheet the WebCoreToolkit-Blazor libraries need.
    /// Each library registers its own assets at startup (typically from its <c>WebPartInit</c>) via
    /// <c>services.AddToolkitClientScript(...)</c> / <c>AddToolkitClientStyleSheet(...)</c>; the host
    /// emits the union with a single <c>&lt;ITVentureReferences /&gt;</c>. Registration order is
    /// preserved and duplicate URLs are ignored, so libraries can declare shared dependencies safely.
    /// </summary>
    public class ClientResourceOptions
    {
        public List<ClientScript> Scripts { get; } = new();

        public List<ClientStyleSheet> StyleSheets { get; } = new();

        public void AddScript(ClientScript script)
        {
            if (!Scripts.Any(s => s.Src == script.Src))
            {
                Scripts.Add(script);
            }
        }

        public void AddScript(string src, bool module = false, bool defer = false, bool async = false)
            => AddScript(new ClientScript(src, module, defer, async));

        public void AddStyleSheet(ClientStyleSheet styleSheet)
        {
            if (!StyleSheets.Any(s => s.Href == styleSheet.Href))
            {
                StyleSheets.Add(styleSheet);
            }
        }

        public void AddStyleSheet(string href) => AddStyleSheet(new ClientStyleSheet(href));
    }
}
