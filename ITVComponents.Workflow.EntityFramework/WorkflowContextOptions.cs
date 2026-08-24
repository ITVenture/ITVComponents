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
        /// Model-Optionen gesetzt werden.
        /// </para>
        /// <para>
        /// <b>Wer welchen Weg nimmt:</b> der <b>Runner</b> braucht den filterfreien - sein Suchlauf geht
        /// jedem <c>WorkflowExecutionScope</c> voraus, und <c>EfWorkflowStore.LoadInstances</c> zieht die
        /// Mandantengrenze zentral fuer alle Aufgriffs-Wege; mit Filter faende er nichts, was einem
        /// Mandanten gehoert. <c>AddWorkflowWebWorker</c> nimmt deshalb von sich aus die Kontext-Fabrik.
        /// Die <b>Ansichten</b> nehmen den umgekehrten Weg: sie leasen pro Operation ueber
        /// <c>IFreshInjectablePlugin&lt;WorkflowContext&gt;</c>, und dort ist der Filter erwuenscht. Beide
        /// Wege koennen (und sollen) im selben Prozess auf dieselbe Datenbank zeigen.
        /// </para>
        /// </remarks>
        public bool UseTenantFilter { get; set; } = true;
    }
}
