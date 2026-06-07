using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.ViewModels;

/// <summary>
/// Form-bound model for the anonymous direct-onboarding page. Extends <see cref="BillingProfileViewModel"/>
/// (whose <c>Email</c> doubles as the login e-mail) with the account password. The password properties are
/// <see cref="JsonIgnoreAttribute">not serialized</see> so they never end up in the parked onboarding payload.
/// </summary>
public class DirectOnboardingViewModel : BillingProfileViewModel
{
    [Required, DataType(DataType.Password)]
    [JsonIgnore]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [JsonIgnore]
    public string ConfirmPassword { get; set; } = string.Empty;
}
