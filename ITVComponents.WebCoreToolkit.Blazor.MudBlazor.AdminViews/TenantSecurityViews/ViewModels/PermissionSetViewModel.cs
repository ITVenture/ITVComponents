using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

public class PermissionSetViewModel
{
    public int AppPermissionSetId { get; set; }

    /// <summary>
    /// Das Template, zu dem dieses Buendel gehoert - Pflicht.
    /// </summary>
    /// <remarks>
    /// Seit PRE240 gehoert ein Buendel genau EINEM Template (die Zugehoerigkeit traegt die Obergrenze
    /// dessen, was eine ClientApp fuehren darf). Das Feld fehlte hier, und weil es fehlte, legte
    /// <c>CreateAsync</c> mit <c>ClientAppTemplateId = 0</c> an - der Fremdschluessel hat das abgewiesen.
    /// Es gab damit gar keinen Weg, ein Buendel anzulegen.
    /// </remarks>
    [Range(1, int.MaxValue, ErrorMessage = "A template is required")]
    public int ClientAppTemplateId { get; set; }

    /// <summary>Nur zur Anzeige - die Liste zeigt sonst Buendel aller Templates ununterscheidbar.</summary>
    public string? ClientAppTemplateName { get; set; }

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;
}

public sealed class AppPermissionAssignmentViewModel
{
    public int PermissionId { get; set; }
    public string PermissionName { get; set; } = "";
    public string? Description { get; set; }
    public int AppPermissionSetId { get; set; }
    public bool Assigned { get; set; }
}
