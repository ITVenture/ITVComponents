namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

/// <summary>
/// Ein protokollierter Zugriff auf eine Freigabe.
/// </summary>
public class AssetAccessViewModel
{
    public int SharedAssetAccessId { get; set; }

    public string? AssetKey { get; set; }

    /// <summary>
    /// Bei einem Ad-hoc-Ticket das Einzige, worueber sich Zugriffe zuordnen lassen - ausgegebene
    /// Tickets werden bewusst nirgends gesammelt, weil sie kurzlebig sind.
    /// </summary>
    public string? TicketNonce { get; set; }

    public string? TemplateSystemKey { get; set; }

    public string? RecipientLabel { get; set; }

    public string? AccessedBy { get; set; }

    public string? RequestPath { get; set; }

    public string? ArgumentSummary { get; set; }

    public bool Granted { get; set; }

    /// <summary>
    /// <c>NotConfirmed</c> heisst: niemand hat die Argumente bestaetigt. <c>Denied</c> heisst: jemand
    /// hat auf etwas Fremdes gezeigt. Nur das zweite ist ein Alarmzeichen.
    /// </summary>
    public string? DenyReason { get; set; }

    public DateTime Created { get; set; }
}
