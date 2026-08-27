namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    /// <summary>
    /// Normalisiert ein Argument nach oben: der Endpunkt kennt ein Unter-Objekt, die Freigabe zeigt auf
    /// das darueberliegende.
    /// <para>
    /// Das ist der einzige Teil der Objektsicherheit, den das Toolkit nicht wissen kann. Ob Position 815
    /// zu Auftrag 4711 gehoert, weiss allein der Host - aber er muss es nur <b>einmal je Argumenttyp</b>
    /// sagen und nicht an jedem Endpunkt. Der Datei-Endpunkt ist der Musterfall: er kennt nach dem
    /// Aufloesen seines Tokens eine Datei-Kennung, die Freigabe aber einen Auftrag.
    /// </para>
    /// </summary>
    public interface IAssetArgumentResolver
    {
        /// <summary>
        /// Der Schluessel, unter dem eine Vorlage diesen Aufloeser an einem ihrer Argumente benennt.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Versucht, aus dem gemeldeten Wert den Wert auf der geteilten Ebene zu bestimmen.
        /// </summary>
        /// <param name="targetArgument">der Name des Arguments der Freigabe (z.B. <c>orderId</c>)</param>
        /// <param name="sourceArgument">der Name des gemeldeten Arguments (z.B. <c>positionId</c>)</param>
        /// <param name="sourceValue">der gemeldete Wert</param>
        /// <param name="resolvedValue">der Wert auf der geteilten Ebene</param>
        /// <returns>
        /// true, wenn sich der Wert bestimmen liess. <b>false ist kein Fehler</b>, sondern heisst
        /// schlicht: dieser Aufloeser ist hier nicht zustaendig oder findet keinen Bezug - der Zugriff
        /// gilt dann als nicht bestaetigt.
        /// </returns>
        bool TryResolve(string targetArgument, string sourceArgument, object sourceValue, out object resolvedValue);
    }
}
