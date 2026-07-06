using System.Security.Claims;
using System.Security.Cryptography;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Tree;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Tree.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Handlers.Impl;

/// <summary>
/// Hierarchy-strategy implementation of <see cref="ITenantInvitationHandler"/>. Operates against
/// <c>IHierarchySecurityContextWithOnboarding</c>. Every operation is gated by the caller's permissions in the
/// current permission-scope (which the centralized auth already restricts to their enabled, possibly inherited,
/// access on the ambient tenant) and scopes strictly to <c>CurrentTenantId</c>. Employee queries bypass the
/// onboarding global filters and scope explicitly by tenant instead, so an admin reliably sees every row of the
/// tenant they manage.
/// </summary>
public class HierarchyTenantInvitationHandler<TContext> : ITenantInvitationHandler
    where TContext : DbContext, IHierarchySecurityContextWithOnboarding
{
    private const int DefaultLifetimeDays = 14;

    private readonly IDbContextFactory<TContext> dbFactory;
    private readonly UserManager<User> userManager;
    private readonly IServiceProvider services;
    private readonly IInvitationMailComposer mailComposer;
    private readonly ILogger<HierarchyTenantInvitationHandler<TContext>> logger;

    public HierarchyTenantInvitationHandler(IDbContextFactory<TContext> dbFactory, UserManager<User> userManager,
        IServiceProvider services, IInvitationMailComposer mailComposer, ILogger<HierarchyTenantInvitationHandler<TContext>> logger)
    {
        this.dbFactory = dbFactory;
        this.userManager = userManager;
        this.services = services;
        this.mailComposer = mailComposer;
        this.logger = logger;
    }

    /// <summary>The hierarchy strategy pins a parent tenant, so sub-tenant invitations are supported.</summary>
    public bool SupportsTenantInvitations => true;

    public async Task<TenantPickerItem?> GetCurrentTenantAsync(ClaimsPrincipal admin, CancellationToken ct = default)
    {
        using var db = dbFactory.CreateDbContext();
        // The page invites from the ambient scope tenant; CurrentTenantId reflects exactly that selection. The
        // permission-scope check already restricts the caller to their enabled (possibly inherited) access on it.
        var current = db.CurrentTenantId ?? 0;
        if (current == 0 || !HasAny(OnboardingAdminPermissions.AnyAccess))
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
        var current = db.CurrentTenantId ?? 0;
        if (current == 0 || !HasAny(OnboardingAdminPermissions.SubTenantsWrite))
        {
            return new TenantInvitationResult(false, 0, null, default, "You are not permitted to invite sub-tenants.");
        }

        // Audit-only reference to the issuing user (no FK). We record the user id, not a TenantUser id: the
        // caller may hold the current tenant purely through an inherited parent membership and have no TenantUser
        // row on it, and we must never pin a foreign tenant's TenantUser here.
        var owner = await userManager.GetUserAsync(admin);

        var token = GenerateToken();
        var expires = DateTime.UtcNow.AddDays(input.LifetimeDays is > 0 ? input.LifetimeDays.Value : DefaultLifetimeDays);

        var invitation = new TenantInvitation
        {
            ParentTenantId = current,
            Email = input.Email,
            Token = token,
            ExpiresUtc = expires,
            Status = InvitationStatus.Pending,
            RoleName = input.RoleName,
            TemplateName = input.TemplateName,
            CreatedByUserId = owner?.Id,
            CreatedUtc = DateTime.UtcNow
        };
        db.TenantInvitations.Add(invitation);
        await db.SaveChangesAsync(ct);

        var parentName = await db.Tenants.AsNoTracking()
            .Where(t => t.TenantId == current)
            .Select(t => t.DisplayName ?? t.TenantName)
            .FirstOrDefaultAsync(ct) ?? "";
        var link = BuildAbsoluteLink($"/Account/Onboarding/Invitation/{token}");
        var mailSent = link != null && await mailComposer.SendTenantInvitationAsync(input.Email, null, parentName, link, ct);

        return new TenantInvitationResult(true, invitation.TenantInvitationId, token, expires, null, mailSent);
    }

    public async Task<TenantInvitationItem[]> ListTenantInvitationsAsync(ClaimsPrincipal admin, CancellationToken ct = default)
    {
        using var db = dbFactory.CreateDbContext();
        var current = db.CurrentTenantId ?? 0;
        if (current == 0 || !HasAny(OnboardingAdminPermissions.SubTenantsRead))
        {
            return Array.Empty<TenantInvitationItem>();
        }

        return await db.TenantInvitations.AsNoTracking()
            .Where(i => i.ParentTenantId == current)
            .OrderByDescending(i => i.CreatedUtc)
            .Select(i => new TenantInvitationItem(i.TenantInvitationId, i.ParentTenantId, i.Email, i.Token,
                i.ExpiresUtc, i.CreatedUtc, i.Status, i.ChildTenantId))
            .ToArrayAsync(ct);
    }

    public async Task<bool> RevokeTenantInvitationAsync(ClaimsPrincipal admin, int tenantInvitationId, CancellationToken ct = default)
    {
        using var db = dbFactory.CreateDbContext();
        var current = db.CurrentTenantId ?? 0;
        if (current == 0 || !HasAny(OnboardingAdminPermissions.SubTenantsWrite))
        {
            return false;
        }

        // Scope the row to the current tenant: an unscoped id would otherwise let an admin of tenant A revoke a
        // sub-tenant invitation issued under tenant B. Requiring ParentTenantId == current closes that.
        var invitation = await db.TenantInvitations
            .FirstOrDefaultAsync(i => i.TenantInvitationId == tenantInvitationId && i.ParentTenantId == current, ct);
        if (invitation == null || invitation.Status != InvitationStatus.Pending)
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
        var current = db.CurrentTenantId ?? 0;
        if (current == 0 || !HasAny(OnboardingAdminPermissions.EmployeesWrite))
        {
            return false;
        }

        // The tenant's billing profile anchors employees; an employee invite needs an existing profile.
        var profileId = await db.BillingProfiles.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.TenantId == current)
            .Select(p => (int?)p.BillingProfileId)
            .FirstOrDefaultAsync(ct);
        if (profileId == null)
        {
            logger.LogWarning("Employee invitation rejected: tenant {TenantId} has no billing profile.", current);
            return false;
        }

        var alreadyInvited = await db.Employees.IgnoreQueryFilters()
            .AnyAsync(e => e.TenantId == current && e.EMail == input.Email
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
            TenantId = current,
            FirstName = input.FirstName ?? "",
            LastName = input.LastName ?? ""
        });
        await db.SaveChangesAsync(ct);

        await SendEmployeeInvitationMailAsync(db, current, input, ct);
        return true;
    }

    /// <summary>
    /// Sends the employee invitation mail (employee invites carry no token — the invitee accepts by e-mail
    /// match on the My-Tenants page). A missing transport or a malformed template never fails the invitation.
    /// </summary>
    private async Task SendEmployeeInvitationMailAsync(TContext db, int tenantId, EmployeeInvitationInput input, CancellationToken ct)
    {
        var tenantName = await db.Tenants.AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .Select(t => t.DisplayName ?? t.TenantName)
            .FirstOrDefaultAsync(ct) ?? "";
        // Land the invitee on the Join page (anonymous): it guides a brand-new user through register-then-accept
        // and sends an existing user straight on to accept. The e-mail is informational (display/prefill) — the
        // actual match still runs by account e-mail on My-Tenants.
        var link = BuildAbsoluteLink($"/Account/Onboarding/Join?email={Uri.EscapeDataString(input.Email)}");
        if (link == null)
        {
            logger.LogError("Failed to build absolute link for employee invitation.");
            return;
        }

        var name = $"{input.FirstName} {input.LastName}".Trim();
        var sentOk = await mailComposer.SendEmployeeInvitationAsync(input.Email, name, tenantName, link, ct);
        if (!sentOk)
        {
            logger.LogError("Failed to send employee invitation.");
        }
    }

    /// <summary>
    /// Turns a relative app path into an absolute URL using the circuit's <see cref="NavigationManager"/>.
    /// Returns null when no NavigationManager is available (i.e. outside a Blazor circuit), in which case the
    /// caller skips sending — the invitation row is already persisted and the link can be shared manually.
    /// </summary>
    private string BuildAbsoluteLink(string relativePath)
    {
        var nav = services.GetService<NavigationManager>();
        return nav?.ToAbsoluteUri(relativePath).AbsoluteUri;
    }

    public async Task<EmployeeInvitationItem[]> ListEmployeeInvitationsAsync(ClaimsPrincipal admin, CancellationToken ct = default)
    {
        using var db = dbFactory.CreateDbContext();
        var current = db.CurrentTenantId ?? 0;
        if (current == 0 || !HasAny(OnboardingAdminPermissions.EmployeesRead))
        {
            return Array.Empty<EmployeeInvitationItem>();
        }

        return await db.Employees.IgnoreQueryFilters().AsNoTracking()
            .Where(e => e.TenantId == current)
            .OrderBy(e => e.EMail)
            .Select(e => new EmployeeInvitationItem(e.EmployeeId, e.TenantId, e.EMail, e.FirstName, e.LastName, e.InvitationStatus))
            .ToArrayAsync(ct);
    }

    public async Task<bool> RevokeEmployeeInvitationAsync(ClaimsPrincipal admin, int employeeId, CancellationToken ct = default)
    {
        using var db = dbFactory.CreateDbContext();
        var current = db.CurrentTenantId ?? 0;
        if (current == 0 || !HasAny(OnboardingAdminPermissions.EmployeesWrite))
        {
            return false;
        }

        // Scope the row to the current tenant: the lookup bypasses the tenant filter, so an unscoped id would
        // otherwise let a member of tenant A revoke tenant B's invitation. Requiring TenantId == current closes it.
        var employee = await db.Employees.IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId && e.TenantId == current, ct);
        if (employee == null || employee.InvitationStatus != InvitationStatus.Pending)
        {
            return false;
        }

        employee.InvitationStatus = InvitationStatus.Revoked;
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// True when the ambient user holds ANY of <paramref name="permissions"/> in the current permission-scope
    /// — the authoritative server-side gate mirroring the UI's SecureView/permission checks.
    /// </summary>
    private bool HasAny(params string[] permissions) => services.VerifyUserPermissions(permissions);

    private static string GenerateToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}
