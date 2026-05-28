using System.Security.Claims;
using ITVComponents.Json;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Flat;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Flat.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.FlatTenantModels;
using ITVComponents.WebCoreToolkit.OnboardingViews.Blazor.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.OnboardingViews.Blazor.Handlers.Impl;

/// <summary>
/// Flat-strategy onboarding handler. Operates against <c>ISecurityContextWithOnboarding</c>
/// (i.e. <c>AspNetCoreTenants</c> + <c>CustomerOnboarding</c>). Mirrors the behaviour of the
/// Telerik COB <c>RegistrationController.CreateCompany</c> / <c>CreateTenant.cshtml.cs</c>
/// flows but ProfileType-aware: personal tenants get no employee row, company tenants get
/// the owner as employee #1.
/// </summary>
public class OnboardingHandler<TContext> : IOnboardingHandler
    where TContext : DbContext, ISecurityContextWithOnboarding
{
    private readonly TContext db;
    private readonly UserManager<User> userManager;
    private readonly IGlobalSettings<TenantSetupOptions> setupOptions;
    private readonly ITenantTemplateHelper<Tenant, FlatWebPlugin, FlatWebPluginConstant,
        FlatWebPluginGenericParameter, FlatSequence, FlatTenantSetting,
        FlatTenantFeatureActivation, FlatExternalOAuthService, FlatExternalOAuthServiceState,
        FlatExternalOAuthServiceTenantLogin, BaseTenantContextSecurityTrustConfig> tenantInitializer;
    private readonly ILogger<OnboardingHandler<TContext>> logger;

    public OnboardingHandler(
        TContext db,
        UserManager<User> userManager,
        IGlobalSettings<TenantSetupOptions> setupOptions,
        ITenantTemplateHelper<Tenant, FlatWebPlugin, FlatWebPluginConstant,
            FlatWebPluginGenericParameter, FlatSequence, FlatTenantSetting,
            FlatTenantFeatureActivation, FlatExternalOAuthService, FlatExternalOAuthServiceState,
            FlatExternalOAuthServiceTenantLogin, BaseTenantContextSecurityTrustConfig> tenantInitializer,
        ILogger<OnboardingHandler<TContext>> logger)
    {
        this.db = db;
        this.userManager = userManager;
        this.setupOptions = setupOptions;
        this.tenantInitializer = tenantInitializer;
        this.logger = logger;
    }

    public bool UseHierarchy => false;

    public async Task<int?> CreateTenantAsync(ClaimsPrincipal user, BillingProfileViewModel input, CancellationToken ct = default)
    {
        var owner = await userManager.GetUserAsync(user);
        if (owner == null)
        {
            return null;
        }

        var displayName = input.ProfileType == ProfileType.Company
            ? input.CompanyName ?? owner.Email ?? owner.UserName ?? ""
            : $"{input.FirstName} {input.LastName}".Trim();

        var tenant = new Tenant
        {
            DisplayName = displayName,
            TenantName = Guid.NewGuid().ToString("N"),
            TenantPassword = Convert.ToBase64String(AesEncryptor.CreateKey())
        };
        db.Tenants.Add(tenant);

        var admin = new TenantUser { Enabled = true, Tenant = tenant, UserId = owner.Id };
        db.TenantUsers.Add(admin);

        var profile = new BillingProfile
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
            DefaultAddress = MapAddress<DefaultAddress>(input.DefaultAddress, fallbackName: displayName),
            InvoiceAddress = input.UseInvoiceAddr ? MapAddress<InvoiceAddress>(input.InvoiceAddress, fallbackName: displayName) : null
        };
        db.BillingProfiles.Add(profile);

        if (input.ProfileType == ProfileType.Company)
        {
            db.Employees.Add(new Employee
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
            var tu = new TenantUser
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

    public Task<TenantPickerItem[]> ListEligibleParentsAsync(ClaimsPrincipal user, CancellationToken ct = default)
        => Task.FromResult(Array.Empty<TenantPickerItem>());

    private async Task ApplyTenantTemplateAsync(Tenant tenant, TenantUser admin, CancellationToken ct)
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
            if (baseCtx is not ISecurityContextWithOnboarding ctx) return;
            if (string.IsNullOrEmpty(cfg.AdminUserRole)) return;
            var role = ctx.SecurityRoles.FirstOrDefault(n => n.TenantId == tenant.TenantId && n.RoleName == cfg.AdminUserRole);
            if (role == null) return;
            ctx.TenantUserRoles.Add(new UserRole { Role = role, User = admin });
            ctx.SaveChanges();
        });
    }

    private static TAddress? MapAddress<TAddress>(AddressInput input, string fallbackName)
        where TAddress : Address, new()
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
