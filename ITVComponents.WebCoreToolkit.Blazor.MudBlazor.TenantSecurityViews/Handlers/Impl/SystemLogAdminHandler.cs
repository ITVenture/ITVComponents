using System.Security.Claims;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers.Impl;

public class SystemLogAdminHandler : ISystemLogAdminHandler
{
    private readonly ICoreSystemContext db;
    private readonly IServiceProvider services;

    public SystemLogAdminHandler(ICoreSystemContext db, IServiceProvider services)
    {
        this.db = db;
        this.services = services;
        this.db.ShowAllTenants = true;
    }

    public bool HasPermission(ClaimsPrincipal user, params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public async Task<PagedResult<SystemEventViewModel>> ListAsync(ClaimsPrincipal user, SystemLogQuery query)
    {
        if (!HasPermission(user, "SystemLog.View"))
            return new PagedResult<SystemEventViewModel>();

        var q = db.SystemLog.AsNoTracking().AsQueryable();
        if (query.MinimumLevel.HasValue)
            q = q.Where(e => e.LogLevel >= query.MinimumLevel.Value);
        if (query.MaximumLevel.HasValue)
            q = q.Where(e => e.LogLevel <= query.MaximumLevel.Value);
        if (query.From.HasValue)
            q = q.Where(e => e.EventTime >= query.From.Value);
        if (query.To.HasValue)
            q = q.Where(e => e.EventTime <= query.To.Value);
        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var c = query.Category.Trim();
            q = q.Where(e => e.Category != null && e.Category.Contains(c));
        }
        if (!string.IsNullOrWhiteSpace(query.Title))
        {
            var t = query.Title.Trim();
            q = q.Where(e => e.Title != null && e.Title.Contains(t));
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(e => (e.Message != null && e.Message.Contains(s))
                          || (e.Title != null && e.Title.Contains(s))
                          || (e.Category != null && e.Category.Contains(s)));
        }

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(e => e.EventTime)
            .Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(e => new SystemEventViewModel
            {
                SystemEventId = e.SystemEventId,
                LogLevel = e.LogLevel,
                Category = e.Category,
                Title = e.Title,
                Message = e.Message,
                EventTime = e.EventTime
            })
            .ToListAsync();
        return new PagedResult<SystemEventViewModel> { Items = items, TotalCount = total };
    }
}
