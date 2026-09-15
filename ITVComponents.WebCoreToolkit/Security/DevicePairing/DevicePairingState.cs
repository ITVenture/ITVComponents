namespace ITVComponents.WebCoreToolkit.Security.DevicePairing
{
    /// <summary>
    /// Der Zustand eines Kopplungsvorgangs.
    /// </summary>
    /// <remarks>
    /// <b>Die Werte werden NUMERISCH gespeichert - nur ans Ende anhaengen, nie dazwischenschieben und
    /// nie umnummerieren.</b> Ein verschobener Wert macht aus wartenden Vorgaengen stillschweigend
    /// abgelieferte.
    /// </remarks>
    public enum DevicePairingState
    {
        /// <summary>Angelegt, wartet auf die Bestaetigung durch einen angemeldeten Benutzer.</summary>
        Pending = 0,

        /// <summary>Bestaetigt; der Zugang ist angelegt, das Geheimnis noch nicht abgeholt.</summary>
        Confirmed = 1,

        /// <summary>
        /// Das Geheimnis wurde abgeholt. Endzustand - ein zweiter Abruf bekommt es nicht mehr, sonst
        /// holte es ein spaeterer Mitleser nochmals ab.
        /// </summary>
        Delivered = 2,

        /// <summary>Die Frist ist abgelaufen, ohne dass jemand bestaetigt hat.</summary>
        Expired = 3,

        /// <summary>Ausdruecklich abgelehnt.</summary>
        Denied = 4
    }
}
