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

        /// <summary>Rendered HTML of the resolved localized Markdown body (empty for containers).</summary>
        public string Html { get; set; } = string.Empty;
    }
}
