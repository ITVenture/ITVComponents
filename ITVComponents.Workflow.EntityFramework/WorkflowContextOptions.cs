namespace ITVComponents.Workflow.EntityFramework
{
    /// <summary>
    /// Die tenant-bezogene Einstellung des <see cref="WorkflowContext"/> fuer den DI-Weg - derselbe
    /// Schalter wie der <c>useTenantFilter</c>-Parameter des Plugin-Ctors, nur als aufloesbarer Typ.
    /// </summary>
    /// <remarks>
    /// Der Grund fuer diesen Typ ist ein rein mechanischer: ein <see cref="bool"/> laesst sich aus keinem
    /// ServiceProvider aufloesen, ein <c>IOptions&lt;WorkflowContextOptions&gt;</c> schon. Der Plugin-/
    /// Factory-Weg (Mehr-Instanzen-Betrieb, je Umgebung eine eigene Kontext-Dependency) gibt den Schalter
    /// weiterhin direkt als Parameter mit - dieser Typ <b>ergaenzt</b> ihn, er ersetzt ihn nicht.
    /// </remarks>
    public class WorkflowContextOptions
    {
        /// <summary>
        /// Schaltet die tenant-abhaengige Filterung. <b>Standard: true.</b>
        /// </summary>
        /// <remarks>
        /// Der Standard ist bewusst die restriktive Seite: wer den Kontext ueber die DI aufloest, tut das
        /// im Web-Betrieb - dort ist "zu viel sehen" der teure Fehler, "zu wenig sehen" faellt sofort auf.
        /// <para>
        /// <b>Achtung:</b> <c>false</c> heisst <b>nicht</b> "kein Filter". Sind ueber einen
        /// <see cref="WorkflowFilterInitializer{TContext}"/> Filter im Modell, dann bleiben sie dort und
        /// werten den aktiven Mandanten als <c>null</c> aus - sichtbar sind dann nur noch die
        /// mandantenlosen Zeilen (bei Definitionen: die oeffentlichen). Wirklich filterfrei ist allein der
        /// options-only-Weg (<c>IDbContextFactory&lt;WorkflowContext&gt;</c>), auf dem gar keine
        /// Model-Optionen gesetzt werden - das ist der Weg fuer Runner und Inline-Ausfuehrung.
        /// </para>
        /// </remarks>
        public bool UseTenantFilter { get; set; } = true;
    }
}
