using System;

namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    /// <summary>
    /// Wodurch ein Konsument von Asset-Argumenten identifiziert wird.
    /// </summary>
    public enum AssetConsumerKind
    {
        /// <summary>
        /// Ueber seine Route-Vorlage, z.B. <c>/sales/order/{id}</c>. Der Normalfall: die Vorlage ist Stelle
        /// und Argumentquelle zugleich, und nur mit ihr laesst sich aus Argumentwerten wieder eine URL
        /// bauen.
        /// </summary>
        Path = 0,

        /// <summary>
        /// Ueber den CLR-Typ der meldenden Klasse. Fuer alles, was hinter einem gemeinsamen Endpunkt sitzt
        /// und sich daher nicht am Pfad erkennen laesst - allen voran die Datei-Behandlungen: hinter
        /// <c>/File/{token}</c> stehen beliebig viele Implementierungen, und welche es ist, weiss man erst
        /// nach dem Aufloesen des Tokens. Der Plugin-Name taugt dafuer nicht, weil zwei Mandanten darunter
        /// verschiedene Klassen fahren koennen; der Typ ist dagegen eine Eigenschaft des Codes.
        /// </summary>
        Type = 1
    }

    /// <summary>
    /// Der Typ eines Asset-Arguments. Bewusst ein kleiner, geschlossener Satz: er steuert die
    /// Eingabemaske und den Vergleich, und beides wird unzuverlaessig, sobald beliebige Typen erlaubt
    /// sind.
    /// </summary>
    public enum AssetArgumentType
    {
        String = 0,
        Int = 1,
        Long = 2,
        Guid = 3,
        Date = 4
    }

    /// <summary>
    /// Was ein Endpunkt ueber eines seiner Argumente sagt. Reine Deklaration - ohne Werte.
    /// </summary>
    /// <param name="Name">der Name des Arguments, so wie er in der Route bzw. im Formular heisst</param>
    /// <param name="Type">der erwartete Typ</param>
    /// <param name="Required">ob der Endpunkt das Argument zwingend braucht</param>
    /// <param name="ResolverKey">
    /// der Aufloeser, mit dem ein Endpunkt ein Unter-Objekt auf diese Ebene normalisiert - "zu dieser
    /// Position gehoert Auftrag 4711". Null, wenn nur direkt verglichen wird. Von einem Endpunkt bei der
    /// Selbstmeldung nie gesetzt; er steht an der Vorlage.
    /// </param>
    public sealed record AssetArgumentDeclaration(string Name, AssetArgumentType Type = AssetArgumentType.String,
        bool Required = true, string ResolverKey = null);

    /// <summary>
    /// Was ein Endpunkt insgesamt deklariert: wie er identifiziert wird und welche Argumente er versteht.
    /// </summary>
    /// <param name="Kind">ob der Konsument ueber seine Route oder ueber seinen Typ erkannt wird</param>
    /// <param name="Key">die Route-Vorlage bzw. der CLR-Typ</param>
    /// <param name="Arguments">die Argumente - in der Reihenfolge, in der die Maske sie zeigen soll</param>
    public sealed record AssetConsumerDeclaration(AssetConsumerKind Kind, string Key,
        AssetArgumentDeclaration[] Arguments);
}
