using System;
using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

/// <summary>
/// Eine Anwendung dieses Mandanten.
/// </summary>
public class ClientAppViewModel
{
    public int ClientAppId { get; set; }

    [Required, MaxLength(200)]
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// Das Template, aus dem die Anwendung entstanden ist. <b>Nach dem Anlegen unveraenderlich</b> - es
    /// begrenzt, welche Rechtebuendel die Anwendung fuehren darf, und ein Wechsel wuerde bestehende
    /// Zuordnungen ungueltig machen.
    /// </summary>
    public int ClientAppTemplateId { get; set; }

    public string TemplateName { get; set; } = string.Empty;

    /// <summary>
    /// Die oeffentliche Kennung. Wird beim Anlegen erzeugt und danach nicht mehr geaendert - Geraete
    /// tragen sie in ihrem Schluessel.
    /// </summary>
    public string ClientKey { get; set; } = string.Empty;

    /// <summary>Der Sammelschalter: aus heisst, kein Zugang dieser Anwendung kommt mehr herein.</summary>
    public bool Enabled { get; set; } = true;

    public DateTime CreatedUtc { get; set; }

    /// <summary>Wie viele Geraete gekoppelt und nicht widerrufen sind.</summary>
    public int ActiveAccessCount { get; set; }
}

/// <summary>
/// Ein Zugang - in aller Regel ein gekoppeltes Geraet.
/// </summary>
public sealed class ClientAppAccessViewModel
{
    public int ClientAppAccessId { get; set; }

    public int ClientAppId { get; set; }

    public string Label { get; set; } = string.Empty;

    public string DeviceLabel { get; set; } = string.Empty;

    /// <summary>
    /// Falsch, wenn der Zugang an einem Benutzer haengt (Delegation). Bei Geraeten ist er wahr.
    /// </summary>
    public bool IsMachine { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? ExpiresUtc { get; set; }

    public DateTime? RevokedUtc { get; set; }

    /// <summary>
    /// Letzte erfolgreiche Anmeldung. <b>Das Feld, an dem ein stillgelegtes Geraet auffaellt</b> - und
    /// zugleich das, an dem man sieht, ob eine Kopplung je abgeschlossen wurde.
    /// </summary>
    public DateTime? LastUsedUtc { get; set; }

    /// <summary>
    /// Falsch, solange die Kopplung nicht abgeschlossen ist (das Geheimnis wurde nie abgeholt). So ein
    /// Zugang kann sich nicht anmelden.
    /// </summary>
    public bool HasSecret { get; set; }
}

/// <summary>
/// Ein Rechtebuendel des Templates und ob die Anwendung es zugestanden bekommt.
/// </summary>
public sealed class ClientAppPermissionSetViewModel
{
    public int AppPermissionSetId { get; set; }
    public int ClientAppId { get; set; }
    public string PermissionSetName { get; set; } = "";

    /// <summary>Die Rechte, die in diesem Buendel stecken - damit niemand blind ankreuzt.</summary>
    public string Permissions { get; set; } = "";

    public bool Assigned { get; set; }
}
