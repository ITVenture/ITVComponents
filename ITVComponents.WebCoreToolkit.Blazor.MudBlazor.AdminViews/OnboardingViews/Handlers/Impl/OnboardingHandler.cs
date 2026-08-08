using System.Security.Claims;
using ITVComponents.Json;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Flat;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Flat.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Consent;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Extensibility;
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
    private readonly ICustomCompanyInfoProvider customInfo;
    private readonly IConsentProvider consent;
    private readonly ILogger<OnboardingHandler<TContext>> logger;

    public OnboardingHandler(
        IDbContextFactory<TContext> dbFactory,
        UserManager<User> userManager,
        IGlobalSettings<TenantSetupOptions> setupOptions,
        ITenantTemplateHelper<Tenant, FlatWebPlugin, FlatWebPluginConstant,
            FlatWebPluginGenericParameter, FlatSequence, FlatTenantSetting,
            FlatTenantFeatureActivation, FlatExternalOAuthService, FlatExternalOAuthServiceState,
            FlatExternalOAuthServiceTenantLogin, BaseTenantContextSecurityTrustConfig> tenantInitializer,
        ICustomCompanyInfoProvider customInfo,
        IConsentProvider consent,
        ILogger<OnboardingHandler<TContext>> logger)
    {
        this.dbFactory = dbFactory;
        this.userManager = userManager;
        this.setupOptions = setupOptions;
        this.tenantInitializer = tenantInitializer;
        this.customInfo = customInfo;
        this.consent = consent;
        this.logger = logger;
    }

    public bool UseHierarchy => false;

    public OnboardingParentPolicy ParentPolicy => new(false, false);

    public async Task<int?> CreateTenantAsync(ClaimsPrincipal user, BillingProfileViewModel input, CancellationToken ct = default)
    {
        // Die Zusatzangaben werden VOR der Anlage geprueft: entstuende der Tenant erst und wuerde dann
        // beanstandet, muesste er wieder weg - und genau das laesst sich mit den Modulen, die in fremde
        // Ablagen schreiben, nicht sauber zuruecknehmen.
        var infoCtx = CustomCompanyInfoOnboardingHelper.ContextFor(input, CustomInfoMode.Create, null, user);
        if (!await CustomCompanyInfoOnboardingHelper.AcceptsAsync(customInfo, input, infoCtx, logger, ct))
        {
            return null;
        }

        // Standalone entry point (no pending record): create the tenant atomically on its own context + transaction,
        // so tenant/admin/profile creation and the template application commit or roll back together.
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var strategy = db.Database.CreateExecutionStrategy();
        var created = await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var result = await CreateOrResumeTenantAsync(user, input, db, null, ct);
            if (result == null)
            {
                return ((int tenantId, int billingProfileId)?)null;
            }

            await tx.CommitAsync(ct);
            return result;
        });

        if (created == null)
        {
            return null;
        }

        // Erst nach dem Commit: vorher gibt es die TenantId nicht, an der die Angaben haengen.
        await CustomCompanyInfoOnboardingHelper.PersistAsync(customInfo, input, infoCtx, created.Value.tenantId, logger, ct);

        // Ebenso der Zustimmungs-Nachweis. Er haengt an der TenantId und nicht an der zurueckgelieferten
        // BillingProfileId - die beiden werden hier leicht verwechselt.
        var owner = await userManager.GetUserAsync(user);
        await ConsentOnboardingHelper.RecordAsync(consent, input, created.Value.tenantId, owner?.Id, owner?.Email, logger, ct);
        return created.Value.billingProfileId;
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
        PendingCompletion completion = await OnboardingPendingHelper.CompleteAsync(dbFactory, userManager, user, CreateOrResumeTenantAsync, ct, logger);
        if (!completion.Completed)
        {
            return false;
        }

        // Hier wird bewusst NICHT mehr geprueft und nichts mehr abgelehnt: der Benutzer hat gerade seine
        // Mailadresse bestaetigt und kann nichts mehr eingeben. Wuerde ein inzwischen hinzugekommenes
        // Pflicht-Modul die Fertigstellung blockieren, saesse er dauerhaft fest. Was fehlt, meldet der
        // Provider - nachtragen laesst es sich im Firmenprofil.
        await CustomCompanyInfoOnboardingHelper.PersistAsync(customInfo, completion.Profile,
            CustomCompanyInfoOnboardingHelper.ContextFor(completion.Profile, CustomInfoMode.Create, completion.TenantId, user),
            completion.TenantId, logger, ct);

        // Der Zustimmungs-Nachweis stammt aus dem geparkten Vorgang: der Benutzer hat vor der
        // Mailbestaetigung zugestimmt, und genau dieser Zeitpunkt steht in den Antworten.
        await ConsentOnboardingHelper.RecordAsync(consent, completion.Profile, completion.TenantId,
            completion.UserId, completion.Email, logger, ct);
        return true;
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

    public async Task<bool> AssignDefaultTenantAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        string? wanted = setupOptions.ValueOrDefault?.DefaultUserTenant;
        if (string.IsNullOrWhiteSpace(wanted))
        {
            return false;
        }

        var owner = await userManager.GetUserAsync(user);
        if (owner == null)
        {
            logger.LogWarning("Skipped the assignment to the default tenant: no account could be resolved for the signed-in user.");
            return false;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // Gehoert er schon irgendwo dazu, ist nichts zu tun - das ist der Normalfall bei jedem weiteren
        // Aufruf und keine Meldung wert.
        if (await db.TenantUsers.IgnoreQueryFilters().AnyAsync(n => n.UserId == owner.Id, ct))
        {
            return false;
        }

        // Eine wartende Einladung hat Vorrang: sie fuehrt ihn dorthin, wo er hingehoert.
        if (await db.Employees.IgnoreQueryFilters()
                .AnyAsync(e => e.EMail == owner.Email && e.InvitationStatus == InvitationStatus.Pending, ct))
        {
            logger.LogDebug("User {Email} is not assigned to the default tenant: an invitation is pending.", owner.Email);
            return false;
        }

        // Ohne Filter gelesen: der Benutzer hat auf diesen Mandanten noch keinen Zugriff - genau deshalb
        // ist er ja hier. Mit aktiven Mandanten-Filtern faende die Abfrage nichts.
        var tenant = await db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TenantName == wanted || t.DisplayName == wanted, ct);
        if (tenant == null)
        {
            logger.LogError("The tenant '{Tenant}' configured as default does not exist; user {Email} is left without a tenant.", wanted, owner.Email);
            return false;
        }

        var tenantUser = new TenantUser
        {
            Enabled = true,
            TenantId = tenant.TenantId,
            // FK-Skalar und nicht die Navigation: 'owner' gehoert dem Kontext des UserManagers. Als
            // Navigation angehaengt hielte EF ihn fuer einen neuen Datensatz und wuerde ihn einzufuegen
            // versuchen - PK-Verletzung auf einem Benutzer, den es laengst gibt.
            UserId = owner.Id
        };
        db.TenantUsers.Add(tenantUser);

        string? roleName = setupOptions.ValueOrDefault?.DefaultUserTenantRole;
        if (!string.IsNullOrWhiteSpace(roleName))
        {
            var role = await db.SecurityRoles.IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.TenantId == tenant.TenantId && r.RoleName == roleName, ct);
            if (role != null)
            {
                db.TenantUserRoles.Add(new UserRole { Role = role, User = tenantUser });
            }
            else
            {
                // Der Benutzer wird trotzdem Mitglied - aber ohne Rolle sieht er nichts, und ohne diese
                // Zeile wuerde man die Ursache im Mandanten suchen statt in der Konfiguration.
                logger.LogError("The role '{Role}' configured as default does not exist in tenant '{Tenant}'; user {Email} is assigned without a role.", roleName, wanted, owner.Email);
            }
        }
        else
        {
            logger.LogWarning("No role is configured for the default tenant '{Tenant}'; user {Email} is assigned without a role and will probably see nothing.", wanted, owner.Email);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("User {Email} was assigned to the default tenant '{Tenant}'.", owner.Email, wanted);
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
