using ITVComponents.EFRepo.DataSync;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Configuration
{
    /// <summary>
    /// System-config export section for the global help system: the topic tree with its localized contents, the
    /// resource library's folders and the resource entries. Cross-references travel by natural name (slug, resource
    /// name, folder reference tag) because the numeric ids differ per system.
    /// </summary>
    /// <remarks>
    /// The bytes behind a resource travel inline as base64 (see <see cref="HelpResourceFileMarkup.Content"/>),
    /// bounded by <see cref="HelpConfigExportOptions"/>. They are only available where the built-in EF blob store
    /// backs the resources; with a host-specific <c>IHelpResourceStore</c> the section carries metadata only and
    /// the diff reports what it could not create.
    /// </remarks>
    [SystemConfigHandler(SectionName, typeof(HelpConfigExtension))]
    public class HelpConfigMarkup : ConfigExtensionMarkup
    {
        /// <summary>Stable section key; equals the polymorphism discriminator.</summary>
        public const string SectionName = "help";

        /// <summary>The topic tree, parents before children.</summary>
        public HelpTopicMarkup[]? Topics { get; set; }

        /// <summary>Folders of the resource library, parents before children.</summary>
        public HelpResourceFolderMarkup[]? ResourceFolders { get; set; }

        /// <summary>The resource entries with their per-culture file bindings.</summary>
        public HelpResourceMarkup[]? Resources { get; set; }
    }

    /// <summary>One node of the help tree, identified by its globally unique <see cref="Slug"/>.</summary>
    public class HelpTopicMarkup
    {
        /// <summary>Stable, URL-friendly identifier; the key this section compares topics by.</summary>
        public string? Slug { get; set; }

        /// <summary>Slug of the parent topic; null for a root topic.</summary>
        public string? ParentSlug { get; set; }

        /// <summary>Container or content page.</summary>
        public HelpTopicKind Kind { get; set; }

        /// <summary>Ordering among siblings.</summary>
        public int SortOrder { get; set; }

        /// <summary>Optional icon name shown in the tree.</summary>
        public string? Icon { get; set; }

        /// <summary>Whether the topic is visible in the public viewer.</summary>
        public bool IsPublished { get; set; }

        /// <summary>Whether the topic is listed in the viewer's navigation.</summary>
        public bool ShowInMenu { get; set; }

        /// <summary>Localized title and Markdown body, one entry per culture.</summary>
        public HelpTopicContentMarkup[]? Contents { get; set; }
    }

    /// <summary>The localized payload of a topic.</summary>
    public class HelpTopicContentMarkup
    {
        /// <summary>BCP-47 culture or the literal <c>DEFAULT</c> fallback.</summary>
        public string? Culture { get; set; }

        /// <summary>Title shown in the tree.</summary>
        public string? Title { get; set; }

        /// <summary>Markdown body; null/empty for a container topic.</summary>
        public string? Body { get; set; }
    }

    /// <summary>
    /// A folder of the resource library, identified by its <see cref="RefTag"/>. Folders are pure organisation —
    /// a resource is addressed by its flat global name regardless of where it sits.
    /// </summary>
    /// <remarks>
    /// The tag and not the path is the key, so a folder that was renamed or moved on the exporting system
    /// arrives as a rename or a move. Over a path key the two would be indistinguishable from "a different
    /// folder", and the receiving system would end up with the new one beside the one it was meant to replace.
    /// </remarks>
    public class HelpResourceFolderMarkup
    {
        /// <summary>Stable identity of the folder; the key this section compares folders by.</summary>
        public string? RefTag { get; set; }

        /// <summary>The folder's own name, without its ancestors.</summary>
        public string? Name { get; set; }

        /// <summary>Reference tag of the parent folder; null for a folder at the root.</summary>
        public string? ParentRef { get; set; }

        /// <summary>
        /// Full path from the root, separated by <c>/</c>. For reading only — the diff shows it in place of the
        /// bare tag. Nothing is resolved through it.
        /// </summary>
        public string? Path { get; set; }
    }

    /// <summary>A resource entry, identified by its globally unique <see cref="Name"/>.</summary>
    public class HelpResourceMarkup
    {
        /// <summary>Unique reference name used from help content (<c>resource:{Name}</c>).</summary>
        public string? Name { get; set; }

        /// <summary>Free-text description shown in the administration.</summary>
        public string? Description { get; set; }

        /// <summary>Image, video or other.</summary>
        public HelpResourceKind Kind { get; set; }

        /// <summary>Reference tag of the folder the resource is filed in; null when it sits at the root.</summary>
        public string? FolderRef { get; set; }

        /// <summary>Full path of that folder, for reading only; the assignment travels through <see cref="FolderRef"/>.</summary>
        public string? FolderPath { get; set; }

        /// <summary>One binding per culture.</summary>
        public HelpResourceFileMarkup[]? Files { get; set; }
    }

    /// <summary>
    /// The culture-specific file binding of a resource, with its content when the export carries it.
    /// </summary>
    public class HelpResourceFileMarkup
    {
        /// <summary>BCP-47 culture or the literal <c>DEFAULT</c> fallback.</summary>
        public string? Culture { get; set; }

        /// <summary>MIME type of the stored file.</summary>
        public string? ContentType { get; set; }

        /// <summary>File name the resource was uploaded under.</summary>
        public string? OriginalName { get; set; }

        /// <summary>
        /// The storage handle on the system this markup was described from. It is <b>not</b> transferable: an
        /// import assigns a fresh identifier, because the handle is opaque and owned by each system's own resource
        /// store. Carried so the diff can address the local blob of an existing binding.
        /// </summary>
        public string? FileIdentifier { get; set; }

        /// <summary>
        /// SHA-256 of the content as lower-case hex — what decides whether a file changed, so an unchanged blob is
        /// not rewritten. Null when the content was not read (see <see cref="ContentOmittedReason"/>).
        /// </summary>
        public string? ContentHash { get; set; }

        /// <summary>Size of the stored content in bytes; 0 when unknown.</summary>
        public long ContentLength { get; set; }

        /// <summary>
        /// The stored bytes as base64, or null when the export does not carry them. This is the payload the apply
        /// engine decodes — the review dialog shows a summary in its place.
        /// </summary>
        public string? Content { get; set; }

        /// <summary>
        /// Why <see cref="Content"/> is absent (size limit, disabled, or content held in a host-specific store),
        /// so the diff can say what it could not do instead of failing quietly. Null when the content is present.
        /// </summary>
        public string? ContentOmittedReason { get; set; }
    }
}
