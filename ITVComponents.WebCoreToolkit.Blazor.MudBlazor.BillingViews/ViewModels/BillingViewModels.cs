using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models;

namespace ITVComponents.WebCoreToolkit.BillingViews.Blazor.ViewModels
{
    /// <summary>A single currency-specific price of a plan/add-on (catalog/edit view).</summary>
    public class PriceViewModel
    {
        public string Currency { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? ProviderPriceId { get; set; }
    }

    /// <summary>A purchasable base plan (catalog view). Carries one price per supported currency.</summary>
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

        /// <summary>The price row for <paramref name="currency"/> (case-insensitive), or null if none.</summary>
        public PriceViewModel? PriceFor(string? currency)
            => currency == null ? null : Prices.FirstOrDefault(p => string.Equals(p.Currency, currency, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A purchasable add-on (catalog view). Carries one price per supported currency.</summary>
    public class AddOnViewModel
    {
        public int AddOnId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public BillingInterval BillingInterval { get; set; }
        public bool IsActive { get; set; } = true;
        public List<PriceViewModel> Prices { get; set; } = new();
        public List<string> FeatureKeys { get; set; } = new();

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
