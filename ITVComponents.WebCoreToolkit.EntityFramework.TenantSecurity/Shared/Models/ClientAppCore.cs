using System;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models
{
    /// <summary>
    /// Was eine ClientApp ausserhalb der Sicherheitsschicht ausmacht — ohne ihre Rechte-Kette.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Die volle ClientApp trägt neunzehn Typparameter, weil sie über Templates, Rechtebündel und Zugänge in
    /// die ganze Rechte-Struktur greift. Wer nur wissen will, <i>welche</i> Kasse es ist und ob sie läuft
    /// (die Agent-Dienste etwa), verlangt diese Schnittstelle statt des ganzen Typs.
    /// </para>
    /// <para>
    /// <b>In EF-Abfragen nicht über die Schnittstelle zugreifen</b>, sondern über
    /// <c>EF.Property&lt;T&gt;(e, nameof(IClientAppCore.X))</c>: ein Zugriff über ein Interface ist im
    /// Ausdruck nicht übersetzbar.
    /// </para>
    /// </remarks>
    public interface IClientAppCore
    {
        /// <summary>Der Schlüssel.</summary>
        int ClientAppId { get; set; }

        /// <summary>Der Mandant.</summary>
        int TenantId { get; set; }

        /// <summary>Das Template, aus dem sie entstanden ist.</summary>
        int ClientAppTemplateId { get; set; }

        /// <summary>Der Anzeigename.</summary>
        string ClientName { get; set; }

        /// <summary>Die systemweit eindeutige Kennung — zugleich der Dienstname am Verteiler.</summary>
        string ClientKey { get; set; }

        /// <summary>Ob die Anwendung als Ganzes eingeschaltet ist.</summary>
        bool Enabled { get; set; }

        /// <summary>Wann sie sich zuletzt am Dienst-Verteiler an- oder abgemeldet hat.</summary>
        DateTime? LastSeenUtc { get; set; }
    }

    /// <summary>
    /// Was ein ClientApp-Template ausserhalb der Sicherheitsschicht ausmacht.
    /// </summary>
    public interface IClientAppTemplateCore
    {
        /// <summary>Der Schlüssel.</summary>
        int ClientAppTemplateId { get; set; }

        /// <summary>Der Name.</summary>
        string Name { get; set; }

        /// <summary>
        /// Unter welchem Objektnamen der Agent seinen Katalog bereitstellt. Leer = der Standardname.
        /// </summary>
        string CatalogObjectName { get; set; }

        /// <summary>
        /// Was ein Geraet dieses Templates ist, als fester Schluessel - das Geraet nennt ihn beim Koppeln.
        /// </summary>
        string TypeKey { get; set; }
    }
}
