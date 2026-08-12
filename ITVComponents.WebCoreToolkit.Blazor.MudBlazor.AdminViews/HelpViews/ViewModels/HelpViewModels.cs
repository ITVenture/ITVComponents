using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Models;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.HelpViews.ViewModels
{
    /// <summary>One node in the admin help tree (a single level; children are fetched lazily on expand).</summary>
    public class HelpTopicNodeViewModel
    {
        public int HelpTopicId { get; set; }

        public int? ParentId { get; set; }

        public HelpTopicKind Kind { get; set; }

        public string Slug { get; set; } = string.Empty;

        public string? Icon { get; set; }

        public bool IsPublished { get; set; }

        /// <summary>Whether the topic (and its subtree) is listed in the viewer's navigation.</summary>
        public bool ShowInMenu { get; set; } = true;

        public int SortOrder { get; set; }

        /// <summary>Effective title in the caller's culture (falls back to the slug when no content exists).</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Number of child topics — drives the expand affordance.</summary>
        public int ChildCount { get; set; }
    }

    /// <summary>A localized content row of a topic (title, and Markdown body for content pages).</summary>
    public class HelpTopicContentViewModel
    {
        [Required, MaxLength(35)]
        public string Culture { get; set; } = string.Empty;

        [Required, MaxLength(400)]
        public string Title { get; set; } = string.Empty;

        public string? Body { get; set; }
    }

    /// <summary>The full editable topic (metadata + all localized contents).</summary>
    public class HelpTopicEditViewModel
    {
        public int HelpTopicId { get; set; }

        public int? ParentId { get; set; }

        public HelpTopicKind Kind { get; set; } = HelpTopicKind.ContentPage;

        [Required, MaxLength(200)]
        public string Slug { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Icon { get; set; }

        public bool IsPublished { get; set; }

        /// <summary>
        /// Whether the topic (and its subtree) is listed in the viewer's navigation. Clearing it does not make
        /// the topic private — it stays reachable through its slug, which is how linked documents such as terms
        /// of service are meant to work.
        /// </summary>
        public bool ShowInMenu { get; set; } = true;

        public int SortOrder { get; set; }

        public List<HelpTopicContentViewModel> Contents { get; set; } = new();
    }

    /// <summary>Admin list row for a media resource.</summary>
    public class HelpResourceViewModel
    {
        public int HelpResourceId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public HelpResourceKind Kind { get; set; }

        public int FileCount { get; set; }
    }

    /// <summary>
    /// One row of the resource library: either a folder or a resource. Beide teilen sich die Ansicht,
    /// deshalb teilen sie sich auch die Zeile.
    /// </summary>
    public class HelpResourceNodeViewModel
    {
        /// <summary>true = Ordner, false = Ressource.</summary>
        public bool IsFolder { get; set; }

        /// <summary>Die Kennung innerhalb ihrer Art (Ordner-Id bzw. Ressourcen-Id).</summary>
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>Nur bei Ressourcen belegt.</summary>
        public HelpResourceKind Kind { get; set; }

        /// <summary>Nur bei Ressourcen: wie viele Sprachdateien haengen daran.</summary>
        public int FileCount { get; set; }

        /// <summary>Nur bei Ordnern: wie viele Unterordner und Ressourcen liegen darin.</summary>
        public int ChildCount { get; set; }

        /// <summary>
        /// Die Kennung fuer das Ziehen. Ordner und Ressourcen haben je eigene Zaehler, ihre Ids kollidieren
        /// also - deshalb traegt die Kennung die Art mit ("f:12" / "r:34").
        /// </summary>
        public string DragKey => HelpResourceNodeKey.For(IsFolder, Id);
    }

    /// <summary>
    /// Wandelt die Kennung einer Zeile in Art und Id - und zurueck.
    /// </summary>
    /// <remarks>
    /// Eine eigene kleine Klasse, weil beide Seiten dasselbe verstehen muessen: das Markup schreibt die
    /// Kennung, der Handler liest sie nach dem Ablegen wieder aus.
    /// </remarks>
    public static class HelpResourceNodeKey
    {
        /// <summary>Die Kennung der Wurzel - dorthin zieht man, was aus jedem Ordner heraus soll.</summary>
        public const string Root = "root";

        public static string For(bool isFolder, int id) => (isFolder ? "f:" : "r:") + id.ToString();

        /// <summary>Liest eine Kennung. Liefert false, wenn sie nicht zu deuten ist.</summary>
        public static bool TryParse(string? key, out bool isFolder, out int id)
        {
            isFolder = false;
            id = 0;
            if (string.IsNullOrEmpty(key) || key.Length < 3)
            {
                return false;
            }

            isFolder = key[0] == 'f';
            return (key[0] == 'f' || key[0] == 'r') && key[1] == ':'
                   && int.TryParse(key.Substring(2), out id);
        }
    }

    /// <summary>A localized file binding of a resource (metadata only; bytes live in the store).</summary>
    public class HelpResourceFileViewModel
    {
        [Required, MaxLength(35)]
        public string Culture { get; set; } = string.Empty;

        public string? OriginalName { get; set; }

        public string? ContentType { get; set; }
    }

    /// <summary>The full editable resource (metadata + its per-culture files).</summary>
    public class HelpResourceEditViewModel
    {
        public int HelpResourceId { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1024)]
        public string? Description { get; set; }

        public HelpResourceKind Kind { get; set; } = HelpResourceKind.Image;

        public List<HelpResourceFileViewModel> Files { get; set; } = new();
    }

    /// <summary>A node of the public viewer's topic tree (nested; only published topics).</summary>
    public class HelpTreeNodeViewModel
    {
        public int HelpTopicId { get; set; }

        public string Slug { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string? Icon { get; set; }

        public HelpTopicKind Kind { get; set; }

        public List<HelpTreeNodeViewModel> Children { get; set; } = new();
    }

    /// <summary>A rendered help page for the public viewer (title + sanitized HTML).</summary>
    public class HelpTopicViewViewModel
    {
        public int HelpTopicId { get; set; }

        public string Slug { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public HelpTopicKind Kind { get; set; }

        /// <summary>
        /// Whether the topic is listed in the viewer's navigation. Ein Thema, das nicht ins Menue gehoert
        /// (z.B. ein verlinktes Vertragsdokument), wird auch OHNE die Navigation angezeigt - die waere dort
        /// nur Ballast.
        /// </summary>
        public bool ShowInMenu { get; set; } = true;

        /// <summary>Rendered HTML of the resolved localized Markdown body (empty for containers).</summary>
        public string Html { get; set; } = string.Empty;
    }
}
