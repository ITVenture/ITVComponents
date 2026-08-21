using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.Workflow.EntityFramework.SqlServer.Migrations
{
    /// <summary>
    /// Zieht den denormalisierten Mandanten an den Token-Zeilen nach, die ihn noch nicht tragen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Kein Schema-Change</b> - die Spalte <c>Tokens.TenantId</c> gibt es seit <c>UserTasks</c>. Sie kam
    /// damals als <c>nullable</c> ohne Nachtrag, weil sie nur die Arbeitsliste bediente: dort faellt eine
    /// leere Zelle nicht auf. Geschrieben wird sie seither bei jedem Speichern einer Instanz
    /// (<c>EfWorkflowStore.SaveInstance</c>), womit alle laufenden Vorgaenge sie laengst haben - <b>ausser
    /// den parkenden</b>. Ein Vorgang, der seit damals auf eine Aufgabe, eine Nachricht oder eine Frist
    /// wartet, wurde in der Zwischenzeit nie gespeichert und traegt weiterhin <c>NULL</c>.
    /// </para>
    /// <para>
    /// Ab jetzt haengt an der Spalte ein Query-Filter. Damit wird aus der leeren Zelle ein anderes
    /// Problem: ein Token, das der Filter verschluckt, liefert kein falsches Ergebnis, sondern einen
    /// Vorgang, der stehen bleibt - ohne Meldung. Deshalb dieser Nachtrag, und deshalb laeuft er
    /// <b>vor</b> dem ersten Start mit der neuen Fassung.
    /// </para>
    /// <para>
    /// Der Nachtrag ist wiederholbar (<c>WHERE t.TenantId IS NULL</c>) und ruehrt Token-Zeilen ohne
    /// zugehoerige Instanz nicht an - die gibt es nicht, der Schluessel verlangt sie; der Join sortiert
    /// sie beilaeufig aus, statt sie mit <c>NULL</c> zu ueberschreiben.
    /// </para>
    /// </remarks>
    public partial class TokenTenantBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                                 UPDATE t
                                 SET t.TenantId = i.TenantId
                                 FROM Tokens t
                                 INNER JOIN WorkflowInstances i ON i.Id = t.InstanceId
                                 WHERE t.TenantId IS NULL AND i.TenantId IS NOT NULL
                                 """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Bewusst leer: der nachgetragene Wert ist der richtige. Ihn beim Zurueckrollen wieder auf
            // NULL zu setzen hiesse, ihn von den Zeilen zu entfernen, die ihn ohnehin schon hatten -
            // unterscheidbar sind die beiden Gruppen danach nicht mehr.
        }
    }
}
