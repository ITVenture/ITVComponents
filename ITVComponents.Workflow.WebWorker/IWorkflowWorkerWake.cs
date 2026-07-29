namespace ITVComponents.Workflow.WebWorker
{
    /// <summary>
    /// Wake-Hook: erlaubt es signal-/enqueue-nahen Code-Stellen (z.B. der Signal-Zustellung aus dem UI),
    /// einen Deskriptor sofort auf "heiss" zu setzen, damit frisch eingebrachte Arbeit nicht bis zum
    /// Max-Linger wartet. Timer werden ohnehin praezise terminiert; dieser Hook ist fuer die
    /// signalgetriebene Arbeit gedacht.
    /// </summary>
    public interface IWorkflowWorkerWake
    {
        /// <summary>
        /// Weckt den Deskriptor der angegebenen Umgebung (und optional des angegebenen Tenants). Unbekannte
        /// Kombinationen werden still ignoriert (der naechste Refresh/Poll faengt die Arbeit ohnehin).
        /// </summary>
        /// <param name="environmentName">Name der Umgebung; null/leer = die Default-Umgebung (kein Tenant-Segment).</param>
        /// <param name="tenantId">Der Tenant-Name; null = der globale (filterfreie) Deskriptor der Umgebung.</param>
        void Poke(string? environmentName, string? tenantId);
    }
}
