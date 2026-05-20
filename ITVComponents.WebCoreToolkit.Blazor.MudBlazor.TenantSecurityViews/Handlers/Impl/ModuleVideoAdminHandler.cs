using System.Security.Claims;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers.Impl;

public class ModuleVideoAdminHandler : IModuleVideoAdminHandler
{
    private readonly ICoreSystemContext db;
    private readonly IServiceProvider services;

    public ModuleVideoAdminHandler(ICoreSystemContext db, IServiceProvider services)
    {
        this.db = db;
        this.services = services;
    }

    public bool HasPermission(ClaimsPrincipal user, params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public async Task<PagedResult<VideoTutorialViewModel>> ListTutorialsAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!HasPermission(user, "ModuleHelp.View", "ModuleHelp.Write")) return new PagedResult<VideoTutorialViewModel>();
        var q = db.Tutorials.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search)) q = q.Where(t => t.DisplayName.Contains(query.Search.Trim()) || t.SortableName.Contains(query.Search.Trim()));
        var total = await q.CountAsync();
        var items = await q.OrderBy(t => t.SortableName)
            .Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(t => new VideoTutorialViewModel
            {
                VideoTutorialId = t.VideoTutorialId,
                SortableName = t.SortableName,
                DisplayName = t.DisplayName,
                Description = t.Description,
                ModuleUrl = t.ModuleUrl
            })
            .ToListAsync();
        return new PagedResult<VideoTutorialViewModel> { Items = items, TotalCount = total };
    }

    public async Task<VideoTutorialViewModel?> CreateTutorialAsync(ClaimsPrincipal user, VideoTutorialViewModel input)
    {
        if (!HasPermission(user, "ModuleHelp.Write")) return null;
        var entity = new VideoTutorial
        {
            SortableName = input.SortableName,
            DisplayName = input.DisplayName,
            Description = input.Description ?? string.Empty,
            ModuleUrl = input.ModuleUrl ?? string.Empty
        };
        db.Tutorials.Add(entity);
        await db.SaveChangesAsync();
        input.VideoTutorialId = entity.VideoTutorialId;
        return input;
    }

    public async Task<VideoTutorialViewModel?> UpdateTutorialAsync(ClaimsPrincipal user, VideoTutorialViewModel input)
    {
        if (!HasPermission(user, "ModuleHelp.Write")) return null;
        var entity = await db.Tutorials.FirstOrDefaultAsync(t => t.VideoTutorialId == input.VideoTutorialId);
        if (entity == null) return null;
        entity.SortableName = input.SortableName;
        entity.DisplayName = input.DisplayName;
        entity.Description = input.Description ?? string.Empty;
        entity.ModuleUrl = input.ModuleUrl ?? string.Empty;
        await db.SaveChangesAsync();
        return input;
    }

    public async Task<bool> DeleteTutorialAsync(ClaimsPrincipal user, int videoTutorialId)
    {
        if (!HasPermission(user, "ModuleHelp.Write")) return false;
        var entity = await db.Tutorials.FirstOrDefaultAsync(t => t.VideoTutorialId == videoTutorialId);
        if (entity == null) return false;
        db.Tutorials.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<TutorialStreamViewModel>> ListStreamsAsync(ClaimsPrincipal user, int videoTutorialId, ListQuery query)
    {
        if (!HasPermission(user, "ModuleHelp.View", "ModuleHelp.Write")) return new PagedResult<TutorialStreamViewModel>();
        var q = db.TutorialStreams.AsNoTracking().Where(s => s.VideoTutorialId == videoTutorialId);
        var total = await q.CountAsync();
        var items = await q.OrderBy(s => s.LanguageTag)
            .Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(s => new TutorialStreamViewModel
            {
                TutorialStreamId = s.TutorialStreamId,
                VideoTutorialId = s.VideoTutorialId,
                LanguageTag = s.LanguageTag,
                ContentType = s.ContentType
            })
            .ToListAsync();
        return new PagedResult<TutorialStreamViewModel> { Items = items, TotalCount = total };
    }

    public async Task<TutorialStreamViewModel?> UpdateStreamAsync(ClaimsPrincipal user, TutorialStreamViewModel input)
    {
        if (!HasPermission(user, "ModuleHelp.Write")) return null;
        var entity = await db.TutorialStreams.FirstOrDefaultAsync(s => s.TutorialStreamId == input.TutorialStreamId);
        if (entity == null) return null;
        entity.LanguageTag = input.LanguageTag;
        entity.ContentType = input.ContentType;
        await db.SaveChangesAsync();
        return input;
    }

    public async Task<bool> DeleteStreamAsync(ClaimsPrincipal user, int tutorialStreamId)
    {
        if (!HasPermission(user, "ModuleHelp.Write")) return false;
        var entity = await db.TutorialStreams.FirstOrDefaultAsync(s => s.TutorialStreamId == tutorialStreamId);
        if (entity == null) return false;
        db.TutorialStreams.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }
}
