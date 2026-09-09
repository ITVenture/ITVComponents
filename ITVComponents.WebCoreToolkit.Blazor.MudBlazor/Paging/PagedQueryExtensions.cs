using System.Linq.Expressions;

namespace ITVComponents.WebCoreToolkit.Blazor.Paging;

/// <summary>
/// Schneidet eine Seite aus einer sortierten Abfrage - und verlangt dafuer einen eindeutigen Schluessel.
/// </summary>
/// <remarks>
/// <para>
/// <b>Warum der Schluessel ein Pflichtargument ist:</b> Sortiert eine Liste nach einer Spalte, die
/// mehrfach denselben Wert tragen kann, ist die Reihenfolge der gleichstehenden Zeilen <b>undefiniert</b>.
/// Jede Seite ist eine eigene Abfrage mit eigenem <c>OFFSET</c>, und der Server darf die Gleichstaende
/// jedes Mal anders anordnen: dann steht eine Zeile auf zwei Seiten und eine andere faellt zwischen sie.
/// Die Gesamtzahl im Blaetterbalken stimmt dabei weiter - nur sehen kann man die fehlende Zeile nie.
/// </para>
/// <para>
/// Ein realer Fall: die Navigations-Zuordnung am Mandanten sortierte nach <c>DisplayName</c>. In einem
/// Menuebaum sind gleiche Anzeigenamen der Normalfall, und ein Teil der Eintraege war schlicht nicht
/// erreichbar. PostgreSQL waehlt fuer verschiedene Offsets gern verschiedene Plaene und macht das damit
/// sichtbar; garantiert war die Reihenfolge auf keiner Datenbank.
/// </para>
/// <para>
/// <b>Merke:</b> „eindeutig" heisst eindeutig <i>in der abgefragten Menge</i>. Viele Namensspalten des
/// Toolkits sind nur zusammen mit dem Mandanten eindeutig (<c>RoleNameUniqueness</c>,
/// <c>PermissionNameUniqueness</c>, <c>PluginNameUniqueness</c>, <c>UrlUniqueness</c>) - in einer Sicht
/// ueber alle Mandanten reichen sie nicht. Im Zweifel den Primaerschluessel nehmen; er kostet nichts und
/// ist immer richtig.
/// </para>
/// </remarks>
public static class PagedQueryExtensions
{
    /// <summary>Haengt den eindeutigen Nachschluessel an und schneidet die Seite heraus.</summary>
    /// <typeparam name="T">der Zeilentyp</typeparam>
    /// <typeparam name="TKey">der Typ des eindeutigen Schluessels</typeparam>
    /// <param name="ordered">die bereits fachlich sortierte Abfrage</param>
    /// <param name="uniqueKey">ein in der abgefragten Menge eindeutiger Schluessel - meist der Primaerschluessel</param>
    /// <param name="page">die gewuenschte Seite, nullbasiert</param>
    /// <param name="pageSize">die Seitengroesse</param>
    public static IQueryable<T> Page<T, TKey>(this IOrderedQueryable<T> ordered,
        Expression<Func<T, TKey>> uniqueKey, int page, int pageSize)
        => ordered.ThenBy(uniqueKey).Skip(page * pageSize).Take(pageSize);

    /// <summary>Wie oben, mit Seite und Groesse aus der Abfrage.</summary>
    public static IQueryable<T> Page<T, TKey>(this IOrderedQueryable<T> ordered,
        Expression<Func<T, TKey>> uniqueKey, ListQuery query)
        => ordered.Page(uniqueKey, query.Page, query.PageSize);

    /// <summary>
    /// Fuer einen zusammengesetzten Schluessel - erst zusammen sind die beiden Teile eindeutig (etwa
    /// Anmeldeanbieter und Anbieterschluessel eines Identity-Logins).
    /// </summary>
    public static IQueryable<T> Page<T, TKey1, TKey2>(this IOrderedQueryable<T> ordered,
        Expression<Func<T, TKey1>> uniqueKeyPart1, Expression<Func<T, TKey2>> uniqueKeyPart2, ListQuery query)
        => ordered.ThenBy(uniqueKeyPart1).ThenBy(uniqueKeyPart2)
            .Skip(query.Page * query.PageSize).Take(query.PageSize);

    /// <summary>
    /// Dasselbe fuer eine bereits im Speicher liegende Liste. Dort ist die Reihenfolge zwar stabil, aber
    /// nur solange die Quelle es ist - und eine Liste, die heute im Speicher sortiert wird, ist morgen
    /// eine Abfrage.
    /// </summary>
    public static IEnumerable<T> Page<T, TKey>(this IOrderedEnumerable<T> ordered,
        Func<T, TKey> uniqueKey, int page, int pageSize)
        => ordered.ThenBy(uniqueKey).Skip(page * pageSize).Take(pageSize);
}
