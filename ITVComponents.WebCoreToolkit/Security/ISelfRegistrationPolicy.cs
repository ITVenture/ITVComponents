namespace ITVComponents.WebCoreToolkit.Security
{
    /// <summary>
    /// Ob sich jemand ohne Einladung selbst ein Konto anlegen darf.
    /// </summary>
    /// <remarks>
    /// Der Vertrag liegt hier und nicht bei der Anmeldung oder beim Onboarding, weil ihn beide Seiten
    /// brauchen und keine die andere kennen darf: die Anmeldeseite entscheidet damit, ob sie den Verweis
    /// auf die Registrierung ueberhaupt anbietet, und die Registrierungsseite selbst, ob sie einen Vorgang
    /// annimmt. Waeren das zwei getrennte Einstellungen, liefen Anzeige und Pruefung frueher oder spaeter
    /// auseinander - und dann steht entweder ein Verweis da, der abgewiesen wird, oder eine offene Seite,
    /// zu der kein Weg fuehrt.
    /// <para>
    /// Ist keine Umsetzung registriert, gibt es keine Aussage: die Anmeldeseite bleibt dann bei dem, was
    /// ihre eigene Konfiguration sagt. Ein Host ohne Onboarding-Paket verhaelt sich damit unveraendert.
    /// </para>
    /// </remarks>
    public interface ISelfRegistrationPolicy
    {
        /// <summary>Darf sich jemand ohne Einladung selbst registrieren?</summary>
        bool AllowsSelfRegistration { get; }
    }
}
