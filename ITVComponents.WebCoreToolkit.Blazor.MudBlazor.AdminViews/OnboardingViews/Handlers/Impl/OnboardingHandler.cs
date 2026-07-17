using System.Security.Claims;
using ITVComponents.Json;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Flat;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Flat.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.FlatTenantModels;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Handlers.Impl;

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
    private readonly IDbContextFactory<TContext> dbFactory;
    private readonly UserManager<User> userManager;
    private readonly IGlobalSettings<TenantSetupOptions> setupOptions;
    private readonly ITenantTemplateHelper<Tenant, FlatWebPlugin, FlatWebPluginConstant,
        FlatWebPluginGenericParameter, FlatSequence, FlatTenantSetting,
        FlatTenantFeatureActivation, FlatExternalOAuthService, FlatExternalOAuthServiceState,
        FlatExternalOAuthServiceTenantLogin, BaseTenantContextSecurityTrustConfig> tenantInitializer;
    private readonly ILogger<OnboardingHandler<TContext>> logger;

    public OnboardingHandler(
        IDbContextFactory<TContext> dbFactory,
        UserManager<User> userManager,
        IGlobalSettings<TenantSetupOptions> setupOptions,
        ITenantTemplateHelper<Tenant, FlatWebPlugin, FlatWebPluginConstant,
            FlatWebPluginGenericParameter, FlatSequence, FlatTenantSetting,
            FlatTenantFeatureActivation, FlatExternalOAuthService, FlatExternalOAuthServiceState,
            FlatExternalOAuthServiceTenantLogin, BaseTenantContextSecurityTrustConfig> tenantInitializer,
        ILogger<OnboardingHandler<TContext>> logger)
    {
        this.dbFactory = dbFactory;
        this.userManager = userManager;
        this.setupOptions = setupOptions;
        this.tenantInitializer = tenantInitializer;
        this.logger = logger;
    }

    public bool UseHierarchy => false;

    public OnboardingParentPolicy ParentPolicy => new(false, false);

    public async Task<int?> CreateTenantAsync(ClaimsPrincipal user, BillingProfileViewModel input, CancellationToken ct = default)
    {
        // Standalone entry point (no pending record): create the tenant atomically on its own context + transaction,
        // so tenant/admin/profile creation and the template application commit or roll back together.
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var created = await CreateOrResumeTenantAsync(user, input, db, null, ct);
            if (created == null)
            {
                return (int?)null;
            }

            await tx.CommitAsync(ct);
            return created.Value.billingProfileId;
        });
    }

    /// <summary>
    /// Core tenant-creation logic that runs on the SUPPLIED, caller-owned context (so it participates in the caller's
    /// transaction) and does NOT commit. When <paramref name="resumeTenantId"/> is set, a tenant was already created
    /// for this onboarding by a previous attempt — reuse it (idempotent template re-apply) instead of creating a
    /// duplicate; a stale marker falls through to fresh creation. Returns the tenant id + billing-profile id, or null
    /// on failure (the caller rolls back).
    /// </summary>
    private async Task<(int tenantId, int billingProfileId)?> CreateOrResumeTenantAsync(ClaimsPrincipal user,
        BillingProfileViewModel input, TContext db, int? resumeTenantId, CancellationToken ct)
    {
        var owner = await userManager.GetUserAsync(user);
        if (owner == null)
        {
            return null;
        }

        if (resumeTenantId is int rid)
        {
            var resumed = await TryResumeTenantAsync(db, owner, rid, ct);
            if (resumed != null)
            {
                return resumed;
            }
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
                // FK scalar, not the navigation: 'owner' is tracked by the userManager's context, not by this
                // per-operation 'db'. Assigning it as a navigation makes EF treat it as a new principal and emit
                // INSERT INTO Users → PK violation. The user already exists, so only the FK is needed.
                UserId = owner.Id,
                TenantUser = admin,
                FirstName = "Admin",
                LastName = "Admin",
                Tenant = tenant
            });
        }

        await db.SaveChangesAsync(ct);

        await ApplyTenantTemplateAsync(db, tenant, admin, ct);

        return (tenant.TenantId, profile.BillingProfileId);
    }

    /// <summary>
    /// Resumes onboarding on an already-created tenant: re-applies the (idempotent) template. Returns null when the
    /// marker is stale (tenant/admin/profile no longer present), so the caller falls back to a fresh creation.
    /// </summary>
    private async Task<(int tenantId, int billingProfileId)?> TryResumeTenantAsync(TContext db, User owner, int tenantId, CancellationToken ct)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId, ct);
        var admin = await db.TenantUsers.FirstOrDefaultAsync(tu => tu.TenantId == tenantId && tu.UserId == owner.Id, ct);
        var profile = await db.BillingProfiles.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.OwnerUserId == owner.Id, ct);
        if (tenant == null || admin == null || profile == null)
        {
            logger.LogWarning("Onboarding resume marker points at tenant {TenantId} but its tenant/admin/profile is incomplete; creating fresh.", tenantId);
            return null;
        }

        await ApplyTenantTemplateAsync(db, tenant, admin, ct);
        return (tenant.TenantId, profile.BillingProfileId);
    }

    public async Task<OnboardingStartResult> StartOnboardingAsync(OnboardingStartInput input, CancellationToken ct = default)
    {
        using var db = dbFactory.CreateDbContext();
        return await OnboardingPendingHelper.StartAsync(db, userManager, input, ct);
    }

    public Task<OnboardingStartResult> RegisterAccountAsync(string email, string password, CancellationToken ct = default)
        => OnboardingPendingHelper.RegisterAccountAsync(userManager, email, password);

    public async Task<bool> IsEmailConfirmedAsync(string email, CancellationToken ct = default)
    {
        // Read on a FRESH, no-tracking per-operation context. The confirmation is committed in a different scope
        // (the ConfirmEmail tab); the injected circuit-scoped userManager caches the user it loaded on the first
        // poll, and EF never refreshes a tracked entity's scalars on re-query, so FindByEmailAsync would report
        // EmailConfirmed=false forever and the waiting page would poll endlessly without ever advancing.
        var normalized = userManager.NormalizeEmail(email);
        using var db = dbFactory.CreateDbContext();
        return await db.Set<User>().AsNoTracking()
            .AnyAsync(u => u.NormalizedEmail == normalized && u.EmailConfirmed, ct);
    }

    public async Task StoreJoinNonceAsync(string email, string nonce, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user != null)
        {
            await userManager.SetAuthenticationTokenAsync(user, "Onboarding", "JoinNonce", nonce);
        }
    }

    public async Task<bool> CompletePendingOnboardingAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        // One context + one transaction for the whole flow: consuming the pending record, creating the tenant and
        // applying the template all commit or roll back together (see OnboardingPendingHelper.CompleteAsync). The
        // core runs on the shared context and does not commit; CompleteAsync owns the transaction.
        return await OnboardingPendingHelper.CompleteAsync(dbFactory, userManager, user, CreateOrResumeTenantAsync, ct, logger);
    }

    public async Task<ParticipatingTenantViewModel[]> ListMyTenantsAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var owner = await userManager.GetUserAsync(user);
        if (owner == null)
        {
            return Array.Empty<ParticipatingTenantViewModel>();
        }

        using var db = dbFactory.CreateDbContext();

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

        using var db = dbFactory.CreateDbContext();

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
                // FK scalar, not the navigation: 'owner' belongs to the userManager's context. Attaching it to
                // this per-operation 'db' as a navigation would make EF re-INSERT the existing user (PK violation).
                UserId = owner.Id
            };
            db.TenantUsers.Add(tu);

            var rolesForEmployee = await (from er in db.EmployeeRoles
                join m in db.EmployeeRoleMappings on er.EmployeeRoleMappingId equals m.EmployeeRoleMappingId
                join r in db.SecurityRoles on m.RoleId equals r.RoleId
                where er.EmployeeId == employee.EmployeeId && r.TenantId == tenant.TenantId
                select r).ToListAsync(ct);
            foreach (var role in rolesForEmployee)
            {
                db.TenantUserRoles.Add(new UserRole { Role = role, User = tu });
            }

            employee.UserId = owner.Id;
            employee.TenantUser = tu;
        }

        employee.InvitationStatus = InvitationStatus.Committed;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public Task<TenantPickerItem[]> ListEligibleParentsAsync(ClaimsPrincipal user, CancellationToken ct = default)
        => Task.FromResult(Array.Empty<TenantPickerItem>());

    private async Task ApplyTenantTemplateAsync(TContext db, Tenant tenant, TenantUser admin, CancellationToken ct)
    {
        var cfg = setupOptions.ValueOrDefault;

        var resolved = await OnboardingTemplateResolver.ResolveAsync(db.TenantTypes, db.TenantTemplates, cfg,
            templateNameOverride: null, logger, ct);
        if (resolved == null)
        {
            return;
        }

        // Tag the tenant with the resolved TenantType (BasicTenantType path) unless it already carries one, so a later
        // re-apply can resolve "the tenant's template" from its type.
        if (resolved.TenantTypeId != null && tenant.TenantTypeId == null)
        {
            tenant.TenantTypeId = resolved.TenantTypeId;
        }

        var markup = resolved.Markup;
        // Apply on the SAME context 'db' (external-context overload) so the template writes enlist in the caller's
        // transaction instead of a separately-leased context that would commit independently.
        tenantInitializer.ApplyTemplate(db, tenant, markup, baseCtx =>
        {
            if (baseCtx is not ISecurityContextWithOnboarding ctx) return;
            if (string.IsNullOrEmpty(cfg.AdminUserRole)) return;
            var role = ctx.SecurityRoles.FirstOrDefault(n => n.TenantId == tenant.TenantId && n.RoleName == cfg.AdminUserRole);
            if (role == null) return;
            // FK scalar, not the navigation: 'admin' was saved above, so its PK is populated. Assigning the scalar
            // keeps this correct no matter which context tracks 'admin' — via a navigation, a context that does not
            // track it would treat it (and its Tenant) as new principals and emit INSERTs with explicit identity
            // values → "Cannot insert explicit value for identity column". 'role' is read from 'ctx', so its
            // navigation is safe.
            ctx.TenantUserRoles.Add(new UserRole { Role = role, TenantUserId = admin.TenantUserId });
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
