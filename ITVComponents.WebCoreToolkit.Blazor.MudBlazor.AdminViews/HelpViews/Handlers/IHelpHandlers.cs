using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.HelpViews.ViewModels;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.HelpViews.Handlers
{
    /// <summary>Admin surface for the help-topic tree. All operations are gated by <c>Help.Admin.Topics.*</c>.</summary>
    public interface IHelpAdminHandler
    {
        bool CanManage(ClaimsPrincipal user);

        bool CanWrite(ClaimsPrincipal user);

        /// <summary>Lists the direct child topics of <paramref name="parentId"/> (roots when null), one tree level.</summary>
        Task<HelpTopicNodeViewModel[]> ListChildrenAsync(ClaimsPrincipal admin, int? parentId, CancellationToken ct = default);

        /// <summary>Loads a topic with all its localized content rows for editing.</summary>
        Task<HelpTopicEditViewModel?> GetTopicAsync(ClaimsPrincipal admin, int helpTopicId, CancellationToken ct = default);

        /// <summary>Creates (Id == 0) or updates a topic and upserts its localized contents. Returns the id or null.</summary>
        Task<int?> SaveTopicAsync(ClaimsPrincipal admin, HelpTopicEditViewModel model, CancellationToken ct = default);

        /// <summary>Deletes a topic. Fails (false) when it still has children.</summary>
        Task<bool> DeleteTopicAsync(ClaimsPrincipal admin, int helpTopicId, CancellationToken ct = default);
    }

    /// <summary>Admin surface for media resources. Gated by <c>Help.Admin.Resources.*</c>.</summary>
    public interface IHelpResourceHandler
    {
        bool CanManage(ClaimsPrincipal user);

        bool CanWrite(ClaimsPrincipal user);

        Task<HelpResourceViewModel[]> ListResourcesAsync(ClaimsPrincipal admin, CancellationToken ct = default);

        Task<HelpResourceEditViewModel?> GetResourceAsync(ClaimsPrincipal admin, int helpResourceId, CancellationToken ct = default);

        /// <summary>Creates (Id == 0) or updates resource metadata (Name/Description/Kind). Returns the id or null.</summary>
        Task<int?> SaveResourceAsync(ClaimsPrincipal admin, HelpResourceEditViewModel model, CancellationToken ct = default);

        Task<bool> DeleteResourceAsync(ClaimsPrincipal admin, int helpResourceId, CancellationToken ct = default);

        /// <summary>
        /// Lists one level of the resource library: the folders and resources directly below
        /// <paramref name="folderId"/> (null = the root), folders first, each side by name.
        /// </summary>
        Task<HelpResourceNodeViewModel[]> ListNodesAsync(ClaimsPrincipal admin, int? folderId,
            CancellationToken ct = default);

        /// <summary>Creates (id == 0) or renames a folder. Returns an error message, or null on success.</summary>
        Task<string?> SaveFolderAsync(ClaimsPrincipal admin, int helpResourceFolderId, int? parentId, string name,
            CancellationToken ct = default);

        /// <summary>
        /// Deletes a folder - only when it is empty. Ein Ordner, der Inhalt mitnimmt, waere bei einer reinen
        /// Ordnungsstruktur der teuerste denkbare Fehlgriff.
        /// </summary>
        Task<string?> DeleteFolderAsync(ClaimsPrincipal admin, int helpResourceFolderId, CancellationToken ct = default);

        /// <summary>
        /// Moves a node (folder or resource) into a folder - <paramref name="targetKey"/> is a node key or
        /// <c>HelpResourceNodeKey.Root</c>.
        /// </summary>
        /// <returns>an error message, or null on success</returns>
        Task<string?> MoveNodeAsync(ClaimsPrincipal admin, string nodeKey, string targetKey,
            CancellationToken ct = default);

        /// <summary>
        /// Validates and stores an uploaded file for a resource+culture through <c>IHelpResourceStore</c> and
        /// upserts the <c>HelpResourceFile</c> row (replacing any prior file for that culture). Returns an error
        /// message on rejection, or null on success.
        /// </summary>
        Task<string?> SaveResourceFileAsync(ClaimsPrincipal admin, int helpResourceId, string culture, byte[] content,
            string? contentType, string? fileName, CancellationToken ct = default);

        Task<bool> DeleteResourceFileAsync(ClaimsPrincipal admin, int helpResourceId, string culture, CancellationToken ct = default);
    }

    /// <summary>Read surface for the public (anonymous) help viewer.</summary>
    public interface IHelpViewerHandler
    {
        /// <summary>The full published topic tree (nested), titles resolved for <paramref name="culture"/>.</summary>
        Task<HelpTreeNodeViewModel[]> GetPublishedTreeAsync(string? culture, CancellationToken ct = default);

        /// <summary>
        /// The published subtree ROOTED at the topic with <paramref name="slug"/> (that topic as the root, with
        /// its nested published children), titles resolved for <paramref name="culture"/>; null when no
        /// published topic has that slug. The context-help popup uses this to offer an in-place navigation tree
        /// when a slug points at a whole help area rather than a single page (root with no children = a plain page).
        /// </summary>
        Task<HelpTreeNodeViewModel?> GetPublishedSubtreeAsync(string slug, string? culture, CancellationToken ct = default);

        /// <summary>
        /// Resolves a published topic by slug and renders its localized body to HTML; null if not found.
        /// <paramref name="userAuthenticated"/> drives whether <c>module:</c> links render as (tenant-scoped)
        /// links or as plain title text.
        /// </summary>
        Task<HelpTopicViewViewModel?> GetPublishedTopicAsync(string slug, string? culture, bool userAuthenticated, CancellationToken ct = default);

        /// <summary>
        /// Lightweight check whether a published content topic exists for <paramref name="slug"/> (no rendering).
        /// Used to decide whether a context-help affordance should be shown at all.
        /// </summary>
        Task<bool> PublishedTopicExistsAsync(string slug, CancellationToken ct = default);
    }
}
