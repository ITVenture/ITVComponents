using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Plugins;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Extensibility
{
    /// <summary>
    /// Ein Modul, das die Firmendaten-Erfassung um eigene Angaben erweitert - im Onboarding wie im
    /// Firmenprofil. Jedes angemeldete Modul bekommt unterhalb der Adressen einen eigenen Reiter.
    /// </summary>
    /// <remarks>
    /// Aktiviert werden Module ueber <c>CustomCompanyInfoOptions</c> (GlobalSettings) - dort steht die
    /// Liste der Plugin-Namen, die geladen werden. Das Toolkit legt von den erfassten Angaben selbst
    /// nichts ab: sie sind nicht sicherheitsrelevant und gehoeren nicht in die Security-Datenbank. Wo sie
    /// hingehoeren, weiss allein das Modul - darum <see cref="PersistAsync"/> und <see cref="LoadAsync"/>.
    /// <para>
    /// Der Vertrag kennt bewusst kein Blazor: ein Konsument soll ein Modul schreiben koennen, ohne die
    /// Oberflaechen-Bibliothek zu referenzieren. Eine eigene Maske wird deshalb nicht als Typ, sondern
    /// ueber <see cref="ViewKey"/> benannt - aufgeloest wird der Schluessel gegen das, was der Host
    /// registriert hat.
    /// </para>
    /// </remarks>
    public interface ICustomCompanyInformationHandler : IPlugin
    {
        /// <summary>
        /// Der stabile Schluessel dieses Moduls. Er benennt den Datensatz in der Ablage des Vorgangs und
        /// darf sich nicht mehr aendern, sobald Angaben damit erfasst wurden.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Die Beschriftung des Reiters. Klartext oder Kultur-JSON
        /// (<c>{"de":"Netzwerk","fr":"Reseau"}</c>).
        /// </summary>
        string Title { get; }

        /// <summary>Optionales Symbol fuer den Reiter (Name eines Mud-Icons), oder leer.</summary>
        string Icon { get; }

        /// <summary>
        /// Der Schluessel einer vom Host registrierten eigenen Maske. Leer = die generische Maske wird aus
        /// <see cref="GetFields"/> gebaut. Ein Schluessel, zu dem nichts registriert ist, faellt auf die
        /// generische Maske zurueck - aber mit einer Log-Zeile, nicht still.
        /// </summary>
        string ViewKey { get; }

        /// <summary>
        /// Die Berechtigung, die zum Bearbeiten im Firmenprofil noetig ist. Leer = es gilt die
        /// Berechtigung des Firmenprofils selbst.
        /// </summary>
        string EditPermission { get; }

        /// <summary>
        /// Ist dieses Modul in diesem Fall ueberhaupt zustaendig? Damit entscheidet es beides: ob seine
        /// Angaben bei der Anlage erfasst werden (etwa nicht, wenn der Tenant auf eine Einladung hin
        /// entsteht und die Angabe vom uebergeordneten Tenant erbt) und ob sie sich spaeter im
        /// Firmenprofil nachtragen lassen.
        /// </summary>
        bool AppliesTo(CustomInfoContext ctx);

        /// <summary>
        /// Die Felder der generischen Maske. Weil der Kontext uebergeben wird, darf dieselbe Angabe in
        /// einem Fall Pflicht und in einem anderen freiwillig sein. Leer, wenn das Modul eine eigene
        /// Maske mitbringt (<see cref="ViewKey"/>).
        /// </summary>
        IReadOnlyList<CustomInfoField> GetFields(CustomInfoContext ctx);

        /// <summary>
        /// Prueft die erfassten Angaben fachlich. Die Pflichtfeld-Pruefung der generischen Maske ist
        /// bereits gelaufen - hier geht es um alles, was darueber hinaus geht (Gueltigkeit, Kombinationen,
        /// Abgleich mit dem eigenen Bestand).
        /// </summary>
        /// <param name="values">
        /// die Angaben flach und invariant, wenn die generische Maske gerendert hat; sonst leer - dann
        /// steht der Datensatz in <paramref name="payload"/>.
        /// </param>
        /// <param name="payload">der rohe Datensatz dieses Moduls, oder null</param>
        /// <param name="ctx">der Erfassungsfall</param>
        /// <param name="ct">Abbruch</param>
        Task<CustomInfoValidation> ValidateAsync(IReadOnlyDictionary<string, string> values,
            JsonNode payload, CustomInfoContext ctx, CancellationToken ct);

        /// <summary>
        /// Laedt die abgelegten Angaben eines Tenants zur Vorbelegung der Maske. Die Werte gehen in
        /// derselben Form zurueck, in der sie erfasst wurden.
        /// </summary>
        /// <returns>
        /// der Datensatz dieses Moduls, oder null wenn zu diesem Tenant noch nichts abgelegt ist
        /// </returns>
        Task<JsonNode> LoadAsync(int tenantId, CancellationToken ct);

        /// <summary>
        /// Legt die Angaben ab. Wird erst gerufen, wenn der Tenant tatsaechlich besteht - die TenantId
        /// gibt es vorher nicht.
        /// </summary>
        /// <remarks>
        /// Weil die Angaben ausserhalb der Security-Datenbank liegen, gibt es keine gemeinsame
        /// Transaktion mit der Tenant-Anlage. Scheitert das Ablegen, bleibt der Tenant bestehen und die
        /// Angaben fehlen - der Vorgang wird protokolliert und der Benutzer im Firmenprofil zum Nachtrag
        /// aufgefordert. Ein Modul sollte diese Methode deshalb wiederholbar (idempotent) bauen.
        /// </remarks>
        Task PersistAsync(CustomInfoPersistContext ctx, CancellationToken ct);
    }
}
