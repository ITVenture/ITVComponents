using System.Security.Claims;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Flat;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Flat.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Handlers.Impl;

/// <summary>
/// Flat-strategy implementation of <see cref="ITenantInvitationHandler"/>. Operates against
/// <c>ISecurityContextWithOnboarding</c>, which has no parent relationship and therefore no
/// <c>TenantInvitations</c> set: sub-tenant invitations are <b>not supported</b>
/// (<see cref="SupportsTenantInvitations"/> is <c>false</c> and the tenant-invitation members are inert).
/// Employee invitations work exactly as in the hierarchy variant — the caller must be an enabled member of
/// the affected tenant, and employee queries bypass the onboarding global filters and scope explicitly by
/// tenant instead, so an admin reliably sees every row of the tenant they manage.
/// </summary>
public class FlatTenantInvitationHandler<TContext> : ITenantInvitationHandler
    where TContext : DbContext, ISecurityContextWithOnboarding
{
    private readonly IDbContextFactory<TContext> dbFactory;
    private readonly UserManager<User> userManager;
    private readonly IServiceProvider services;
    private readonly IInvitationMailComposer mailComposer;
    private readonly ILogger<FlatTenantInvitationHandler<TContext>> logger;

    public FlatTenantInvitationHandler(IDbContextFactory<TContext> dbFactory, UserManager<User> userManager,
        IServiceProvider services, IInvitationMailComposer mailComposer, ILogger<FlatTenantInvitationHandler<TContext>> logger)
    {
        this.dbFactory = dbFactory;
        this.userManager = userManager;
        this.services = services;
        this.mailComposer = mailComposer;
        this.logger = logger;
    }

    /// <summary>The flat strategy has no parent to pin a sub-tenant invitation under, so it is unsupported.</summary>
    public bool SupportsTenantInvitations => false;

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

    // -- Tenant invitations: unsupported in the flat strategy (no parent to pin an invitation under) -------

    public Task<TenantInvitationResult> CreateTenantInvitationAsync(ClaimsPrincipal admin, TenantInvitationInput input, CancellationToken ct = default)
        => Task.FromResult(new TenantInvitationResult(false, 0, null, default, "Sub-tenant invitations are not supported in the flat tenant strategy."));

    public Task<TenantInvitationItem[]> ListTenantInvitationsAsync(ClaimsPrincipal admin, int parentTenantId, CancellationToken ct = default)
        => Task.FromResult(Array.Empty<TenantInvitationItem>());

    public Task<bool> RevokeTenantInvitationAsync(ClaimsPrincipal admin, int tenantInvitationId, CancellationToken ct = default)
        => Task.FromResult(false);

    public Task<TenantInvitationInfo?> ResolveTenantInvitationAsync(string token, CancellationToken ct = default)
        => Task.FromResult<TenantInvitationInfo?>(null);

    // -- Employee invitations -----------------------------------------------------------------------

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

        db.Employees.Add(new Employee
        {
            InvitationStatus = InvitationStatus.Pending,
            EMail = input.Email,
            BillingProfileId = profileId.Value,
            TenantId = input.TenantId,
            FirstName = input.FirstName ?? "",
            LastName = input.LastName ?? ""
        });
        await db.SaveChangesAsync(ct);

        await SendEmployeeInvitationMailAsync(db, input, ct);
        return true;
    }

    /// <summary>
    /// Sends the employee invitation mail (employee invites carry no token — the invitee accepts by e-mail
    /// match on the My-Tenants page). A missing transport or a malformed template never fails the invitation.
    /// </summary>
    private async Task SendEmployeeInvitationMailAsync(TContext db, EmployeeInvitationInput input, CancellationToken ct)
    {
        var tenantName = await db.Tenants.AsNoTracking()
            .Where(t => t.TenantId == input.TenantId)
            .Select(t => t.DisplayName ?? t.TenantName)
            .FirstOrDefaultAsync(ct) ?? "";
        // Land the invitee on the Join page (anonymous): it guides a brand-new user through register-then-accept
        // and sends an existing user straight on to accept. The e-mail is informational (display/prefill) — the
        // actual match still runs by account e-mail on My-Tenants.
        var link = BuildAbsoluteLink($"/Account/Onboarding/Join?email={Uri.EscapeDataString(input.Email)}");
        if (link == null)
        {
            return;
        }

        var name = $"{input.FirstName} {input.LastName}".Trim();
        await mailComposer.SendEmployeeInvitationAsync(input.Email, name, tenantName, link, ct);
    }

    /// <summary>
    /// Turns a relative app path into an absolute URL using the circuit's <see cref="NavigationManager"/>.
    /// Returns null when none is available (outside a Blazor circuit); the caller then skips sending and the
    /// link can be shared manually — the invitation row is already persisted.
    /// </summary>
    private string BuildAbsoluteLink(string relativePath)
    {
        var nav = services.GetService<NavigationManager>();
        return nav?.ToAbsoluteUri(relativePath).AbsoluteUri;
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
    private async Task<TenantUser> GetMembershipAsync(TContext db, ClaimsPrincipal admin, int tenantId, CancellationToken ct)
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
}
