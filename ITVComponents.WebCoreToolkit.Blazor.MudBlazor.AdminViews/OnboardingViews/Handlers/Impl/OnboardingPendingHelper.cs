using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.ViewModels;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Handlers.Impl;

/// <summary>
/// Strategy-neutral logic for the deferred direct-onboarding flow. <see cref="StartAsync{TCtx}"/> creates
/// the Identity user (unconfirmed) and parks the requested tenant payload as a <see cref="PendingOnboarding"/>;
/// <see cref="CompleteAsync{TCtx}"/> consumes that record once the user is authenticated, delegating the
/// actual tenant creation back to the active strategy handler so flat and tree share this code.
/// </summary>
internal static class OnboardingPendingHelper
{
    /// <summary>
    /// Creates an Identity user (unconfirmed) WITHOUT parking any pending tenant payload. Used by the
    /// employee-invitation join flow, where the invitee only needs an account to accept an invitation to an
    /// existing tenant (matched by e-mail on the My-Tenants page) — no tenant is ever created for them here.
    /// </summary>
    public static async Task<OnboardingStartResult> RegisterAccountAsync<TUser>(UserManager<TUser> userManager,
        string email, string password)
        where TUser : IdentityUser, new()
    {
        var user = new TUser { UserName = email, Email = email };
        var result = await userManager.CreateAsync(user, password);
        return result.Succeeded
            ? new OnboardingStartResult(true, user.Id, Array.Empty<string>())
            : new OnboardingStartResult(false, null, result.Errors.Select(e => e.Description).ToArray());
    }

    public static async Task<OnboardingStartResult> StartAsync<TCtx, TUser>(TCtx db, UserManager<TUser> userManager,
        OnboardingStartInput input, CancellationToken ct)
        where TCtx : DbContext, IOnboardingPendingContext
        where TUser : IdentityUser, new()
    {
        var user = new TUser { UserName = input.Email, Email = input.Email };
        var result = await userManager.CreateAsync(user, input.Password);
        if (!result.Succeeded)
        {
            return new OnboardingStartResult(false, null, result.Errors.Select(e => e.Description).ToArray());
        }

        db.PendingOnboardings.Add(new PendingOnboarding
        {
            Email = input.Email,
            PayloadJson = JsonSerializer.Serialize(input.Profile),
            Status = InvitationStatus.Pending,
            CreatedUtc = DateTime.UtcNow,
            UserId = user.Id,
            InvitationToken = input.InvitationToken
        });
        await db.SaveChangesAsync(ct);

        return new OnboardingStartResult(true, user.Id, Array.Empty<string>());
    }

    /// <summary>
    /// Consumes the pending record and creates the tenant ATOMICALLY: the tenant/profile creation, the template
    /// application (which <paramref name="createTenant"/> performs on the SAME context it is handed) and flipping the
    /// pending record to <see cref="InvitationStatus.Committed"/> all run inside one transaction on one connection, so
    /// a failure anywhere rolls the whole thing back — no half-created tenants, no duplicate tenants on retry. The
    /// unit of work is wrapped in the context's execution strategy, so it is compatible with a host-configured
    /// <c>EnableRetryOnFailure</c>; the change tracker is reset per attempt so a retried attempt does not replay
    /// entities a rolled-back one left tracked. <paramref name="createTenant"/> receives the shared context and, when
    /// a previous attempt already recorded a tenant (<see cref="PendingOnboarding.CreatedTenantId"/>), that tenant's
    /// id so it can resume on the existing tenant instead of creating a new one; it returns the created/resumed
    /// tenant id plus the billing-profile id, or null to signal failure (rolls back).
    /// </summary>
    public static async Task<bool> CompleteAsync<TCtx, TUser>(IDbContextFactory<TCtx> dbFactory, UserManager<TUser> userManager, ClaimsPrincipal principal,
        Func<ClaimsPrincipal, BillingProfileViewModel, TCtx, int?, CancellationToken, Task<(int tenantId, int billingProfileId)?>> createTenant, CancellationToken ct,
        ILogger logger = null)
        where TCtx : DbContext, IOnboardingPendingContext
        where TUser : IdentityUser
    {
        var owner = await userManager.GetUserAsync(principal);
        if (string.IsNullOrEmpty(owner?.Email))
        {
            logger?.LogWarning("Onboarding completion skipped: no user/e-mail resolved for the current principal.");
            return false;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            // An execution-strategy retry re-runs this whole body; drop anything a rolled-back prior attempt left
            // tracked as Added so it is not re-INSERTed here.
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var pending = await db.PendingOnboardings
                .FirstOrDefaultAsync(p => p.Email == owner.Email && p.Status == InvitationStatus.Pending, ct);
            if (pending == null)
            {
                // Normal on every later visit (already committed) — only interesting while chasing a missing tenant.
                logger?.LogDebug("No pending onboarding for {Email}; nothing to complete.", owner.Email);
                return false;
            }

            var profile = string.IsNullOrEmpty(pending.PayloadJson)
                ? new BillingProfileViewModel()
                : JsonSerializer.Deserialize<BillingProfileViewModel>(pending.PayloadJson) ?? new BillingProfileViewModel();

            var created = await createTenant(principal, profile, db, pending.CreatedTenantId, ct);
            if (created == null)
            {
                logger?.LogWarning(
                    "Onboarding for {Email} was rejected by the strategy handler (resume marker: {ResumeTenantId}); rolling back, no tenant created.",
                    owner.Email, pending.CreatedTenantId);
                return false;
            }

            // Record the tenant BEFORE committing (resume marker) and flip the pending in the same transaction.
            pending.CreatedTenantId = created.Value.tenantId;
            pending.Status = InvitationStatus.Committed;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return true;
        });
    }
}
