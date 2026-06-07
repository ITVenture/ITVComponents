using System.Security.Claims;
using ITVComponents.Json;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Tree;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Tree.Models;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Handlers.Impl;

/// <summary>
/// Hierarchy-strategy onboarding handler. Operates against
/// <c>IHierarchySecurityContextWithOnboarding</c> (i.e. <c>AspNetCoreTreeTenants</c> +
/// <c>TreeCustomerOnboarding</c>). Compared to the flat handler, the create-tenant flow
/// honours <see cref="BillingProfileViewModel.ParentTenantId"/> so a new tenant can be
/// attached as a sub-tenant of an existing one the user is already a member of.
/// </summary>
public class HierarchyOnboardingHandler<TContext> : IOnboardingHandler
    where TContext : DbContext, IHierarchySecurityContextWithOnboarding
{
    private readonly TContext db;
    private readonly UserManager<User> userManager;
    private readonly IGlobalSettings<TenantSetupOptions> setupOptions;
    private readonly ITenantTemplateHelper<HierarchyTenant, HierarchyWebPlugin, HierarchyWebPluginConstant,
        HierarchyWebPluginGenericParameter, HierarchySequence, HierarchyTenantSetting,
        HierarchyTenantFeatureActivation, HierarchyExternalOAuthService, HierarchyExternalOAuthServiceState,
        HierarchyExternalOAuthServiceTenantLogin, HierarchyTenantContextSecurityTrustConfig> tenantInitializer;
    private readonly ILogger<HierarchyOnboardingHandler<TContext>> logger;

    public HierarchyOnboardingHandler(
        TContext db,
        UserManager<User> userManager,
        IGlobalSettings<TenantSetupOptions> setupOptions,
        ITenantTemplateHelper<HierarchyTenant, HierarchyWebPlugin, HierarchyWebPluginConstant,
            HierarchyWebPluginGenericParameter, HierarchySequence, HierarchyTenantSetting,
            HierarchyTenantFeatureActivation, HierarchyExternalOAuthService, HierarchyExternalOAuthServiceState,
            HierarchyExternalOAuthServiceTenantLogin, HierarchyTenantContextSecurityTrustConfig> tenantInitializer,
        ILogger<HierarchyOnboardingHandler<TContext>> logger)
    {
        this.db = db;
        this.userManager = userManager;
        this.setupOptions = setupOptions;
        this.tenantInitializer = tenantInitializer;
        this.logger = logger;
    }

    public bool UseHierarchy => true;

    public OnboardingParentPolicy ParentPolicy
    {
        get
        {
            var cfg = setupOptions.ValueOrDefault;
            var hasDefault = !string.IsNullOrEmpty(cfg?.DefaultParentTenant);
            var allowRoot = cfg?.AllowRootTenantCreation ?? true;
            // A forced default parent hides the picker (auto-assigned). Otherwise show it; a pick is only
            // mandatory when roots are disallowed and there is no default to fall back to.
            return new OnboardingParentPolicy(ShowPicker: !hasDefault, ParentRequired: !allowRoot && !hasDefault);
        }
    }

    public async Task<int?> CreateTenantAsync(ClaimsPrincipal user, BillingProfileViewModel input, CancellationToken ct = default)
    {
        var owner = await userManager.GetUserAsync(user);
        if (owner == null)
        {
            return null;
        }

        // Parent precedence: explicit pick (or, later, an invitation) wins; otherwise fall back to the
        // configured DefaultParentTenant. If still none and roots are disabled, reject onboarding so no
        // further root tenants can be created.
        var parentId = await ResolveParentTenantIdAsync(input.ParentTenantId, ct);
        if (parentId == null && !(setupOptions.ValueOrDefault?.AllowRootTenantCreation ?? true))
        {
            logger.LogWarning("Onboarding rejected: root-tenant creation is disabled but no parent could be resolved.");
            return null;
        }

        var displayName = input.ProfileType == ProfileType.Company
            ? input.CompanyName ?? owner.Email ?? owner.UserName ?? ""
            : $"{input.FirstName} {input.LastName}".Trim();

        var tenant = new HierarchyTenant
        {
            DisplayName = displayName,
            TenantName = Guid.NewGuid().ToString("N"),
            TenantPassword = Convert.ToBase64String(AesEncryptor.CreateKey()),
            ParentTenantId = parentId
        };
        db.Tenants.Add(tenant);

        var admin = new HierarchyTenantUser { Enabled = true, Tenant = tenant, UserId = owner.Id };
        db.TenantUsers.Add(admin);

        var profile = new HierarchyBillingProfile
        {
            ProfileType = input.ProfileType,
            FirstName = input.FirstName,
            LastName = input.LastName,
            CompanyName = input.CompanyName,
            VatNumber = input.VatNumber,
            Email = input.Email,
            PhoneNumber = input.PhoneNumber,
            OwnerUserId = owner.Id,
            Tenant = tenant,
            Admin = admin,
            UseInvoiceAddr = input.UseInvoiceAddr,
            DefaultAddress = MapAddress<HierarchyDefaultAddress>(input.DefaultAddress, fallbackName: displayName),
            InvoiceAddress = input.UseInvoiceAddr ? MapAddress<HierarchyInvoiceAddress>(input.InvoiceAddress, fallbackName: displayName) : null
        };
        db.BillingProfiles.Add(profile);

        if (input.ProfileType == ProfileType.Company)
        {
            db.Employees.Add(new HierarchyEmployee
            {
                InvitationStatus = InvitationStatus.Committed,
                EMail = input.Email,
                BillingProfile = profile,
                User = owner,
                TenantUser = admin,
                FirstName = "Admin",
                LastName = "Admin",
                Tenant = tenant
            });
        }

        await db.SaveChangesAsync(ct);

        await ApplyTenantTemplateAsync(tenant, admin, ct);

        return profile.BillingProfileId;
    }

    public async Task<ParticipatingTenantViewModel[]> ListMyTenantsAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var owner = await userManager.GetUserAsync(user);
        if (owner == null)
        {
            return Array.Empty<ParticipatingTenantViewModel>();
        }

        var ownedPersonal = await db.BillingProfiles.AsNoTracking()
            .Where(p => p.OwnerUserId == owner.Id && p.ProfileType == ProfileType.Personal)
            .Select(p => new ParticipatingTenantViewModel
            {
                BillingProfileId = p.BillingProfileId,
                ProfileType = p.ProfileType,
                Name = (p.FirstName ?? "") + " " + (p.LastName ?? ""),
                DefaultEmail = p.Email,
                StatusText = nameof(InvitationStatus.Committed),
                Status = InvitationStatus.Committed,
                TenantCreated = p.TenantId != null
            })
            .ToListAsync(ct);

        var employeeBased = await (from p in db.BillingProfiles.AsNoTracking()
            join e in db.Employees.AsNoTracking()
                on new { p.BillingProfileId, Email = owner.Email } equals new { e.BillingProfileId, Email = e.EMail }
            select new ParticipatingTenantViewModel
            {
                BillingProfileId = p.BillingProfileId,
                ProfileType = p.ProfileType,
                Name = p.DefaultAddress != null ? p.DefaultAddress.Name : (p.CompanyName ?? ""),
                DefaultEmail = p.Email,
                StatusText = e.InvitationStatus.ToString(),
                Status = e.InvitationStatus,
                TenantCreated = p.TenantId != null
            }).ToListAsync(ct);

        return ownedPersonal.Concat(employeeBased)
            .GroupBy(v => v.BillingProfileId)
            .Select(g => g.First())
            .ToArray();
    }

    public async Task<bool> AcceptInvitationAsync(ClaimsPrincipal user, int billingProfileId, CancellationToken ct = default)
    {
        var owner = await userManager.GetUserAsync(user);
        if (owner == null)
        {
            return false;
        }

        var employee = await (from e in db.Employees
            where e.BillingProfileId == billingProfileId
                  && e.EMail == owner.Email
                  && e.InvitationStatus == InvitationStatus.Pending
            select e).FirstOrDefaultAsync(ct);
        if (employee == null)
        {
            return false;
        }

        var tenant = employee.Tenant;
        if (!await db.TenantUsers.AnyAsync(n => n.UserId == owner.Id && n.TenantId == tenant.TenantId, ct))
        {
            var tu = new HierarchyTenantUser
            {
                Enabled = true,
                Tenant = tenant,
                User = owner
            };
            db.TenantUsers.Add(tu);

            var rolesForEmployee = await (from r in db.SecurityRoles
                join er in db.EmployeeRoles on r.RoleId equals er.RoleId
                where r.TenantId == tenant.TenantId && er.EmployeeId == employee.EmployeeId
                select r).ToListAsync(ct);
            foreach (var role in rolesForEmployee)
            {
                db.TenantUserRoles.Add(new UserRole { Role = role, User = tu });
            }

            employee.User = owner;
            employee.TenantUser = tu;
        }

        employee.InvitationStatus = InvitationStatus.Committed;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<TenantPickerItem[]> ListEligibleParentsAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var owner = await userManager.GetUserAsync(user);
        if (owner == null)
        {
            return Array.Empty<TenantPickerItem>();
        }

        return await (from tu in db.TenantUsers.AsNoTracking()
            join t in db.Tenants.AsNoTracking() on tu.TenantId equals t.TenantId
            where tu.UserId == owner.Id && tu.Enabled == true
            orderby t.DisplayName, t.TenantName
            select new TenantPickerItem(t.TenantId, t.DisplayName ?? t.TenantName))
            .ToArrayAsync(ct);
    }

    private async Task ApplyTenantTemplateAsync(HierarchyTenant tenant, HierarchyTenantUser admin, CancellationToken ct)
    {
        var cfg = setupOptions.ValueOrDefault;
        if (cfg == null || string.IsNullOrEmpty(cfg.BasicTenantTemplate))
        {
            return;
        }

        var tmpl = await db.TenantTemplates.FirstOrDefaultAsync(n => n.Name == cfg.BasicTenantTemplate, ct);
        if (tmpl == null)
        {
            logger.LogWarning("Tenant template {Template} not found; skipping.", cfg.BasicTenantTemplate);
            return;
        }

        var markup = JsonHelper.FromJsonString<TenantTemplateMarkup>(tmpl.Markup, SerializationTypingMode.NativePolymorphism);
        tenantInitializer.ApplyTemplate(tenant, markup, baseCtx =>
        {
            if (baseCtx is not IHierarchySecurityContextWithOnboarding ctx) return;
            if (string.IsNullOrEmpty(cfg.AdminUserRole)) return;
            var role = ctx.SecurityRoles.FirstOrDefault(n => n.TenantId == tenant.TenantId && n.RoleName == cfg.AdminUserRole);
            if (role == null) return;
            ctx.TenantUserRoles.Add(new UserRole { Role = role, User = admin });
            ctx.SaveChanges();
        });
    }

    /// <summary>
    /// Resolves the parent tenant for a new tenant: an explicitly requested parent wins; otherwise the
    /// DB-configured <c>DefaultParentTenant</c> (matched by TenantName, fallback DisplayName) is used.
    /// Returns null when neither applies (root tenant — caller decides whether that is permitted).
    /// </summary>
    private async Task<int?> ResolveParentTenantIdAsync(int? requested, CancellationToken ct)
    {
        if (requested != null)
        {
            return requested;
        }

        var name = setupOptions.ValueOrDefault?.DefaultParentTenant;
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        var parentId = await db.Tenants.AsNoTracking()
            .Where(t => t.TenantName == name || t.DisplayName == name)
            .Select(t => (int?)t.TenantId)
            .FirstOrDefaultAsync(ct);
        if (parentId == null)
        {
            logger.LogWarning("Configured DefaultParentTenant '{Name}' was not found; new tenant gets no parent.", name);
        }
        return parentId;
    }

    private static TAddress? MapAddress<TAddress>(AddressInput input, string fallbackName)
        where TAddress : HierarchyAddress, new()
    {
        if (input == null) return null;
        return new TAddress
        {
            Name = string.IsNullOrWhiteSpace(input.Name) ? fallbackName : input.Name,
            Addition1 = input.Addition1,
            Addition2 = input.Addition2,
            Street = input.Street,
            Number = input.Number,
            Zip = input.Zip ?? string.Empty,
            City = input.City ?? string.Empty
        };
    }
}
