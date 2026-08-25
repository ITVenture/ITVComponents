using System.Linq.Expressions;
using System.Security.Claims;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using Microsoft.EntityFrameworkCore;
using SystemEventEntity = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.SystemEvent;
using ITVComponents.WebCoreToolkit.Blazor.Paging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;

public class SystemLogAdminHandler : ISystemLogAdminHandler
{
    private static readonly Expression<Func<SystemEventEntity, SystemEventViewModel>> ToViewModel =
        e => new SystemEventViewModel
        {
            SystemEventId = e.SystemEventId,
            LogLevel = e.LogLevel,
            Category = e.Category,
            Title = e.Title,
            Message = e.Message,
            EventTime = e.EventTime
        };

    private readonly ICoreSystemContextFactory factory;
    private readonly IServiceProvider services;

    public SystemLogAdminHandler(ICoreSystemContextFactory factory, IServiceProvider services)
    {
        this.factory = factory;
        this.services = services;
    }

    public bool HasPermission(params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public async Task<PagedResult<SystemEventViewModel>> ListAsync(ClaimsPrincipal user, SystemLogQuery query)
    {
        if (!HasPermission("SystemLog.View"))
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
                .Select(ToViewModel)
                .ToListAsync();
            return new PagedResult<SystemEventViewModel> { Items = items, TotalCount = total };
        });
    }

    public async Task<SystemLogContextResult> GetContextAsync(ClaimsPrincipal user, SystemLogContextQuery query)
    {
        if (!HasPermission("SystemLog.View"))
            return new SystemLogContextResult();

        var before = Math.Clamp(query.Before, 0, SystemLogContextQuery.MaxContextSize);
        var after = Math.Clamp(query.After, 0, SystemLogContextQuery.MaxContextSize);

        return await factory.UseAsync(async db =>
        {
            var anchor = await db.SystemLog.AsNoTracking()
                .Where(e => e.SystemEventId == query.AnchorId)
                .Select(ToViewModel)
                .FirstOrDefaultAsync();
            if (anchor == null)
                return new SystemLogContextResult();

            // The anchor was fetched from the unfiltered set above, so it stays visible no matter what these
            // filters exclude — you are always allowed to see the entry you clicked "trace" on.
            var q = db.SystemLog.AsNoTracking().AsQueryable();
            if (query.SameCategoryOnly)
                q = q.Where(e => e.Category == anchor.Category);
            if (query.IncludeLevels != null && query.IncludeLevels.Count != 0)
            {
                var levels = query.IncludeLevels.ToArray();
                q = q.Where(e => levels.Contains(e.LogLevel));
            }
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var s = query.Search.Trim();
                q = q.Where(e => (e.Message != null && e.Message.Contains(s))
                              || (e.Title != null && e.Title.Contains(s))
                              || (e.Category != null && e.Category.Contains(s)));
            }

            // EventTime is not unique - the identity is the tie-breaker, so entries logged within the same
            // timestamp keep the order in which they were written.
            var anchorTime = anchor.EventTime;
            var anchorId = anchor.SystemEventId;

            var predecessors = await q
                .Where(e => e.EventTime < anchorTime || (e.EventTime == anchorTime && e.SystemEventId < anchorId))
                .OrderByDescending(e => e.EventTime).ThenByDescending(e => e.SystemEventId)
                .Take(before + 1)
                .Select(ToViewModel)
                .ToListAsync();

            var successors = await q
                .Where(e => e.EventTime > anchorTime || (e.EventTime == anchorTime && e.SystemEventId > anchorId))
                .OrderBy(e => e.EventTime).ThenBy(e => e.SystemEventId)
                .Take(after + 1)
                .Select(ToViewModel)
                .ToListAsync();

            // One extra row was requested on each side purely to tell whether the window is truncated.
            var hasMoreBefore = predecessors.Count > before;
            var hasMoreAfter = successors.Count > after;

            var items = new List<SystemEventViewModel>(before + after + 1);
            items.AddRange(Enumerable.Reverse(predecessors.Take(before)));
            items.Add(anchor);
            items.AddRange(successors.Take(after));

            return new SystemLogContextResult
            {
                Items = items,
                Anchor = anchor,
                HasMoreBefore = hasMoreBefore,
                HasMoreAfter = hasMoreAfter
            };
        });
    }
}
