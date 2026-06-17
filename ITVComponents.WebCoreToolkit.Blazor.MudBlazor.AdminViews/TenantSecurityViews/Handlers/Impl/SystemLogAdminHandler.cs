using System.Security.Claims;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;

public class SystemLogAdminHandler : ISystemLogAdminHandler
{
    private readonly ICoreSystemContextFactory factory;
    private readonly IServiceProvider services;

    public SystemLogAdminHandler(ICoreSystemContextFactory factory, IServiceProvider services)
    {
        this.factory = factory;
        this.services = services;
    }

    public bool HasPermission(ClaimsPrincipal user, params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public async Task<PagedResult<SystemEventViewModel>> ListAsync(ClaimsPrincipal user, SystemLogQuery query)
    {
        if (!HasPermission(user, "SystemLog.View"))
            return new PagedResult<SystemEventViewModel>();

        return await factory.UseAsync(async db =>
        {
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
        });
    }
}
