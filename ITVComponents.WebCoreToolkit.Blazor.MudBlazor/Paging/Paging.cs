namespace ITVComponents.WebCoreToolkit.Blazor.Paging;

/// <summary>
/// Die Abfrage einer Liste: welche Seite, wie gross, wonach sortiert, wonach gesucht.
/// </summary>
/// <remarks>
/// <para>
/// Lag bis hierher <b>zweimal</b> Feld fuer Feld identisch im selben Paket - einmal als
/// <c>TenantSecurityViews.ViewModels.ListQuery</c> und einmal als
/// <c>AspNetCoreTenantSecurityUserView.ViewModels.UserListQuery</c>. Zwei Namen fuer dieselbe Sache
/// heisst: jede Erweiterung ist zweimal zu machen, und wer nur eine macht, merkt es nicht.
/// </para>
/// <para>
/// Die dritte Fassung (<c>WorkflowViews.Common</c>) ist <b>nicht</b> mitgekommen und heisst jetzt
/// <c>WorkflowListQuery</c>: sie traegt statt <see cref="TenantId"/> ein <c>Status</c>-Feld. Sie sah nur
/// gleich aus, weil sie gleich hiess - genau die Verwechslung, die der eigene Name jetzt ausschliesst.
/// </para></remarks>
public sealed class ListQuery
{
    /// <summary>Die gewuenschte Seite, nullbasiert.</summary>
    public int Page { get; init; }

    /// <summary>Die Seitengroesse.</summary>
    public int PageSize { get; init; } = 25;

    /// <summary>Die Spalte, nach der sortiert wird, oder null.</summary>
    public string? SortColumn { get; init; }

    /// <summary>Absteigend statt aufsteigend.</summary>
    public bool SortDescending { get; init; }

    /// <summary>Der Suchbegriff, oder null.</summary>
    public string? Search { get; init; }

    /// <summary>Der Mandant, auf den eingeschraenkt wird, oder null.</summary>
    public int? TenantId { get; init; }
}

/// <summary>
/// Eine Seite eines Listen-Ergebnisses samt der Gesamtzahl - die braucht der Blaetterbalken, um zu
/// wissen, wie viele Seiten es gibt.
/// </summary>
/// <typeparam name="T">der Zeilentyp</typeparam>
/// <remarks>
/// Lag bis hierher <b>dreimal</b> vollkommen identisch im Repo. Anders als bei den Abfragen gab es hier
/// nicht einmal einen Unterschied im Feldbestand.
/// </remarks>
public sealed class PagedResult<T>
{
    /// <summary>Die Zeilen dieser Seite.</summary>
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    /// <summary>Wie viele Zeilen es insgesamt gibt - nicht, wie viele auf dieser Seite stehen.</summary>
    public int TotalCount { get; init; }
}

/// <summary>
/// Der Rahmen, in dem eine Verwaltungs-Maske arbeitet: wer fragt, und in welchem Mandanten.
/// </summary>
/// <remarks>
/// Lag bis hierher zweimal identisch - als <c>AdminContext</c> und als <c>UserListContext</c>.
/// </remarks>
public sealed class AdminContext
{
    /// <summary>Ob der Aufrufer alles sieht.</summary>
    public bool IsSysAdmin { get; init; }

    /// <summary>Der Mandant, in dem er gerade arbeitet, oder null.</summary>
    public int? CurrentTenantId { get; init; }
}
