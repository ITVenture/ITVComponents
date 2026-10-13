using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

public class ClientAppTemplateViewModel
{
    public int ClientAppTemplateId { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Unter welchem Objektnamen Agenten dieses Templates ihren Katalog bereitstellen; leer = Standard.</summary>
    [MaxLength(128)]
    public string? CatalogObjectName { get; set; }
}

public sealed class AppPermissionSetAssignmentViewModel
{
    public int AppPermissionSetId { get; set; }
    public string PermissionSetName { get; set; } = "";
    public int ClientAppTemplateId { get; set; }
    public bool Assigned { get; set; }
}
