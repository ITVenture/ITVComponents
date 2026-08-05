using Microsoft.AspNetCore.Components;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents
{
    /// <summary>
    /// One entry of a <c>ToolbarActionMenu</c>. Carries its own permission gate so a menu built from a list stays
    /// as fine-grained as the row of individual <see cref="SecureView"/>-wrapped buttons it replaces.
    /// </summary>
    public class ToolbarAction
    {
        /// <summary>The entry's caption. In a menu it may be as long as it needs to be — that is the point.</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>Optional icon (an <c>Icons.Material…</c> constant).</summary>
        public string? Icon { get; set; }

        /// <summary>
        /// Comma-separated permissions, same syntax and ANY-semantics as <see cref="SecureView.RequiredPermissions"/>.
        /// Empty or null means "no restriction".
        /// </summary>
        public string? RequiredPermissions { get; set; }

        /// <summary>When true, the user must hold ALL listed permissions; when false (default) ANY one suffices.</summary>
        public bool RequireAllPermissions { get; set; }

        /// <summary>Greys the entry out without hiding it (unlike a missing permission, which removes it).</summary>
        public bool Disabled { get; set; }

        /// <summary>Invoked when the entry is chosen.</summary>
        public EventCallback OnClick { get; set; }
    }
}
