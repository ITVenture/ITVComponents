using System.Collections.Generic;

namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    /// <summary>
    /// Sammelt, welche Endpunkte Argumente eines geteilten Assets verstehen. Ein Endpunkt meldet sich
    /// selbst - typischerweise im Konstruktor -, und die Registry merkt sich das prozessweit und
    /// (sofern eingeschaltet) ueber den Neustart hinaus.
    /// <para>
    /// <b>Sie ist keine Sicherheitsschranke.</b> Sie beantwortet nicht "darf der jetzt", sondern "ergibt
    /// eine Freigabe an dieser Stelle ueberhaupt Sinn". Sicher wird der Zugriff allein durch die
    /// Bestaetigung der Argumente im Endpunkt selbst.
    /// </para>
    /// <para>
    /// Ihr eigentlicher Nutzen ist die Teilen-Maske: nur wer weiss, welche Argumente eine Seite braucht,
    /// kann sie aus dem laufenden Kontext vorbelegen, statt den Benutzer eine Nummer abtippen zu lassen,
    /// die zwei Zentimeter weiter oben auf dem Bildschirm steht.
    /// </para>
    /// </summary>
    public interface IAssetArgumentRegistry
    {
        /// <summary>
        /// Meldet, welche Argumente ein Endpunkt versteht. Idempotent und billig genug fuer einen
        /// Konstruktor: bekannte, unveraenderte Meldungen kosten einen Vergleich im Speicher und keinen
        /// Datenbankzugriff.
        /// <para>
        /// Die Meldung ist die <b>vollstaendige</b> Liste aus Sicht dieses Endpunkts - verschwundene
        /// Argumente werden entfernt, neue kommen dazu.
        /// </para>
        /// </summary>
        /// <param name="declaration">was der Endpunkt versteht</param>
        void Declare(AssetConsumerDeclaration declaration);

        /// <summary>
        /// Bequeme Fassung von <see cref="Declare(AssetConsumerDeclaration)"/>.
        /// </summary>
        /// <param name="kind">ob der Konsument ueber seine Route oder ueber seinen Typ erkannt wird</param>
        /// <param name="key">die Route-Vorlage bzw. der CLR-Typ</param>
        /// <param name="arguments">die Argumente in Anzeigereihenfolge</param>
        void Declare(AssetConsumerKind kind, string key, params AssetArgumentDeclaration[] arguments);

        /// <summary>
        /// Alle bekannten Konsumenten. Kann nach einem Neustart unvollstaendig sein, solange die
        /// Persistenz abgeschaltet ist - deshalb darf eine Pruefung darauf nur warnen, nie blockieren.
        /// </summary>
        /// <returns>die bekannten Deklarationen</returns>
        IReadOnlyList<AssetConsumerDeclaration> GetConsumers();

        /// <summary>
        /// Sucht einen Konsumenten. Liefert null, wenn er (noch) nicht bekannt ist.
        /// </summary>
        /// <param name="kind">die Art der Identifikation</param>
        /// <param name="key">die Route-Vorlage bzw. der CLR-Typ</param>
        /// <returns>die Deklaration oder null</returns>
        AssetConsumerDeclaration Find(AssetConsumerKind kind, string key);
    }
}
