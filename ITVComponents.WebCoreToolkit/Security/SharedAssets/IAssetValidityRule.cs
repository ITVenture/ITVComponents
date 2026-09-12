namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    /// <summary>
    /// Eine Bedingung, unter der eine Freigabe gilt - <b>statt</b> oder zusaetzlich zu einem Datum.
    /// <para>
    /// "Bis der Auftrag abgeschlossen ist" ist kein Ablauf. Wer das als Frist nachbaut, liegt immer
    /// daneben: mal ist der Auftrag frueher fertig, mal spaeter. Deshalb benennt die Vorlage eine Regel,
    /// die der Host implementiert - er ist der Einzige, der die Frage beantworten kann.
    /// </para>
    /// <para>
    /// <b>Was zur Verfuegung steht:</b> der Mandanten-Geltungsbereich ist gesetzt, und die Plugin-Fabrik
    /// ist benutzbar - eine Regel darf also ihren Fachkontext auf dem normalen Weg leasen
    /// (<c>IFreshInjectablePlugin&lt;T&gt;</c>) und braucht keine eigene Verbindung. Gefragt wird sie
    /// <b>einmal je Vorgang</b>, vom seitenzugewandten Weg aus - nicht je Anfrage und nicht beim
    /// Aufloesen der Freigabe. Wer sie anderswoher ruft, nimmt ihr genau diese Zusage; siehe die
    /// Bemerkung an <see cref="ISharedAssetAdapter.VerifyAssetValidity"/>.
    /// </para>
    /// <para>
    /// <b>Sie wird nicht fuer Unterressourcen gefragt.</b> Skripte, Stylesheets und Bilder einer Seite
    /// tragen denselben Freigabe-Abschnitt, loesen aber keinen Vorgang aus.
    /// </para>
    /// </summary>
    public interface IAssetValidityRule
    {
        /// <summary>
        /// Der Schluessel, unter dem eine Vorlage diese Regel benennt.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Prueft, ob die Freigabe mit diesen Argumentwerten noch gilt.
        /// </summary>
        /// <param name="values">worauf die Freigabe zeigt</param>
        /// <returns>
        /// false beendet den Zugriff - genauso hart wie ein abgelaufenes Datum. Wer sich nicht sicher
        /// ist, antwortet false: eine Freigabe, die zu lange gilt, ist schlimmer als eine, die zu frueh
        /// endet.
        /// </returns>
        bool IsValid(AssetArgumentValues values);
    }
}
