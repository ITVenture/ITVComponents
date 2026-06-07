namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.AspNetCoreTenantSecurityUserView.ViewModels;

public class UserLoginViewModel
{
    public string LoginProvider { get; set; } = "";
    public string ProviderKey { get; set; } = "";
    public string? ProviderDisplayName { get; set; }
    public string UserId { get; set; } = "";
}
