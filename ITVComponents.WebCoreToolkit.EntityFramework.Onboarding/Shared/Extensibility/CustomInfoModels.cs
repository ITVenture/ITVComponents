using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json.Nodes;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Extensibility
{
    /// <summary>
    /// Wird gerade erfasst oder nachgetragen?
    /// </summary>
    public enum CustomInfoMode
    {
        /// <summary>Der Tenant entsteht gerade - es gibt noch keine TenantId.</summary>
        Create,

        /// <summary>Nachtrag am bestehenden Tenant (Firmenprofil).</summary>
        Edit
    }

    /// <summary>
    /// Woher kommt der Vorgang?
    /// </summary>
    public enum CustomInfoOrigin
    {
        /// <summary>Der Benutzer legt selbst an.</summary>
        SelfService,

        /// <summary>Der Vorgang laeuft auf eine Einladung hin.</summary>
        Invitation
    }

    /// <summary>
    /// Der Erfassungsfall, in dem ein <see cref="ICustomCompanyInformationHandler"/> gefragt wird.
    /// </summary>
    /// <remarks>
    /// Der Kontext beschreibt bewusst NUR den Vorgang und nennt die eingesetzte Mandanten-Strategie
    /// nirgends. Ein Modul soll nicht fuer den flachen oder den hierarchischen Betrieb geschrieben werden
    /// muessen: <see cref="ParentTenantId"/> ist im flachen Betrieb schlicht immer null, und ein Modul,
    /// das nur <see cref="Mode"/>, <see cref="Origin"/> und <see cref="ProfileType"/> auswertet, laeuft in
    /// beiden Welten unveraendert. Auch <see cref="CustomInfoOrigin.Invitation"/> ist kein Merkmal der
    /// Hierarchie - Einladungen gibt es im flachen Betrieb genauso.
    /// </remarks>
    public class CustomInfoContext
    {
        /// <summary>Erfassung waehrend der Anlage oder Nachtrag am bestehenden Tenant.</summary>
        public CustomInfoMode Mode { get; set; }

        /// <summary>Selbst angelegt oder auf eine Einladung hin.</summary>
        public CustomInfoOrigin Origin { get; set; }

        /// <summary>Personen- oder Firmenprofil.</summary>
        public ProfileType ProfileType { get; set; }

        /// <summary>
        /// Der Tenant, um den es geht - bei <see cref="CustomInfoMode.Create"/> null, weil er noch nicht
        /// existiert.
        /// </summary>
        public int? TenantId { get; set; }

        /// <summary>
        /// Der uebergeordnete Tenant, sofern einer feststeht. Im flachen Betrieb immer null; reine
        /// Zusatzinformation, kein Fallunterschied.
        /// </summary>
        public int? ParentTenantId { get; set; }

        /// <summary>Der handelnde Benutzer, sofern angemeldet. Beim anonymen Start null.</summary>
        public ClaimsPrincipal User { get; set; }
    }

    /// <summary>Die Art eines Feldes der generischen Maske.</summary>
    public enum CustomInfoFieldKind
    {
        /// <summary>Einzeiliger Text.</summary>
        Text,

        /// <summary>Mehrzeiliger Text.</summary>
        MultilineText,

        /// <summary>Zahl.</summary>
        Number,

        /// <summary>Ja/Nein.</summary>
        Boolean,

        /// <summary>Datum.</summary>
        Date,

        /// <summary>Auswahl aus <see cref="CustomInfoField.Choices"/>.</summary>
        Choice
    }

    /// <summary>
    /// Ein Feld der generischen Maske. Der einfache Fall braucht damit keine eigene Komponente.
    /// </summary>
    public class CustomInfoField
    {
        /// <summary>Der Name des Feldes - zugleich der Schluessel im Bucket des Moduls.</summary>
        public string Name { get; set; }

        /// <summary>
        /// Die Beschriftung. Klartext ODER Kultur-JSON (<c>{"de":"Netzwerktyp","fr":"Type de reseau"}</c>).
        /// Uebersetzt wird erst beim Anzeigen, in der Kultur des Lesers.
        /// </summary>
        public string Label { get; set; }

        /// <summary>Die Art des Feldes (Vorgabe: einzeiliger Text).</summary>
        public CustomInfoFieldKind Kind { get; set; } = CustomInfoFieldKind.Text;

        /// <summary>Muss das Feld ausgefuellt sein?</summary>
        public bool Required { get; set; }

        /// <summary>Nur anzeigen, nicht erfassen.</summary>
        public bool ReadOnly { get; set; }

        /// <summary>Optionaler Hinweistext unter dem Feld. Klartext oder Kultur-JSON.</summary>
        public string HelpText { get; set; }

        /// <summary>Die Auswahlmoeglichkeiten - nur bei <see cref="CustomInfoFieldKind.Choice"/>.</summary>
        public List<CustomInfoChoice> Choices { get; set; } = new List<CustomInfoChoice>();
    }

    /// <summary>Eine Auswahlmoeglichkeit eines <see cref="CustomInfoFieldKind.Choice"/>-Feldes.</summary>
    public class CustomInfoChoice
    {
        /// <summary>Der Wert, der abgelegt wird.</summary>
        public string Value { get; set; }

        /// <summary>Die Beschriftung. Klartext oder Kultur-JSON; leer = <see cref="Value"/>.</summary>
        public string Label { get; set; }
    }

    /// <summary>
    /// Das Ergebnis der Pruefung eines Moduls.
    /// </summary>
    /// <remarks>
    /// Bewusst ein Ergebnis-Objekt und kein blosses bool: die Meldung ist der eine Text, den der Benutzer
    /// im Fehlerfall garantiert liest. Sie darf deshalb ebenfalls Kultur-JSON sein - sonst kaeme genau
    /// diese Stelle unuebersetzt durch.
    /// </remarks>
    public class CustomInfoValidation
    {
        private CustomInfoValidation(bool valid, string message, string fieldName)
        {
            Valid = valid;
            Message = message;
            FieldName = fieldName;
        }

        /// <summary>Sind die Angaben in Ordnung?</summary>
        public bool Valid { get; }

        /// <summary>Der anzeigbare Grund, wenn nicht. Klartext oder Kultur-JSON.</summary>
        public string Message { get; }

        /// <summary>
        /// Optional das Feld, an dem es haengt - fuer eine Markierung an Ort und Stelle. Leer, wenn die
        /// Beanstandung die Angaben als Ganzes betrifft.
        /// </summary>
        public string FieldName { get; }

        /// <summary>Alles in Ordnung.</summary>
        public static CustomInfoValidation Ok() => new CustomInfoValidation(true, null, null);

        /// <summary>Beanstandet, mit anzeigbarem Grund.</summary>
        /// <param name="message">der Grund - Klartext oder Kultur-JSON</param>
        /// <param name="fieldName">optional das betroffene Feld</param>
        public static CustomInfoValidation Failed(string message, string fieldName = null)
            => new CustomInfoValidation(false, message, fieldName);
    }

    /// <summary>
    /// Was ein Modul beim Ablegen seiner Angaben zur Verfuegung hat.
    /// </summary>
    public class CustomInfoPersistContext
    {
        /// <summary>Der Tenant, zu dem die Angaben gehoeren. Hier immer bekannt.</summary>
        public int TenantId { get; set; }

        /// <summary>Der Erfassungsfall, aus dem die Angaben stammen.</summary>
        public CustomInfoContext Info { get; set; }

        /// <summary>
        /// Der Datensatz dieses Moduls, roh. Bei einer eigenen Komponente ist das genau das, was sie
        /// geliefert hat.
        /// </summary>
        public JsonNode Payload { get; set; }

        /// <summary>
        /// Dieselben Angaben flach und in invarianter Schreibweise - nur belegt, wenn die generische Maske
        /// gerendert hat. Ein Modul mit eigener Komponente liest stattdessen <see cref="Payload"/>.
        /// </summary>
        public IReadOnlyDictionary<string, string> Fields { get; set; }
    }
}
