namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    /// <summary>
    /// Eine Bedingung, unter der eine Freigabe gilt - <b>statt</b> oder zusaetzlich zu einem Datum.
    /// <para>
    /// "Bis der Auftrag abgeschlossen ist" ist kein Ablauf. Wer das als Frist nachbaut, liegt immer
    /// daneben: mal ist der Auftrag frueher fertig, mal spaeter. Deshalb benennt die Vorlage eine Regel,
    /// die der Host implementiert - er ist der Einzige, der die Frage beantworten kann.
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
