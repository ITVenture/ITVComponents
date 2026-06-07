using System.Security.Claims;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;

public class AuthenticationTypeAdminHandler : IAuthenticationTypeAdminHandler
{
    private readonly ICoreSystemContext db;
    private readonly IServiceProvider services;

    public AuthenticationTypeAdminHandler(ICoreSystemContext db, IServiceProvider services)
    {
        this.db = db;
        this.services = services;
        this.db.ShowAllTenants = true;
    }

    public bool HasPermission(ClaimsPrincipal user, params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public async Task<PagedResult<AuthenticationTypeViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!HasPermission(user, "AuthenticationTypes.View", "AuthenticationTypes.Write"))
            return new PagedResult<AuthenticationTypeViewModel>();

        var q = db.AuthenticationTypes.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(a => a.AuthenticationTypeName.Contains(s));
        }
        var total = await q.CountAsync();
        q = query.SortDescending ? q.OrderByDescending(a => a.AuthenticationTypeName) : q.OrderBy(a => a.AuthenticationTypeName);
        var items = await q.Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(a => new AuthenticationTypeViewModel
            {
                AuthenticationTypeId = a.AuthenticationTypeId,
                AuthenticationTypeName = a.AuthenticationTypeName
            }).ToListAsync();
        return new PagedResult<AuthenticationTypeViewModel> { Items = items, TotalCount = total };
    }

    public async Task<AuthenticationTypeViewModel?> CreateAsync(ClaimsPrincipal user, AuthenticationTypeViewModel input)
    {
        if (!HasPermission(user, "AuthenticationTypes.Write")) return null;
        var entity = new AuthenticationType { AuthenticationTypeName = input.AuthenticationTypeName };
        db.AuthenticationTypes.Add(entity);
        await db.SaveChangesAsync();
        input.AuthenticationTypeId = entity.AuthenticationTypeId;
        return input;
    }

    public async Task<AuthenticationTypeViewModel?> UpdateAsync(ClaimsPrincipal user, AuthenticationTypeViewModel input)
    {
        if (!HasPermission(user, "AuthenticationTypes.Write")) return null;
        var entity = await db.AuthenticationTypes.FirstOrDefaultAsync(a => a.AuthenticationTypeId == input.AuthenticationTypeId);
        if (entity == null) return null;
        entity.AuthenticationTypeName = input.AuthenticationTypeName;
        await db.SaveChangesAsync();
        return input;
    }

    public async Task<bool> DeleteAsync(ClaimsPrincipal user, int authenticationTypeId)
    {
        if (!HasPermission(user, "AuthenticationTypes.Write")) return false;
        var entity = await db.AuthenticationTypes.FirstOrDefaultAsync(a => a.AuthenticationTypeId == authenticationTypeId);
        if (entity == null) return false;
        db.AuthenticationTypes.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<AuthenticationClaimMappingViewModel>> ListClaimsAsync(ClaimsPrincipal user, int authenticationTypeId, ListQuery query)
    {
        if (!HasPermission(user, "AuthenticationTypes.View", "AuthenticationTypes.Write"))
            return new PagedResult<AuthenticationClaimMappingViewModel>();

        var q = db.AuthenticationClaimMappings.AsNoTracking()
            .Where(c => c.AuthenticationTypeId == authenticationTypeId);
        var total = await q.CountAsync();
        var sorted = query.SortDescending ? q.OrderByDescending(c => c.IncomingClaimName) : q.OrderBy(c => c.IncomingClaimName);
        var page = await sorted.Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(c => new AuthenticationClaimMappingViewModel
            {
                AuthenticationClaimMappingId = c.AuthenticationClaimMappingId,
                AuthenticationTypeId = c.AuthenticationTypeId,
                IncomingClaimName = c.IncomingClaimName,
                Condition = c.Condition,
                OutgoingClaimName = c.OutgoingClaimName,
                OutgoingValueType = c.OutgoingValueType,
                OutgoingIssuer = c.OutgoingIssuer,
                OutgoingOriginalIssuer = c.OutgoingOriginalIssuer,
                OutgoingClaimValue = c.OutgoingClaimValue
            }).ToListAsync();
        return new PagedResult<AuthenticationClaimMappingViewModel> { Items = page, TotalCount = total };
    }

    public async Task<AuthenticationClaimMappingViewModel?> CreateClaimAsync(ClaimsPrincipal user, int authenticationTypeId, AuthenticationClaimMappingViewModel input)
    {
        if (!HasPermission(user, "AuthenticationTypes.Write")) return null;
        var entity = new AuthenticationClaimMapping
        {
            AuthenticationTypeId = authenticationTypeId,
            IncomingClaimName = input.IncomingClaimName,
            Condition = input.Condition,
            OutgoingClaimName = input.OutgoingClaimName,
            OutgoingValueType = input.OutgoingValueType,
            OutgoingIssuer = input.OutgoingIssuer,
            OutgoingOriginalIssuer = input.OutgoingOriginalIssuer,
            OutgoingClaimValue = input.OutgoingClaimValue
        };
        db.AuthenticationClaimMappings.Add(entity);
        await db.SaveChangesAsync();
        input.AuthenticationClaimMappingId = entity.AuthenticationClaimMappingId;
        input.AuthenticationTypeId = authenticationTypeId;
        return input;
    }

    public async Task<AuthenticationClaimMappingViewModel?> UpdateClaimAsync(ClaimsPrincipal user, AuthenticationClaimMappingViewModel input)
    {
        if (!HasPermission(user, "AuthenticationTypes.Write")) return null;
        var entity = await db.AuthenticationClaimMappings.FirstOrDefaultAsync(c => c.AuthenticationClaimMappingId == input.AuthenticationClaimMappingId);
        if (entity == null) return null;
        entity.IncomingClaimName = input.IncomingClaimName;
        entity.Condition = input.Condition;
        entity.OutgoingClaimName = input.OutgoingClaimName;
        entity.OutgoingValueType = input.OutgoingValueType;
        entity.OutgoingIssuer = input.OutgoingIssuer;
        entity.OutgoingOriginalIssuer = input.OutgoingOriginalIssuer;
        entity.OutgoingClaimValue = input.OutgoingClaimValue;
        await db.SaveChangesAsync();
        return input;
    }

    public async Task<bool> DeleteClaimAsync(ClaimsPrincipal user, int claimMappingId)
    {
        if (!HasPermission(user, "AuthenticationTypes.Write")) return false;
        var entity = await db.AuthenticationClaimMappings.FirstOrDefaultAsync(c => c.AuthenticationClaimMappingId == claimMappingId);
        if (entity == null) return false;
        db.AuthenticationClaimMappings.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }
}
