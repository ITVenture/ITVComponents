using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.HelpViews.ViewModels;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Options;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.HelpViews.Handlers.Impl
{
    /// <summary>
    /// Default <see cref="IHelpResourceHandler"/>. Resource metadata lives in the help context; the bytes go
    /// through the plain <see cref="IHelpResourceStore"/> (not the plugin file-handler), so the same store also
    /// serves the anonymous viewer. Gated by <c>Help.Admin.Resources.*</c>.
    /// </summary>
    public class HelpResourceHandler<TContext> : IHelpResourceHandler
        where TContext : DbContext, IHelpSystemContext
    {
        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly IServiceProvider services;
        private readonly IHelpResourceStore store;
        private readonly IGlobalSettings<HelpSystemOptions> options;

        public HelpResourceHandler(IDbContextFactory<TContext> dbFactory, IServiceProvider services,
            IHelpResourceStore store, IGlobalSettings<HelpSystemOptions> options)
        {
            this.dbFactory = dbFactory;
            this.services = services;
            this.store = store;
            this.options = options;
        }

        public bool CanManage(ClaimsPrincipal user) => services.VerifyUserPermissions(HelpPermissions.ResourcesRead);

        public bool CanWrite(ClaimsPrincipal user) => services.VerifyUserPermissions(HelpPermissions.ResourcesWriteAny);

        public async Task<HelpResourceViewModel[]> ListResourcesAsync(ClaimsPrincipal admin, CancellationToken ct = default)
        {
            if (!services.VerifyUserPermissions(HelpPermissions.ResourcesRead))
            {
                return Array.Empty<HelpResourceViewModel>();
            }

            await using var db = await dbFactory.CreateDbContextAsync(ct);
            return await db.HelpResources.AsNoTracking()
                .OrderBy(r => r.Name)
                .Select(r => new HelpResourceViewModel
                {
                    HelpResourceId = r.HelpResourceId,
                    Name = r.Name,
                    Description = r.Description,
                    Kind = r.Kind,
                    FileCount = r.Files.Count
                })
                .ToArrayAsync(ct);
        }

        public async Task<HelpResourceEditViewModel?> GetResourceAsync(ClaimsPrincipal admin, int helpResourceId, CancellationToken ct = default)
        {
            if (!services.VerifyUserPermissions(HelpPermissions.ResourcesRead))
            {
                return null;
            }

            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var resource = await db.HelpResources.AsNoTracking().Include(r => r.Files)
                .FirstOrDefaultAsync(r => r.HelpResourceId == helpResourceId, ct);
            if (resource == null)
            {
                return null;
            }

            return new HelpResourceEditViewModel
            {
                HelpResourceId = resource.HelpResourceId,
                Name = resource.Name,
                Description = resource.Description,
                Kind = resource.Kind,
                Files = resource.Files
                    .OrderBy(f => f.Culture)
                    .Select(f => new HelpResourceFileViewModel { Culture = f.Culture, OriginalName = f.OriginalName, ContentType = f.ContentType })
                    .ToList()
            };
        }

        public async Task<int?> SaveResourceAsync(ClaimsPrincipal admin, HelpResourceEditViewModel model, CancellationToken ct = default)
        {
            if (!services.VerifyUserPermissions(HelpPermissions.ResourcesWriteAny) || string.IsNullOrWhiteSpace(model.Name))
            {
                return null;
            }

            var name = model.Name.Trim();
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            if (await db.HelpResources.AnyAsync(r => r.Name == name && r.HelpResourceId != model.HelpResourceId, ct))
            {
                return null;
            }

            HelpResource resource;
            if (model.HelpResourceId != 0)
            {
                resource = await db.HelpResources.FirstOrDefaultAsync(r => r.HelpResourceId == model.HelpResourceId, ct);
                if (resource == null)
                {
                    return null;
                }
            }
            else
            {
                resource = new HelpResource();
                db.HelpResources.Add(resource);
            }

            resource.Name = name;
            resource.Description = model.Description;
            resource.Kind = model.Kind;
            await db.SaveChangesAsync(ct);
            return resource.HelpResourceId;
        }

        public async Task<bool> DeleteResourceAsync(ClaimsPrincipal admin, int helpResourceId, CancellationToken ct = default)
        {
            if (!services.VerifyUserPermissions(HelpPermissions.ResourcesWriteAny))
            {
                return false;
            }

            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var resource = await db.HelpResources.Include(r => r.Files)
                .FirstOrDefaultAsync(r => r.HelpResourceId == helpResourceId, ct);
            if (resource == null)
            {
                return false;
            }

            // Drop the backing bytes first (best-effort), then the metadata rows (files cascade with the resource).
            foreach (var file in resource.Files)
            {
                await store.DeleteAsync(file.FileIdentifier, ct);
            }

            db.HelpResources.Remove(resource);
            await db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<HelpResourceNodeViewModel[]> ListNodesAsync(ClaimsPrincipal admin, int? folderId,
            CancellationToken ct = default)
        {
            if (!CanManage(admin))
            {
                return Array.Empty<HelpResourceNodeViewModel>();
            }

            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var folders = await db.HelpResourceFolders.AsNoTracking()
                .Where(f => f.ParentId == folderId)
                .OrderBy(f => f.Name)
                .Select(f => new HelpResourceNodeViewModel
                {
                    IsFolder = true,
                    Id = f.HelpResourceFolderId,
                    Name = f.Name,
                    // Beides zaehlt, weil beides den Ordner am Loeschen hindert - und weil der Pfeil zum
                    // Aufklappen sonst an einem Ordner haengt, unter dem nichts kommt.
                    ChildCount = f.Children.Count + f.Resources.Count
                }).ToArrayAsync(ct);

            var resources = await db.HelpResources.AsNoTracking()
                .Where(r => r.FolderId == folderId)
                .OrderBy(r => r.Name)
                .Select(r => new HelpResourceNodeViewModel
                {
                    IsFolder = false,
                    Id = r.HelpResourceId,
                    Name = r.Name,
                    Description = r.Description,
                    Kind = r.Kind,
                    FileCount = r.Files.Count
                }).ToArrayAsync(ct);

            // Ordner zuerst: die Struktur soll man sehen, bevor man den Inhalt liest.
            return folders.Concat(resources).ToArray();
        }

        public async Task<string?> SaveFolderAsync(ClaimsPrincipal admin, int helpResourceFolderId, int? parentId,
            string name, CancellationToken ct = default)
        {
            if (!services.VerifyUserPermissions(HelpPermissions.ResourcesWriteAny))
            {
                return "Not authorized.";
            }

            name = name?.Trim() ?? string.Empty;
            if (name.Length == 0)
            {
                return "The folder needs a name.";
            }

            await using var db = await dbFactory.CreateDbContextAsync(ct);

            // Eindeutigkeit im Handler und nicht ueber einen Index: der muesste (ParentId, Name) umfassen,
            // und ueber eine NULL-Spalte verhalten sich SQL Server und PostgreSQL dabei verschieden.
            if (await db.HelpResourceFolders.AnyAsync(
                    f => f.ParentId == parentId && f.Name == name && f.HelpResourceFolderId != helpResourceFolderId, ct))
            {
                return $"A folder named '{name}' already exists here.";
            }

            if (helpResourceFolderId == 0)
            {
                db.HelpResourceFolders.Add(new HelpResourceFolder { ParentId = parentId, Name = name });
            }
            else
            {
                var folder = await db.HelpResourceFolders
                    .FirstOrDefaultAsync(f => f.HelpResourceFolderId == helpResourceFolderId, ct);
                if (folder == null)
                {
                    return "The folder no longer exists.";
                }

                folder.Name = name;
            }

            await db.SaveChangesAsync(ct);
            return null;
        }

        public async Task<string?> DeleteFolderAsync(ClaimsPrincipal admin, int helpResourceFolderId,
            CancellationToken ct = default)
        {
            if (!services.VerifyUserPermissions(HelpPermissions.ResourcesWriteAny))
            {
                return "Not authorized.";
            }

            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var folder = await db.HelpResourceFolders
                .FirstOrDefaultAsync(f => f.HelpResourceFolderId == helpResourceFolderId, ct);
            if (folder == null)
            {
                return "The folder no longer exists.";
            }

            // Nur leere Ordner: eine Ordnungsstruktur, die beim Loeschen Inhalte mitnimmt, ist der
            // teuerste denkbare Fehlgriff - der Weg zurueck fuehrt dann ueber die Sicherung.
            int folders = await db.HelpResourceFolders.CountAsync(f => f.ParentId == helpResourceFolderId, ct);
            int resources = await db.HelpResources.CountAsync(r => r.FolderId == helpResourceFolderId, ct);
            if (folders + resources != 0)
            {
                return $"'{folder.Name}' is not empty ({folders} folder(s), {resources} resource(s)). "
                       + "Move its contents elsewhere first.";
            }

            db.HelpResourceFolders.Remove(folder);
            await db.SaveChangesAsync(ct);
            return null;
        }

        public async Task<string?> MoveNodeAsync(ClaimsPrincipal admin, string nodeKey, string targetKey,
            CancellationToken ct = default)
        {
            if (!services.VerifyUserPermissions(HelpPermissions.ResourcesWriteAny))
            {
                return "Not authorized.";
            }

            if (!HelpResourceNodeKey.TryParse(nodeKey, out bool nodeIsFolder, out int nodeId))
            {
                return "The dragged item could not be identified.";
            }

            // Das Ziel ist entweder die Wurzel oder ein ORDNER - in eine Ressource kann nichts hinein.
            int? targetFolderId = null;
            if (!string.Equals(targetKey, HelpResourceNodeKey.Root, StringComparison.Ordinal))
            {
                if (!HelpResourceNodeKey.TryParse(targetKey, out bool targetIsFolder, out int targetId)
                    || !targetIsFolder)
                {
                    return "A resource cannot hold other items - drop onto a folder or onto the root.";
                }

                targetFolderId = targetId;
            }

            await using var db = await dbFactory.CreateDbContextAsync(ct);

            if (targetFolderId.HasValue
                && !await db.HelpResourceFolders.AnyAsync(f => f.HelpResourceFolderId == targetFolderId.Value, ct))
            {
                return "The target folder no longer exists.";
            }

            if (!nodeIsFolder)
            {
                var resource = await db.HelpResources.FirstOrDefaultAsync(r => r.HelpResourceId == nodeId, ct);
                if (resource == null)
                {
                    return "The resource no longer exists.";
                }

                resource.FolderId = targetFolderId;
                await db.SaveChangesAsync(ct);
                return null;
            }

            var moved = await db.HelpResourceFolders.FirstOrDefaultAsync(f => f.HelpResourceFolderId == nodeId, ct);
            if (moved == null)
            {
                return "The folder no longer exists.";
            }

            if (targetFolderId == nodeId)
            {
                return "A folder cannot be moved into itself.";
            }

            // Ein Ordner darf nicht unter sich selbst wandern - sonst haengt sein Teilbaum an nichts mehr
            // und ist in der Ansicht nicht wiederzufinden.
            if (targetFolderId.HasValue && await IsDescendantOrSelfAsync(db, targetFolderId.Value, nodeId, ct))
            {
                return "A folder cannot be moved into one of its own subfolders.";
            }

            moved.ParentId = targetFolderId;
            await db.SaveChangesAsync(ct);
            return null;
        }

        /// <summary>Liegt <paramref name="candidateId"/> im Teilbaum von <paramref name="folderId"/>?</summary>
        private static async Task<bool> IsDescendantOrSelfAsync(TContext db, int candidateId, int folderId,
            CancellationToken ct)
        {
            int current = candidateId;
            int guard = 0;
            while (true)
            {
                if (current == folderId)
                {
                    return true;
                }

                // Notbremse: eine im Bestand vorhandene Schleife darf hier nicht zum Haenger werden.
                if (++guard > 256)
                {
                    return true;
                }

                int? parentId = await db.HelpResourceFolders.AsNoTracking()
                    .Where(f => f.HelpResourceFolderId == current)
                    .Select(f => f.ParentId)
                    .FirstOrDefaultAsync(ct);
                if (parentId is null)
                {
                    return false;
                }

                current = parentId.Value;
            }
        }

        public async Task<string?> SaveResourceFileAsync(ClaimsPrincipal admin, int helpResourceId, string culture, byte[] content,
            string? contentType, string? fileName, CancellationToken ct = default)
        {
            if (!services.VerifyUserPermissions(HelpPermissions.ResourcesWriteAny))
            {
                return "Not authorized.";
            }

            if (string.IsNullOrWhiteSpace(culture))
            {
                return "A culture is required.";
            }

            var opts = options.ValueOrDefault ?? new HelpSystemOptions();
            if (content == null || content.Length == 0)
            {
                return "The file is empty.";
            }

            if (content.Length > opts.MaxUploadBytes)
            {
                return $"The file exceeds the maximum of {opts.MaxUploadBytes} bytes.";
            }

            if (opts.AllowedContentTypePrefixes is { Length: > 0 } prefixes
                && !prefixes.Any(p => (contentType ?? string.Empty).StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                return $"Content type '{contentType}' is not allowed.";
            }

            var normalizedCulture = culture.Trim();
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var resource = await db.HelpResources.Include(r => r.Files)
                .FirstOrDefaultAsync(r => r.HelpResourceId == helpResourceId, ct);
            if (resource == null)
            {
                return "Resource not found.";
            }

            var newFileId = await store.SaveAsync(content, contentType, fileName, ct);

            var existing = resource.Files.FirstOrDefault(f => string.Equals(f.Culture, normalizedCulture, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                // Replace: drop the previous bytes so they don't leak.
                var oldId = existing.FileIdentifier;
                existing.FileIdentifier = newFileId;
                existing.ContentType = contentType;
                existing.OriginalName = fileName;
                await db.SaveChangesAsync(ct);
                if (!string.IsNullOrEmpty(oldId) && oldId != newFileId)
                {
                    await store.DeleteAsync(oldId, ct);
                }
            }
            else
            {
                resource.Files.Add(new HelpResourceFile
                {
                    Culture = normalizedCulture,
                    FileIdentifier = newFileId,
                    ContentType = contentType,
                    OriginalName = fileName
                });
                await db.SaveChangesAsync(ct);
            }

            return null;
        }

        public async Task<bool> DeleteResourceFileAsync(ClaimsPrincipal admin, int helpResourceId, string culture, CancellationToken ct = default)
        {
            if (!services.VerifyUserPermissions(HelpPermissions.ResourcesWriteAny))
            {
                return false;
            }

            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var file = await db.HelpResourceFiles
                .FirstOrDefaultAsync(f => f.HelpResourceId == helpResourceId && f.Culture == culture, ct);
            if (file == null)
            {
                return false;
            }

            var fileId = file.FileIdentifier;
            db.HelpResourceFiles.Remove(file);
            await db.SaveChangesAsync(ct);
            await store.DeleteAsync(fileId, ct);
            return true;
        }
    }
}
