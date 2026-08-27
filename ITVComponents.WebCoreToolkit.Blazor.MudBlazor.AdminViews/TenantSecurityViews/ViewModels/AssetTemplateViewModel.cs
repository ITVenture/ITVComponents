using System.ComponentModel.DataAnnotations;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

public class AssetTemplateViewModel
{
    public int AssetTemplateId { get; set; }

    public int? FeatureId { get; set; }

    public int? PermissionId { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string SystemKey { get; set; } = string.Empty;

    /// <summary>
    /// Wie streng die Bestaetigung der Argumente verlangt wird. Ohne Argumente bedeutungslos.
    /// </summary>
    public AssetArgumentEnforcement ArgumentEnforcement { get; set; } = AssetArgumentEnforcement.None;

    /// <summary>
    /// Ob mit dieser Vorlage Ad-hoc-Tickets erzeugt werden duerfen - Freigaben, die nirgends stehen und
    /// sich nicht einzeln zurueckziehen lassen.
    /// </summary>
    public bool AllowAdHoc { get; set; }

    /// <summary>Die Hoechstdauer eines solchen Tickets.</summary>
    public int MaxAdHocMinutes { get; set; } = 60;

    /// <summary>
    /// Der Schluessel einer Gueltigkeitsregel, die der Host implementiert ("Auftrag ist offen").
    /// </summary>
    [MaxLength(128)]
    public string? ValidityRuleKey { get; set; }
}

/// <summary>
/// Ein Argument, das eine Vorlage fuehrt - worauf eine damit erzeugte Freigabe zeigen kann.
/// </summary>
public sealed class AssetTemplateArgumentViewModel
{
    public int AssetTemplateArgumentId { get; set; }
    public int AssetTemplateId { get; set; }

    [Required, MaxLength(128)]
    public string ArgumentName { get; set; } = string.Empty;

    public AssetArgumentType ArgumentType { get; set; } = AssetArgumentType.String;

    public bool Required { get; set; } = true;

    public int SortOrder { get; set; }

    /// <summary>
    /// Der Aufloeser, der ein Argument der Anfrage auf die geteilte Ebene normalisiert. Leer, wenn nur
    /// direkt verglichen wird.
    /// </summary>
    [MaxLength(128)]
    public string? ResolverKey { get; set; }
}

/// <summary>
/// Ein Endpunkt, den eine Vorlage abdeckt.
/// </summary>
public sealed class AssetTemplateConsumerViewModel
{
    public int AssetTemplateConsumerId { get; set; }
    public int AssetTemplateId { get; set; }

    public AssetConsumerKind DeclarationKind { get; set; } = AssetConsumerKind.Path;

    [Required, MaxLength(1024)]
    public string DeclarationKey { get; set; } = string.Empty;

    public bool IsEntryPoint { get; set; }

    /// <summary>
    /// Ob sich dieser Endpunkt schon einmal gemeldet hat. Nein heisst nicht falsch - es kann auch eine
    /// Seite sein, die seit dem letzten Neustart niemand besucht hat.
    /// </summary>
    public bool KnownToRegistry { get; set; }
}

public class AssetTemplatePathViewModel
{
    public int AssetTemplatePathId { get; set; }
    public int AssetTemplateId { get; set; }

    [Required, MaxLength(1024)]
    public string PathTemplate { get; set; } = string.Empty;
}

public sealed class AssetTemplatePermissionAssignmentViewModel
{
    public int PermissionId { get; set; }
    public string PermissionName { get; set; } = "";
    public string? Description { get; set; }
    public int AssetTemplateId { get; set; }
    public bool Assigned { get; set; }
}

public sealed class AssetTemplateFeatureAssignmentViewModel
{
    public int FeatureId { get; set; }
    public string FeatureName { get; set; } = "";
    public string? Description { get; set; }
    public int AssetTemplateId { get; set; }
    public bool Assigned { get; set; }
}
