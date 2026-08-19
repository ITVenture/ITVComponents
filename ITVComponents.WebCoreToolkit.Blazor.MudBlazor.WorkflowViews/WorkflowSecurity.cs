namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews
{
    /// <summary>
    /// Die Namen von Feature und Berechtigungen dieses Moduls. Das Feature schaltet den Bereich
    /// frei, die Berechtigungen gaten die einzelnen Aspekte.
    /// </summary>
    public static class WorkflowSecurity
    {
        /// <summary>Feature, das den gesamten Workflow-Bereich freischaltet (pro Mandant aktiviert).</summary>
        public const string Feature = "ITVWorkflow";

        /// <summary>
        /// Die Systemverwalter-Berechtigung. Sie gatet das, was eine Aussage ueber <b>alle</b> Mandanten
        /// ist: welches Feature und welche Berechtigung eine oeffentliche Definition verlangt.
        /// </summary>
        /// <remarks>
        /// Als Name wiederholt und nicht ueber <c>ToolkitPermission.Sysadmin</c> bezogen: der liegt im
        /// TenantSecurity-Paket, und dieses Ansichten-Paket soll sich dafuer keine Abhaengigkeit auf die
        /// Mandanten-Persistenz einhandeln. Die Berechtigung ist ein <b>Name</b> - genau deshalb steht
        /// sie in Vertraegen ueberall als Zeichenkette.
        /// </remarks>
        public const string Sysadmin = "Sysadmin";

        /// <summary>Aspekt: laufende Instanzen ansehen (read-only).</summary>
        public const string Monitor = "Workflow.Monitor";

        /// <summary>Aspekt: operativ eingreifen (Signal senden, abbrechen).</summary>
        public const string Operate = "Workflow.Operate";

        /// <summary>
        /// Aspekt: eine neue Instanz von Hand starten. Bewusst getrennt von <see cref="Operate"/>: einen
        /// laufenden Prozess anzustossen oder abzubrechen ist Betrieb an etwas Bestehendem - einen neuen
        /// Geschaeftsfall zu eroeffnen ist eine fachliche Handlung, die typisch andere Leute duerfen.
        /// </summary>
        /// <remarks>
        /// Ergaenzt <see cref="Monitor"/>, ersetzt es nicht: der Startknopf sitzt in der Instanz-Uebersicht,
        /// die <see cref="Monitor"/> verlangt. Wer starten koennen soll, braucht also beide - was zusammen
        /// passt, denn eine gestartete Instanz will man auch sehen.
        /// </remarks>
        public const string Start = "Workflow.Start";

        /// <summary>Aspekt: Definitionen anlegen/bearbeiten (Editor).</summary>
        public const string Design = "Workflow.Design";

        /// <summary>
        /// Aspekt: <b>oeffentliche</b> Definitionen anlegen und aendern - solche, die jedem Mandanten
        /// gehoeren und von jedem gestartet werden koennen.
        /// </summary>
        /// <remarks>
        /// Bewusst getrennt von <see cref="Design"/> und bewusst „Design" und nicht „Create": auch das
        /// AENDERN einer bestehenden oeffentlichen Definition wirkt auf alle Mandanten. Wer sie nur im
        /// eigenen Mandanten modelliert, richtet dort Schaden an; wer eine oeffentliche aendert, bei
        /// allen. Verlangt wird die Berechtigung zusaetzlich zu <see cref="Design"/>.
        /// </remarks>
        public const string DesignPublic = "Workflow.DesignPublic";

        /// <summary>
        /// Aspekt: die eigenen Aufgaben sehen und erledigen. Bewusst getrennt von
        /// <see cref="Monitor"/>/<see cref="Operate"/>: die Arbeitsliste ist eine <b>Benutzer</b>-Ansicht -
        /// wer Rechnungen freigibt, braucht deswegen keinen Blick in fremde Instanzen. Die feine
        /// Zustaendigkeit macht die Permission am Knoten
        /// (<see cref="ITVComponents.Workflow.Model.UserActivityNode.RequiredPermission"/>).
        /// </summary>
        public const string Tasks = "Workflow.Tasks";

        /// <summary>
        /// Aspekt: <b>fremde</b> Aufgaben umtragen - die Vertretung. Wer sie hat, darf jede offene Aufgabe
        /// seines Mandanten einem anderen Bearbeiter geben oder in den Pool zurueckstellen.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Bewusst <b>ohne</b> die fachliche Permission des Knotens: wer die Aufgaben eines Erkrankten
        /// verteilt, muss sie nicht selbst erledigen duerfen. Umtragen ist eine organisatorische Handlung -
        /// sie zeigt Titel und Zustaendigkeit, aber nie den Inhalt der Maske. Das Oeffnen der Aufgabe
        /// bleibt an der Knoten-Permission haengen, und daran aendert diese Berechtigung nichts.
        /// </para>
        /// <para>
        /// Wer sie NICHT hat, kann trotzdem umtragen - nur eingeschraenkt: die eigene Aufgabe abgeben oder
        /// zuruecklegen und eine Pool-Aufgabe an sich nehmen. Das braucht keine eigene Berechtigung, denn
        /// es ist dieselbe Handlung, die er mit dem Erledigen ohnehin vollziehen duerfte.
        /// </para>
        /// </remarks>
        public const string AssignTasks = "Workflow.AssignTasks";
    }
}
