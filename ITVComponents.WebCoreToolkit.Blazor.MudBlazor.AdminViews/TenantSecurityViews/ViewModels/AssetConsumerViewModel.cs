using ITVComponents.WebCoreToolkit.Security.SharedAssets;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

/// <summary>
/// Ein Endpunkt, der Argumente eines geteilten Assets versteht. Die Zeilen entstehen im Betrieb, wenn
/// sich ein Endpunkt meldet - die Maske zeigt und raeumt, sie pflegt nicht.
/// </summary>
public class AssetConsumerViewModel
{
    public int AssetConsumerId { get; set; }

    public AssetConsumerKind DeclarationKind { get; set; }

    public string DeclarationKey { get; set; } = string.Empty;

    public DateTime FirstSeenUtc { get; set; }

    /// <summary>
    /// Die einzige Auskunft darueber, ob eine Zeile noch aktuell ist. Deshalb steht sie in der Maske.
    /// </summary>
    public DateTime LastSeenUtc { get; set; }

    public int ArgumentCount { get; set; }
}

/// <summary>
/// Ein Argument eines Endpunkts. Kommt aus dem Code und wird bei der naechsten Meldung ueberschrieben -
/// deshalb in der Maske nur lesbar.
/// </summary>
public sealed class AssetConsumerArgumentViewModel
{
    public int AssetConsumerArgumentId { get; set; }

    public int AssetConsumerId { get; set; }

    public string ArgumentName { get; set; } = string.Empty;

    public AssetArgumentType ArgumentType { get; set; }

    public bool Required { get; set; }

    public int SortOrder { get; set; }
}
