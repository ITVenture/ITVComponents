using System.Security.Claims;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using Microsoft.EntityFrameworkCore;
using ITVComponents.WebCoreToolkit.Blazor.Paging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;

// Phase 2 (IDbContextFactory-Migration / Pilot): dieser Handler bezieht den Context NICHT mehr als geteilte,
// circuit-scoped Instanz, sondern pro Operation frisch über die ICoreSystemContextFactory (Blazor-sicher).
// factory.UseAsync(...) erzeugt einen frischen Context, elevatet (ShowAllTenants) für Admin-Zugriff, führt den
// Body aus und disposed den Context. Laden+Speichern passieren je Methode atomar auf derselben per-Operation-
// Instanz -> kein detached Attach/Update nötig.
public class AuthenticationTypeAdminHandler : IAuthenticationTypeAdminHandler
{
    private readonly ICoreSystemContextFactory factory;
    private readonly IServiceProvider services;

    public AuthenticationTypeAdminHandler(ICoreSystemContextFactory factory, IServiceProvider services)
    {
        this.factory = factory;
        this.services = services;
    }

    public bool HasPermission(params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public Task<PagedResult<AuthenticationTypeViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!HasPermission("AuthenticationTypes.View", "AuthenticationTypes.Write"))
            return Task.FromResult(new PagedResult<AuthenticationTypeViewModel>());

        return factory.UseAsync(async db =>
        {
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
        });
    }

    public Task<AuthenticationTypeViewModel?> CreateAsync(ClaimsPrincipal user, AuthenticationTypeViewModel input)
    {
        if (!HasPermission("AuthenticationTypes.Write")) return Task.FromResult<AuthenticationTypeViewModel?>(null);

        return factory.UseAsync<AuthenticationTypeViewModel?>(async db =>
        {
            var entity = new AuthenticationType { AuthenticationTypeName = input.AuthenticationTypeName };
            db.AuthenticationTypes.Add(entity);
            await db.SaveChangesAsync();
            input.AuthenticationTypeId = entity.AuthenticationTypeId;
            return input;
        });
    }

    public Task<AuthenticationTypeViewModel?> UpdateAsync(ClaimsPrincipal user, AuthenticationTypeViewModel input)
    {
        if (!HasPermission("AuthenticationTypes.Write")) return Task.FromResult<AuthenticationTypeViewModel?>(null);

        return factory.UseAsync<AuthenticationTypeViewModel?>(async db =>
        {
            var entity = await db.AuthenticationTypes.FirstOrDefaultAsync(a => a.AuthenticationTypeId == input.AuthenticationTypeId);
            if (entity == null) return null;
            entity.AuthenticationTypeName = input.AuthenticationTypeName;
            await db.SaveChangesAsync();
            return input;
        });
    }

    public Task<bool> DeleteAsync(ClaimsPrincipal user, int authenticationTypeId)
    {
        if (!HasPermission("AuthenticationTypes.Write")) return Task.FromResult(false);

        return factory.UseAsync(async db =>
        {
            var entity = await db.AuthenticationTypes.FirstOrDefaultAsync(a => a.AuthenticationTypeId == authenticationTypeId);
            if (entity == null) return false;
            db.AuthenticationTypes.Remove(entity);
            await db.SaveChangesAsync();
            return true;
        });
    }

    public Task<PagedResult<AuthenticationClaimMappingViewModel>> ListClaimsAsync(ClaimsPrincipal user, int authenticationTypeId, ListQuery query)
    {
        if (!HasPermission("AuthenticationTypes.View", "AuthenticationTypes.Write"))
            return Task.FromResult(new PagedResult<AuthenticationClaimMappingViewModel>());

        return factory.UseAsync(async db =>
        {
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
        });
    }

    public Task<AuthenticationClaimMappingViewModel?> CreateClaimAsync(ClaimsPrincipal user, int authenticationTypeId, AuthenticationClaimMappingViewModel input)
    {
        if (!HasPermission("AuthenticationTypes.Write")) return Task.FromResult<AuthenticationClaimMappingViewModel?>(null);

        return factory.UseAsync<AuthenticationClaimMappingViewModel?>(async db =>
        {
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
        });
    }

    public Task<AuthenticationClaimMappingViewModel?> UpdateClaimAsync(ClaimsPrincipal user, AuthenticationClaimMappingViewModel input)
    {
        if (!HasPermission("AuthenticationTypes.Write")) return Task.FromResult<AuthenticationClaimMappingViewModel?>(null);

        return factory.UseAsync<AuthenticationClaimMappingViewModel?>(async db =>
        {
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
        });
    }

    public Task<bool> DeleteClaimAsync(ClaimsPrincipal user, int claimMappingId)
    {
        if (!HasPermission("AuthenticationTypes.Write")) return Task.FromResult(false);

        return factory.UseAsync(async db =>
        {
            var entity = await db.AuthenticationClaimMappings.FirstOrDefaultAsync(c => c.AuthenticationClaimMappingId == claimMappingId);
            if (entity == null) return false;
            db.AuthenticationClaimMappings.Remove(entity);
            await db.SaveChangesAsync();
            return true;
        });
    }
}
