using System.Security.Claims;
using System.Security.Cryptography;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Tree;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Tree.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Handlers.Impl;

/// <summary>
/// Hierarchy-strategy implementation of <see cref="ITenantInvitationHandler"/>. Operates against
/// <c>IHierarchySecurityContextWithOnboarding</c>. All admin operations require the caller to be an
/// enabled member of the affected tenant; employee queries bypass the onboarding global filters and scope
/// explicitly by tenant instead, so an admin reliably sees every row of the tenant they manage.
/// </summary>
public class HierarchyTenantInvitationHandler<TContext> : ITenantInvitationHandler
    where TContext : DbContext, IHierarchySecurityContextWithOnboarding
{
    private const int DefaultLifetimeDays = 14;

    private readonly IDbContextFactory<TContext> dbFactory;
    private readonly UserManager<User> userManager;
    private readonly IServiceProvider services;
    private readonly ILogger<HierarchyTenantInvitationHandler<TContext>> logger;

    public HierarchyTenantInvitationHandler(IDbContextFactory<TContext> dbFactory, UserManager<User> userManager,
        IServiceProvider services, ILogger<HierarchyTenantInvitationHandler<TContext>> logger)
    {
        this.dbFactory = dbFactory;
        this.userManager = userManager;
        this.services = services;
        this.logger = logger;
    }

    /// <summary>The hierarchy strategy pins a parent tenant, so sub-tenant invitations are supported.</summary>
    public bool SupportsTenantInvitations => true;

    public async Task<TenantPickerItem?> GetCurrentTenantAsync(ClaimsPrincipal admin, CancellationToken ct = default)
    {
        using var db = dbFactory.CreateDbContext();
        // The page invites from the ambient scope tenant; CurrentTenantId reflects exactly that selection.
        var current = db.CurrentTenantId ?? 0;
        if (current == 0 || await GetMembershipAsync(db, admin, current, ct) == null)
        {
            return null;
        }

        return await db.Tenants.AsNoTracking()
            .Where(t => t.TenantId == current)
            .Select(t => new TenantPickerItem(t.TenantId, t.DisplayName ?? t.TenantName))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<TenantPickerItem[]> ListAdministrableTenantsAsync(ClaimsPrincipal admin, CancellationToken ct = default)
    {
        var owner = await userManager.GetUserAsync(admin);
        if (owner == null)
        {
            return Array.Empty<TenantPickerItem>();
        }

        using var db = dbFactory.CreateDbContext();
        return await (from tu in db.TenantUsers.AsNoTracking()
            join t in db.Tenants.AsNoTracking() on tu.TenantId equals t.TenantId
            where tu.UserId == owner.Id && tu.Enabled == true
            orderby t.DisplayName, t.TenantName
            select new TenantPickerItem(t.TenantId, t.DisplayName ?? t.TenantName))
            .ToArrayAsync(ct);
    }

    public async Task<TenantInvitationResult> CreateTenantInvitationAsync(ClaimsPrincipal admin, TenantInvitationInput input, CancellationToken ct = default)
    {
        using var db = dbFactory.CreateDbContext();
        var membership = await GetMembershipAsync(db, admin, input.ParentTenantId, ct);
        if (membership == null)
        {
            return new TenantInvitationResult(false, 0, null, default, "You are not a member of the inviting tenant.");
        }

        if (!HasAny(OnboardingAdminPermissions.SubTenantsWrite))
        {
            return new TenantInvitationResult(false, 0, null, default, "You are not permitted to invite sub-tenants.");
        }

        var token = GenerateToken();
        var expires = DateTime.UtcNow.AddDays(input.LifetimeDays is > 0 ? input.LifetimeDays.Value : DefaultLifetimeDays);

        var invitation = new TenantInvitation
        {
            ParentTenantId = input.ParentTenantId,
            Email = input.Email,
            Token = token,
            ExpiresUtc = expires,
            Status = InvitationStatus.Pending,
            RoleName = input.RoleName,
            TemplateName = input.TemplateName,
            CreatedByTenantUserId = membership.TenantUserId,
            CreatedUtc = DateTime.UtcNow
        };
        db.TenantInvitations.Add(invitation);
        await db.SaveChangesAsync(ct);

        return new TenantInvitationResult(true, invitation.TenantInvitationId, token, expires, null);
    }

    public async Task<TenantInvitationItem[]> ListTenantInvitationsAsync(ClaimsPrincipal admin, int parentTenantId, CancellationToken ct = default)
    {
        using var db = dbFactory.CreateDbContext();
        if (!HasAny(OnboardingAdminPermissions.SubTenantsRead)
            || await GetMembershipAsync(db, admin, parentTenantId, ct) == null)
        {
            return Array.Empty<TenantInvitationItem>();
        }

        return await db.TenantInvitations.AsNoTracking()
            .Where(i => i.ParentTenantId == parentTenantId)
            .OrderByDescending(i => i.CreatedUtc)
            .Select(i => new TenantInvitationItem(i.TenantInvitationId, i.ParentTenantId, i.Email, i.Token,
                i.ExpiresUtc, i.CreatedUtc, i.Status, i.ChildTenantId))
            .ToArrayAsync(ct);
    }

    public async Task<bool> RevokeTenantInvitationAsync(ClaimsPrincipal admin, int tenantInvitationId, CancellationToken ct = default)
    {
        using var db = dbFactory.CreateDbContext();
        var invitation = await db.TenantInvitations.FirstOrDefaultAsync(i => i.TenantInvitationId == tenantInvitationId, ct);
        if (invitation == null || invitation.Status != InvitationStatus.Pending)
        {
            return false;
        }

        if (!HasAny(OnboardingAdminPermissions.SubTenantsWrite) || await GetMembershipAsync(db, admin, invitation.ParentTenantId, ct) == null)
        {
            return false;
        }

        invitation.Status = InvitationStatus.Revoked;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<TenantInvitationInfo?> ResolveTenantInvitationAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        using var db = dbFactory.CreateDbContext();
        var invitation = await db.TenantInvitations.FirstOrDefaultAsync(i => i.Token == token, ct);
        if (invitation == null)
        {
            return null;
        }

        if (invitation.Status == InvitationStatus.Pending && invitation.ExpiresUtc < DateTime.UtcNow)
        {
            invitation.Status = InvitationStatus.Expired;
            await db.SaveChangesAsync(ct);
        }

        var parentName = await db.Tenants.AsNoTracking()
            .Where(t => t.TenantId == invitation.ParentTenantId)
            .Select(t => t.DisplayName ?? t.TenantName)
            .FirstOrDefaultAsync(ct) ?? "";

        var acceptable = invitation.Status == InvitationStatus.Pending;
        return new TenantInvitationInfo(invitation.ParentTenantId, parentName, invitation.Email, invitation.Status, acceptable);
    }

    public async Task<bool> CreateEmployeeInvitationAsync(ClaimsPrincipal admin, EmployeeInvitationInput input, CancellationToken ct = default)
    {
        using var db = dbFactory.CreateDbContext();
        if (!HasAny(OnboardingAdminPermissions.EmployeesWrite) || await GetMembershipAsync(db, admin, input.TenantId, ct) == null)
        {
            return false;
        }

        // The tenant's billing profile anchors employees; an employee invite needs an existing profile.
        var profileId = await db.BillingProfiles.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.TenantId == input.TenantId)
            .Select(p => (int?)p.BillingProfileId)
            .FirstOrDefaultAsync(ct);
        if (profileId == null)
        {
            logger.LogWarning("Employee invitation rejected: tenant {TenantId} has no billing profile.", input.TenantId);
            return false;
        }

        var alreadyInvited = await db.Employees.IgnoreQueryFilters()
            .AnyAsync(e => e.TenantId == input.TenantId && e.EMail == input.Email
                           && (e.InvitationStatus == InvitationStatus.Pending || e.InvitationStatus == InvitationStatus.Committed), ct);
        if (alreadyInvited)
        {
            return false;
        }

        db.Employees.Add(new HierarchyEmployee
        {
            InvitationStatus = InvitationStatus.Pending,
            EMail = input.Email,
            BillingProfileId = profileId.Value,
            TenantId = input.TenantId,
            FirstName = input.FirstName ?? "",
            LastName = input.LastName ?? ""
        });
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<EmployeeInvitationItem[]> ListEmployeeInvitationsAsync(ClaimsPrincipal admin, int tenantId, CancellationToken ct = default)
    {
        using var db = dbFactory.CreateDbContext();
        if (!HasAny(OnboardingAdminPermissions.EmployeesRead)
            || await GetMembershipAsync(db, admin, tenantId, ct) == null)
        {
            return Array.Empty<EmployeeInvitationItem>();
        }

        return await db.Employees.IgnoreQueryFilters().AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.EMail)
            .Select(e => new EmployeeInvitationItem(e.EmployeeId, e.TenantId, e.EMail, e.FirstName, e.LastName, e.InvitationStatus))
            .ToArrayAsync(ct);
    }

    public async Task<bool> RevokeEmployeeInvitationAsync(ClaimsPrincipal admin, int employeeId, CancellationToken ct = default)
    {
        using var db = dbFactory.CreateDbContext();
        var employee = await db.Employees.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.EmployeeId == employeeId, ct);
        if (employee == null || employee.InvitationStatus != InvitationStatus.Pending)
        {
            return false;
        }

        if (!HasAny(OnboardingAdminPermissions.EmployeesWrite) || await GetMembershipAsync(db, admin, employee.TenantId, ct) == null)
        {
            return false;
        }

        employee.InvitationStatus = InvitationStatus.Revoked;
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Returns the caller's enabled membership row for <paramref name="tenantId"/>, or null when the caller
    /// is unknown or not an enabled member — the authorization gate for every admin operation here.
    /// </summary>
    private async Task<HierarchyTenantUser> GetMembershipAsync(TContext db, ClaimsPrincipal admin, int tenantId, CancellationToken ct)
    {
        var owner = await userManager.GetUserAsync(admin);
        if (owner == null)
        {
            return null;
        }

        return await db.TenantUsers.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(tu => tu.UserId == owner.Id && tu.TenantId == tenantId && tu.Enabled == true, ct);
    }

    /// <summary>
    /// True when the ambient user holds ANY of <paramref name="permissions"/> in the current permission-scope
    /// — the authoritative server-side gate mirroring the UI's SecureView/permission checks.
    /// </summary>
    private bool HasAny(params string[] permissions) => services.VerifyUserPermissions(permissions);

    private static string GenerateToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}
