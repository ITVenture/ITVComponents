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
