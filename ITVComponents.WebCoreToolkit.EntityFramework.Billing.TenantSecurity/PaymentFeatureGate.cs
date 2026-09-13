using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.TenantSecurity
{
    /// <summary>
    /// Default <see cref="IPaymentFeatureGate"/> for the toolkit tenant-security model: answers whether a tenant
    /// currently holds the payments feature by asking the database for an active
    /// <c>TenantFeatureActivation</c>.
    /// <para>
    /// By TENANT ID and explicitly NOT through the ambient security scope. The sale service is called from
    /// anonymous shop requests and background runs where there is no scope at all, and a scope-based check would
    /// then either fail or, worse, silently answer for the wrong tenant. That rules out
    /// <c>VerifyActivatedFeatures</c>, which needs an authenticated caller and reads the tenant from
    /// <c>IPermissionScope</c> — but NOT the rule underneath it, which lives in
    /// <see cref="FeatureActivationExtensions"/> and is what this gate asks.
    /// </para>
    /// <para>
    /// Nothing special has to happen for a subscription to switch this on: a plan or add-on carrying the feature
    /// key runs through the existing <see cref="BillingFeatureProvisioner{TContext,TTenant,TActivation}"/> and
    /// produces the activation this gate reads. Axis A unlocks axis B without a line of extra logic.
    /// </para>
    /// <para>
    /// This gate answers the ENTITLEMENT only. Whether the deployment permits payments at all is
    /// <c>StripePaymentsOptions.Enabled</c>, checked by <c>PaymentsRuntime.EnsureEnabled</c> — the deployment
    /// always wins over the entitlement. The feature catalogue is not a second master switch, and reading it as
    /// one is what made a per-tenant sold module impossible to switch on.
    /// </para>
    /// </summary>
    /// <typeparam name="TContext">The host <see cref="DbContext"/> (must map Features and the activation type).</typeparam>
    /// <typeparam name="TTenant">The tenant model the activation is bound to.</typeparam>
    /// <typeparam name="TActivation">The concrete <c>TenantFeatureActivation</c> derivative.</typeparam>
    public class PaymentFeatureGate<TContext, TTenant, TActivation> : IPaymentFeatureGate
        where TContext : DbContext
        where TTenant : Tenant
        where TActivation : TenantFeatureActivation<TTenant>, new()
    {
        /// <summary>The feature key that unlocks receiving payments from end customers.</summary>
        public const string FeatureKey = "StripePayments";

        private readonly IToolkitContextFactory contextFactory;

        private readonly ILogger<PaymentFeatureGate<TContext, TTenant, TActivation>> logger;

        public PaymentFeatureGate(IToolkitContextFactory contextFactory,
            ILogger<PaymentFeatureGate<TContext, TTenant, TActivation>> logger)
        {
            this.contextFactory = contextFactory;
            this.logger = logger;
        }

        /// <inheritdoc />
        public async Task<bool> IsEnabledForTenantAsync(int tenantId, CancellationToken cancellationToken = default)
        {
            using var lease = contextFactory.Lease<TContext>();

            var state = await lease.Context
                .GetFeatureStateForTenantAsync<TTenant, TActivation>(FeatureKey, tenantId,
                    cancellationToken: cancellationToken);

            // Every no gets its own line. The effect of a no ("every sale is refused") shows up far away from
            // its cause, and from the outside a missing catalogue entry looks exactly like a tenant that simply
            // did not buy the module - that difference is what costs the time when someone goes looking.
            switch (state)
            {
                case FeatureActivationState.Unknown:
                    logger.LogError(
                        "Payments refused for tenant {TenantId}: there is no feature '{FeatureKey}' in the catalogue. Nobody can be sold payments until that row exists.",
                        tenantId, FeatureKey);
                    break;
                case FeatureActivationState.NotActivatedForTenant:
                    logger.LogDebug(
                        "Payments refused for tenant {TenantId}: '{FeatureKey}' is not on for everyone and this tenant has no currently valid activation.",
                        tenantId, FeatureKey);
                    break;
                case FeatureActivationState.EnabledForEveryone:
                    logger.LogDebug(
                        "Payments granted for tenant {TenantId}: '{FeatureKey}' is enabled for every tenant. To sell it per tenant instead, clear Enabled on the feature row and activate it per tenant.",
                        tenantId, FeatureKey);
                    break;
                case FeatureActivationState.ActivatedForTenant:
                    logger.LogDebug("Payments granted for tenant {TenantId}: '{FeatureKey}' is activated for it.",
                        tenantId, FeatureKey);
                    break;
            }

            return state.IsEnabled();
        }
    }
}
