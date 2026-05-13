namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.IdentityPages.Handlers;

/// <summary>
/// Marker type used as TPageModel for <see cref="IPasskeyHandler"/>. No actual MVC Razor page is bound — the
/// handler services both the Login flow and the Account/Manage/Passkeys + Account/Manage/RenamePasskey pages
/// in the Blazor library. Exists purely to satisfy the <c>IPageHandlerInstance&lt;TPageModel&gt;</c> contract.
/// </summary>
public sealed class PasskeyPageModel
{
}
