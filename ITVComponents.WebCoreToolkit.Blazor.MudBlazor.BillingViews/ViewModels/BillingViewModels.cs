using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models;

namespace ITVComponents.WebCoreToolkit.BillingViews.Blazor.ViewModels
{
    /// <summary>
    /// One entry of the tenant-security feature catalog, offered as a pickable option when authoring plan/add-on
    /// feature grants. <see cref="Name"/> is the <c>Feature.FeatureName</c> that a <c>FeatureKey</c> must match.
    /// </summary>
    public class FeatureCatalogItemViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool Enabled { get; set; }
    }

    /// <summary>A single currency-specific price of a plan/add-on (catalog/edit view).</summary>
    public class PriceViewModel
    {
        public string Currency { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? ProviderPriceId { get; set; }
    }

    /// <summary>A purchasable base plan (catalog view). Carries one price per supported currency plus the add-ons bookable with it.</summary>
    public class PlanViewModel
    {
        public int PlanId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public BillingInterval BillingInterval { get; set; }
        public int? TrialDays { get; set; }
        public bool IsActive { get; set; } = true;
        public List<PriceViewModel> Prices { get; set; } = new();
        public List<string> FeatureKeys { get; set; } = new();

        /// <summary>Add-ons bookable with this plan; each carries its per-currency price under this plan.</summary>
        public List<PlanAddOnViewModel> AddOns { get; set; } = new();

        /// <summary>The price row for <paramref name="currency"/> (case-insensitive), or null if none.</summary>
        public PriceViewModel? PriceFor(string? currency)
            => currency == null ? null : Prices.FirstOrDefault(p => string.Equals(p.Currency, currency, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// An add-on's identity (catalog/edit view). Pricing and plan bookability are per-plan and live on the
    /// plan link (<see cref="PlanAddOnViewModel"/>), not here — an add-on is just a name + features.
    /// </summary>
    public class AddOnViewModel
    {
        public int AddOnId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public List<string> FeatureKeys { get; set; } = new();
    }

    /// <summary>
    /// An add-on as bookable under a specific plan: the add-on identity plus its per-currency price for that
    /// plan. The recurring interval is the owning plan's <c>BillingInterval</c>.
    /// </summary>
    public class PlanAddOnViewModel
    {
        public int AddOnId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<PriceViewModel> Prices { get; set; } = new();

        /// <summary>True while this add-on is linked to the plan being edited (admin picker state).</summary>
        public bool Selected { get; set; }

        /// <summary>The price row for <paramref name="currency"/> (case-insensitive), or null if none.</summary>
        public PriceViewModel? PriceFor(string? currency)
            => currency == null ? null : Prices.FirstOrDefault(p => string.Equals(p.Currency, currency, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>One line of the tenant's active subscription.</summary>
    public class SubscriptionItemViewModel
    {
        public string Name { get; set; } = string.Empty;
        public bool IsAddOn { get; set; }
    }

    /// <summary>The current tenant's subscription summary for the manage page.</summary>
    public class SubscriptionOverviewViewModel
    {
        public bool HasSubscription { get; set; }
        public SubscriptionStatus Status { get; set; }
        public string? Currency { get; set; }
        public System.DateTime? CurrentPeriodEnd { get; set; }
        public bool CancelAtPeriodEnd { get; set; }
        public List<SubscriptionItemViewModel> Items { get; set; } = new();
    }

    /// <summary>
    /// One tenant subscription as shown on the read-only admin overview. The billing tables carry the tenant
    /// only as a plain id (no FK to the security model), so the overview lists <see cref="TenantId"/> plus
    /// the resolved plan/add-on item names and the billing state.
    /// </summary>
    public class SubscriptionAdminViewModel
    {
        public int TenantId { get; set; }
        public SubscriptionStatus Status { get; set; }
        public string? Currency { get; set; }
        public System.DateTime? CurrentPeriodStart { get; set; }
        public System.DateTime? CurrentPeriodEnd { get; set; }
        public bool CancelAtPeriodEnd { get; set; }
        public List<SubscriptionItemViewModel> Items { get; set; } = new();

        /// <summary>Comma-joined item names (plans then add-ons) for compact display.</summary>
        public string ItemsSummary => string.Join(", ", Items.Select(i => i.Name));
    }
}
