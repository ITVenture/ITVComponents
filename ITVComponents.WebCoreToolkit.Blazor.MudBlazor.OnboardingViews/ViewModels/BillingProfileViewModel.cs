using System.ComponentModel.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.OnboardingShared.Models;

namespace ITVComponents.WebCoreToolkit.OnboardingViews.Blazor.ViewModels;

/// <summary>
/// Form-bound view model for creating (or editing) a <c>BillingProfile</c>.
/// <see cref="ProfileType"/> drives conditional validation: <c>Personal</c> requires
/// <see cref="FirstName"/>/<see cref="LastName"/>; <c>Company</c> requires <see cref="CompanyName"/>.
/// <see cref="ParentTenantId"/> is only relevant for the hierarchy variant; flat handlers ignore it.
/// </summary>
public class BillingProfileViewModel
{
    public int BillingProfileId { get; set; }

    public ProfileType ProfileType { get; set; } = ProfileType.Personal;

    [MaxLength(256)]
    [ConditionalRequired(BackEndCondition = "ProfileType == 0", ErrorMessage = "First name is required for a personal profile.")]
    public string? FirstName { get; set; }

    [MaxLength(256)]
    [ConditionalRequired(BackEndCondition = "ProfileType == 0", ErrorMessage = "Last name is required for a personal profile.")]
    public string? LastName { get; set; }

    [MaxLength(1024)]
    [ConditionalRequired(BackEndCondition = "ProfileType == 1", ErrorMessage = "Company name is required for a company profile.")]
    public string? CompanyName { get; set; }

    [MaxLength(64)]
    public string? VatNumber { get; set; }

    [Required, MaxLength(256), EmailAddress]
    public string Email { get; set; } = string.Empty;

    [MaxLength(100), Phone]
    public string? PhoneNumber { get; set; }

    public AddressInput DefaultAddress { get; set; } = new();

    public bool UseInvoiceAddr { get; set; }

    public AddressInput InvoiceAddress { get; set; } = new();

    /// <summary>
    /// Only used by the hierarchy variant (<c>HierarchyOnboardingHandler</c>). When set,
    /// the new tenant is created as a child of this tenant; null = root-level.
    /// </summary>
    public int? ParentTenantId { get; set; }

    [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept the terms of service.")]
    public bool AcceptTos { get; set; }
}

/// <summary>
/// Form-bound subset of an <c>Address</c> entity. Conditional required behaviour for the
/// invoice address is handled at the form level (<c>BillingProfileForm.razor</c>) via the
/// <c>UseInvoiceAddr</c> toggle.
/// </summary>
public class AddressInput
{
    [MaxLength(1024)]
    public string? Name { get; set; }

    [MaxLength(256)]
    public string? Addition1 { get; set; }

    [MaxLength(256)]
    public string? Addition2 { get; set; }

    [MaxLength(256)]
    public string? Street { get; set; }

    [MaxLength(256)]
    public string? Number { get; set; }

    [MaxLength(10)]
    public string? Zip { get; set; }

    [MaxLength(256)]
    public string? City { get; set; }
}
