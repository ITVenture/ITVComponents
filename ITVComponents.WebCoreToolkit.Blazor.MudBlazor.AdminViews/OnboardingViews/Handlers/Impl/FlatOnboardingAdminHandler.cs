using System.Security.Claims;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Flat;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Flat.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.ViewModels;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Handlers.Impl;

/// <summary>
/// Flat-strategy implementation of <see cref="IOnboardingAdminHandler"/> over
/// <c>ISecurityContextWithOnboarding</c>. Every operation is scoped to the caller's current tenant
/// (<c>CurrentTenantId</c>), gated by the <c>ManageEmployees</c> permission plus an enabled membership in
/// that tenant; onboarding entities are read with <c>IgnoreQueryFilters</c> and scoped explicitly by
/// <c>TenantId</c> so the admin reliably sees every row of the tenant they manage.
/// </summary>
public class FlatOnboardingAdminHandler<TContext> : IOnboardingAdminHandler
    where TContext : DbContext, ISecurityContextWithOnboarding
{
    private readonly IDbContextFactory<TContext> dbFactory;
    private readonly UserManager<User> userManager;
    private readonly IServiceProvider services;

    public FlatOnboardingAdminHandler(IDbContextFactory<TContext> dbFactory, UserManager<User> userManager, IServiceProvider services)
    {
        this.dbFactory = dbFactory;
        this.userManager = userManager;
        this.services = services;
    }

    public bool HasPermission(ClaimsPrincipal user, params string[] permissions) => services.VerifyUserPermissions(permissions);

    public bool CanManage(ClaimsPrincipal user) => services.VerifyUserPermissions(new[] { OnboardingAdminPermissions.ManageEmployees });

    public async Task<TenantPickerItem?> GetCurrentTenantAsync(ClaimsPrincipal admin, CancellationToken ct = default)
    {
        using var db = dbFactory.CreateDbContext();
        var (ok, current) = await AuthorizeAsync(db, admin, ct);
        if (!ok)
        {
            return null;
        }

        return await db.Tenants.AsNoTracking()
            .Where(t => t.TenantId == current)
            .Select(t => new TenantPickerItem(t.TenantId, t.DisplayName ?? t.TenantName))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<BillingProfileAdminViewModel[]> ListBillingProfilesAsync(ClaimsPrincipal admin, CancellationToken ct = default)
    {
        using var db = dbFactory.CreateDbContext();
        var (ok, current) = await AuthorizeAsync(db, admin, ct);
        if (!ok)
        {
            return Array.Empty<BillingProfileAdminViewModel>();
        }

        return await db.BillingProfiles.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.TenantId == current)
            .OrderBy(p => p.CompanyName).ThenBy(p => p.LastName)
            .Select(p => new BillingProfileAdminViewModel
            {
                BillingProfileId = p.BillingProfileId,
                TenantId = p.TenantId,
                TenantName = p.Tenant.DisplayName ?? p.Tenant.TenantName,
                ProfileType = p.ProfileType,
                FirstName = p.FirstName,
                LastName = p.LastName,
                CompanyName = p.CompanyName,
                VatNumber = p.VatNumber,
                Email = p.Email,
                PhoneNumber = p.PhoneNumber,
                UseInvoiceAddr = p.UseInvoiceAddr,
                EmployeeCount = p.Employees.Count
            })
            .ToArrayAsync(ct);
    }

    public async Task<BillingProfileAdminViewModel?> GetBillingProfileAsync(ClaimsPrincipal admin, int billingProfileId, CancellationToken ct = default)
    {
        using var db = dbFactory.CreateDbContext();
        var (ok, current) = await AuthorizeAsync(db, admin, ct);
        if (!ok)
        {
            return null;
        }

        var p = await db.BillingProfiles.IgnoreQueryFilters().AsNoTracking()
            .Include(x => x.DefaultAddress)
            .Include(x => x.InvoiceAddress)
            .FirstOrDefaultAsync(x => x.BillingProfileId == billingProfileId && x.TenantId == current, ct);
        if (p == null)
        {
            return null;
        }

        return new BillingProfileAdminViewModel
        {
            BillingProfileId = p.BillingProfileId,
            TenantId = p.TenantId,
            ProfileType = p.ProfileType,
            FirstName = p.FirstName,
            LastName = p.LastName,
            CompanyName = p.CompanyName,
            VatNumber = p.VatNumber,
            Email = p.Email,
            PhoneNumber = p.PhoneNumber,
            UseInvoiceAddr = p.UseInvoiceAddr,
            DefaultAddress = ToInput(p.DefaultAddress),
            InvoiceAddress = ToInput(p.InvoiceAddress)
        };
    }

    public async Task<int?> SaveBillingProfileAsync(ClaimsPrincipal admin, BillingProfileAdminViewModel model, CancellationToken ct = default)
    {
        using var db = dbFactory.CreateDbContext();
        var (ok, current) = await AuthorizeAsync(db, admin, ct);
        if (!ok)
        {
            return null;
        }

        BillingProfile profile;
        if (model.BillingProfileId != 0)
        {
            profile = await db.BillingProfiles.IgnoreQueryFilters()
                .Include(x => x.DefaultAddress)
                .Include(x => x.InvoiceAddress)
                .FirstOrDefaultAsync(x => x.BillingProfileId == model.BillingProfileId && x.TenantId == current, ct);
            if (profile == null)
            {
                return null;
            }
        }
        else
        {
            var owner = await userManager.GetUserAsync(admin);
            profile = new BillingProfile { TenantId = current, OwnerUserId = owner?.Id };
            db.BillingProfiles.Add(profile);
        }

        profile.ProfileType = model.ProfileType;
        profile.FirstName = model.FirstName;
        profile.LastName = model.LastName;
        profile.CompanyName = model.CompanyName;
        profile.VatNumber = model.VatNumber;
        profile.Email = model.Email;
        profile.PhoneNumber = model.PhoneNumber;
        profile.UseInvoiceAddr = model.UseInvoiceAddr;

        var fallback = model.DisplayName;
        profile.DefaultAddress = Apply(profile.DefaultAddress, model.DefaultAddress, fallback);
        profile.InvoiceAddress = model.UseInvoiceAddr ? Apply(profile.InvoiceAddress, model.InvoiceAddress, fallback) : null;

        await db.SaveChangesAsync(ct);
        return profile.BillingProfileId;
    }

    public async Task<EmployeeViewModel[]> ListEmployeesAsync(ClaimsPrincipal admin, int billingProfileId, CancellationToken ct = default)
    {
        using var db = dbFactory.CreateDbContext();
        var (ok, current) = await AuthorizeAsync(db, admin, ct);
        if (!ok || !await ProfileInScopeAsync(db, billingProfileId, current, ct))
        {
            return Array.Empty<EmployeeViewModel>();
        }

        return await db.Employees.IgnoreQueryFilters().AsNoTracking()
            .Where(e => e.BillingProfileId == billingProfileId && e.TenantId == current)
            .OrderBy(e => e.EMail)
            .Select(e => new EmployeeViewModel
            {
                EmployeeId = e.EmployeeId,
                BillingProfileId = e.BillingProfileId,
                TenantId = e.TenantId,
                FirstName = e.FirstName,
                LastName = e.LastName,
                EMail = e.EMail,
                InvitationStatus = e.InvitationStatus
            })
            .ToArrayAsync(ct);
    }

    public async Task<int?> SaveEmployeeAsync(ClaimsPrincipal admin, EmployeeViewModel model, CancellationToken ct = default)
    {
        using var db = dbFactory.CreateDbContext();
        var (ok, current) = await AuthorizeAsync(db, admin, ct);
        if (!ok || !await ProfileInScopeAsync(db, model.BillingProfileId, current, ct))
        {
            return null;
        }

        Employee employee;
        if (model.EmployeeId != 0)
        {
            employee = await db.Employees.IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.EmployeeId == model.EmployeeId && e.TenantId == current, ct);
            if (employee == null)
            {
                return null;
            }
        }
        else
        {
            employee = new Employee { BillingProfileId = model.BillingProfileId, TenantId = current };
            db.Employees.Add(employee);
        }

        employee.FirstName = model.FirstName ?? "";
        employee.LastName = model.LastName ?? "";
        employee.EMail = model.EMail;
        employee.InvitationStatus = model.InvitationStatus;

        await db.SaveChangesAsync(ct);
        return employee.EmployeeId;
    }

    public async Task<bool> DeleteEmployeeAsync(ClaimsPrincipal admin, int employeeId, CancellationToken ct = default)
    {
        using var db = dbFactory.CreateDbContext();
        var (ok, current) = await AuthorizeAsync(db, admin, ct);
        if (!ok)
        {
            return false;
        }

        var employee = await db.Employees.IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId && e.TenantId == current, ct);
        if (employee == null)
        {
            return false;
        }

        var roles = await db.EmployeeRoles.IgnoreQueryFilters()
            .Where(er => er.EmployeeId == employeeId).ToListAsync(ct);
        db.EmployeeRoles.RemoveRange(roles);
        db.Employees.Remove(employee);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<EmployeeRoleAssignmentViewModel[]> ListEmployeeRolesAsync(ClaimsPrincipal admin, int employeeId, CancellationToken ct = default)
    {
        using var db = dbFactory.CreateDbContext();
        var (ok, current) = await AuthorizeAsync(db, admin, ct);
        if (!ok || !await EmployeeInScopeAsync(db, employeeId, current, ct))
        {
            return Array.Empty<EmployeeRoleAssignmentViewModel>();
        }

        var assigned = await db.EmployeeRoles.IgnoreQueryFilters().AsNoTracking()
            .Where(er => er.EmployeeId == employeeId)
            .Select(er => er.EmployeeRoleMappingId)
            .ToListAsync(ct);

        // Only DirectRole mappings are assignable to employees; PermissionSet mappings are composition blocks.
        return await db.EmployeeRoleMappings.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.TenantId == current && m.Kind == EmployeeRoleMappingKind.DirectRole)
            .OrderBy(m => m.Role.RoleName)
            .Select(m => new EmployeeRoleAssignmentViewModel
            {
                RoleId = m.EmployeeRoleMappingId,
                RoleName = m.Role.RoleName,
                Assigned = assigned.Contains(m.EmployeeRoleMappingId)
            })
            .ToArrayAsync(ct);
    }

    public async Task<bool> SetEmployeeRoleAsync(ClaimsPrincipal admin, int employeeId, int employeeRoleMappingId, bool assigned, CancellationToken ct = default)
    {
        using var db = dbFactory.CreateDbContext();
        var (ok, current) = await AuthorizeAsync(db, admin, ct);
        if (!ok || !await EmployeeInScopeAsync(db, employeeId, current, ct))
        {
            return false;
        }

        var mappingInScope = await db.EmployeeRoleMappings.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(m => m.EmployeeRoleMappingId == employeeRoleMappingId && m.TenantId == current
                           && m.Kind == EmployeeRoleMappingKind.DirectRole, ct);
        if (!mappingInScope)
        {
            return false;
        }

        var existing = await db.EmployeeRoles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(er => er.EmployeeId == employeeId && er.EmployeeRoleMappingId == employeeRoleMappingId, ct);

        if (assigned && existing == null)
        {
            db.EmployeeRoles.Add(new EmployeeRole { EmployeeId = employeeId, EmployeeRoleMappingId = employeeRoleMappingId });
            await db.SaveChangesAsync(ct);
        }
        else if (!assigned && existing != null)
        {
            db.EmployeeRoles.Remove(existing);
            await db.SaveChangesAsync(ct);
        }

        return true;
    }

    public async Task<EmployeeRoleMappingViewModel[]> ListRoleMappingsAsync(ClaimsPrincipal admin, CancellationToken ct = default)
    {
        using var db = dbFactory.CreateDbContext();
        var (ok, current) = await AuthorizeAsync(db, admin, ct);
        if (!ok)
        {
            return Array.Empty<EmployeeRoleMappingViewModel>();
        }

        return await db.EmployeeRoleMappings.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.TenantId == current)
            .OrderBy(m => m.Kind).ThenBy(m => m.Role.RoleName)
            .Select(m => new EmployeeRoleMappingViewModel
            {
                EmployeeRoleMappingId = m.EmployeeRoleMappingId,
                Kind = m.Kind,
                RoleId = m.RoleId,
                RoleName = m.Role.RoleName,
                DisplayNameJson = m.DisplayNameJson
            })
            .ToArrayAsync(ct);
    }

    public async Task<TenantRoleOption[]> ListTenantRolesAsync(ClaimsPrincipal admin, CancellationToken ct = default)
    {
        using var db = dbFactory.CreateDbContext();
        var (ok, current) = await AuthorizeAsync(db, admin, ct);
        if (!ok)
        {
            return Array.Empty<TenantRoleOption>();
        }

        return await db.SecurityRoles.AsNoTracking()
            .Where(r => r.TenantId == current)
            .OrderBy(r => r.RoleName)
            .Select(r => new TenantRoleOption { RoleId = r.RoleId, RoleName = r.RoleName })
            .ToArrayAsync(ct);
    }

    public async Task<int?> SaveRoleMappingAsync(ClaimsPrincipal admin, EmployeeRoleMappingViewModel model, CancellationToken ct = default)
    {
        using var db = dbFactory.CreateDbContext();
        var (ok, current) = await AuthorizeAsync(db, admin, ct);
        if (!ok)
        {
            return null;
        }

        EmployeeRoleMapping mapping;
        if (model.EmployeeRoleMappingId != 0)
        {
            mapping = await db.EmployeeRoleMappings.IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.EmployeeRoleMappingId == model.EmployeeRoleMappingId && m.TenantId == current, ct);
            if (mapping == null)
            {
                return null;
            }
        }
        else
        {
            var roleId = model.RoleId;
            if (roleId == 0)
            {
                if (string.IsNullOrWhiteSpace(model.NewRoleName))
                {
                    return null;
                }

                var existingRole = await db.SecurityRoles.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(r => r.TenantId == current && r.RoleName == model.NewRoleName, ct);
                if (existingRole != null)
                {
                    roleId = existingRole.RoleId;
                }
                else
                {
                    var role = new Role { TenantId = current, RoleName = model.NewRoleName, IsSystemRole = false };
                    db.SecurityRoles.Add(role);
                    await db.SaveChangesAsync(ct);
                    roleId = role.RoleId;
                }
            }
            else if (!await db.SecurityRoles.AsNoTracking().AnyAsync(r => r.RoleId == roleId && r.TenantId == current, ct))
            {
                return null;
            }

            mapping = new EmployeeRoleMapping { TenantId = current, RoleId = roleId };
            db.EmployeeRoleMappings.Add(mapping);
        }

        mapping.Kind = model.Kind;
        mapping.DisplayNameJson = model.DisplayNameJson;
        await db.SaveChangesAsync(ct);
        return mapping.EmployeeRoleMappingId;
    }

    public async Task<bool> DeleteRoleMappingAsync(ClaimsPrincipal admin, int employeeRoleMappingId, CancellationToken ct = default)
    {
        using var db = dbFactory.CreateDbContext();
        var (ok, current) = await AuthorizeAsync(db, admin, ct);
        if (!ok)
        {
            return false;
        }

        var mapping = await db.EmployeeRoleMappings.IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.EmployeeRoleMappingId == employeeRoleMappingId && m.TenantId == current, ct);
        if (mapping == null)
        {
            return false;
        }

        // The EmployeeRole→mapping FK is non-cascading, so remove the assignments first (this also lets the
        // materialization interceptor drop the derived UserRoles). The underlying role is left in place.
        var assignments = await db.EmployeeRoles.IgnoreQueryFilters()
            .Where(er => er.EmployeeRoleMappingId == employeeRoleMappingId).ToListAsync(ct);
        db.EmployeeRoles.RemoveRange(assignments);
        db.EmployeeRoleMappings.Remove(mapping);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PermissionSetActivationViewModel[]> ListActivatablePermissionSetsAsync(ClaimsPrincipal admin, int directMappingId, CancellationToken ct = default)
    {
        using var db = dbFactory.CreateDbContext();
        var (ok, current) = await AuthorizeAsync(db, admin, ct);
        if (!ok)
        {
            return Array.Empty<PermissionSetActivationViewModel>();
        }

        var direct = await db.EmployeeRoleMappings.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(m => m.EmployeeRoleMappingId == directMappingId && m.TenantId == current, ct);
        if (direct == null)
        {
            return Array.Empty<PermissionSetActivationViewModel>();
        }

        var activeRoleIds = await db.RoleRoles.AsNoTracking()
            .Where(rr => rr.PermissiveRoleId == direct.RoleId)
            .Select(rr => rr.PermittedRoleId)
            .ToListAsync(ct);

        return await db.EmployeeRoleMappings.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.TenantId == current && m.Kind == EmployeeRoleMappingKind.PermissionSet)
            .OrderBy(m => m.Role.RoleName)
            .Select(m => new PermissionSetActivationViewModel
            {
                PermissionSetMappingId = m.EmployeeRoleMappingId,
                Label = m.Role.RoleName,
                Active = activeRoleIds.Contains(m.RoleId)
            })
            .ToArrayAsync(ct);
    }

    public async Task<bool> SetPermissionSetActivationAsync(ClaimsPrincipal admin, int directMappingId, int permissionSetMappingId, bool active, CancellationToken ct = default)
    {
        using var db = dbFactory.CreateDbContext();
        var (ok, current) = await AuthorizeAsync(db, admin, ct);
        if (!ok)
        {
            return false;
        }

        var direct = await db.EmployeeRoleMappings.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(m => m.EmployeeRoleMappingId == directMappingId && m.TenantId == current
                                      && m.Kind == EmployeeRoleMappingKind.DirectRole, ct);
        var set = await db.EmployeeRoleMappings.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(m => m.EmployeeRoleMappingId == permissionSetMappingId && m.TenantId == current
                                      && m.Kind == EmployeeRoleMappingKind.PermissionSet, ct);
        if (direct == null || set == null)
        {
            return false;
        }

        var existing = await db.RoleRoles
            .FirstOrDefaultAsync(rr => rr.PermissiveRoleId == direct.RoleId && rr.PermittedRoleId == set.RoleId, ct);

        if (active && existing == null)
        {
            db.RoleRoles.Add(new RoleRole { PermissiveRoleId = direct.RoleId, PermittedRoleId = set.RoleId });
            await db.SaveChangesAsync(ct);
        }
        else if (!active && existing != null)
        {
            db.RoleRoles.Remove(existing);
            await db.SaveChangesAsync(ct);
        }

        return true;
    }

    /// <summary>
    /// Resolves the current scope tenant and enforces the admin gate: a non-zero <c>CurrentTenantId</c>,
    /// the <c>ManageEmployees</c> permission, and an enabled membership of the caller in that tenant.
    /// </summary>
    private async Task<(bool ok, int tenantId)> AuthorizeAsync(TContext db, ClaimsPrincipal admin, CancellationToken ct)
    {
        var current = db.CurrentTenantId ?? 0;
        if (current == 0 || !services.VerifyUserPermissions(new[] { OnboardingAdminPermissions.ManageEmployees }))
        {
            return (false, 0);
        }

        var owner = await userManager.GetUserAsync(admin);
        if (owner == null)
        {
            return (false, 0);
        }

        var member = await db.TenantUsers.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(tu => tu.UserId == owner.Id && tu.TenantId == current && tu.Enabled == true, ct);
        return (member, current);
    }

    private Task<bool> ProfileInScopeAsync(TContext db, int billingProfileId, int current, CancellationToken ct)
        => db.BillingProfiles.IgnoreQueryFilters().AnyAsync(p => p.BillingProfileId == billingProfileId && p.TenantId == current, ct);

    private Task<bool> EmployeeInScopeAsync(TContext db, int employeeId, int current, CancellationToken ct)
        => db.Employees.IgnoreQueryFilters().AnyAsync(e => e.EmployeeId == employeeId && e.TenantId == current, ct);

    private static AddressInput ToInput(Address? address) => address == null
        ? new AddressInput()
        : new AddressInput
        {
            Name = address.Name,
            Addition1 = address.Addition1,
            Addition2 = address.Addition2,
            Street = address.Street,
            Number = address.Number,
            Zip = address.Zip,
            City = address.City
        };

    /// <summary>Updates the existing address row in place, or creates a new typed one when absent.</summary>
    private static TAddress Apply<TAddress>(TAddress? existing, AddressInput input, string fallbackName)
        where TAddress : Address, new()
    {
        var target = existing ?? new TAddress();
        target.Name = string.IsNullOrWhiteSpace(input.Name) ? fallbackName : input.Name;
        target.Addition1 = input.Addition1;
        target.Addition2 = input.Addition2;
        target.Street = input.Street;
        target.Number = input.Number;
        target.Zip = input.Zip ?? string.Empty;
        target.City = input.City ?? string.Empty;
        return target;
    }
}
