namespace ITVComponents.WebCoreToolkit.AspNetCoreTenantSecurityUserView.Blazor.ViewModels;

public class UserTokenViewModel
{
    public string UserId { get; set; } = "";
    public string LoginProvider { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Value { get; set; }
}
