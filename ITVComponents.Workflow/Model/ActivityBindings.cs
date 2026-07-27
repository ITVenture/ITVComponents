namespace ITVComponents.Workflow.Model
{
    /// <summary>
    /// Wie eine Aktivitaet ihre deklarierten Ausgaben in den Variablen-Scope der Instanz einbringt.
    /// </summary>
    public enum ActivityScopeMode
    {
        /// <summary>
        /// Standard: die Ausgaben werden in den bestehenden Scope gemergt (additiv) - vorhandene
        /// Variablen bleiben erhalten.
        /// </summary>
        Extend,

        /// <summary>
        /// Konsolidierung: nach der Aktivitaet besteht der Scope <b>genau</b> aus den deklarierten
        /// Ausgaben (plus einer optionalen Erhaltungs-Whitelist, siehe
        /// <see cref="AutomatedActivityNode.RetainVariables"/>) - alle uebrigen Variablen werden
        /// abgeraeumt. So schleppt ein Workflow nach grossen (auch parallelen) Zwischenschritten keine
        /// nicht mehr benoetigten Daten mit. Nur auf einem Ein-Zweig-Segment sinnvoll (typisch nach
        /// einem Join); der Validator warnt bei Platzierung innerhalb einer parallelen Region.
        /// </summary>
        Replace
    }

    /// <summary>
    /// Woher der Wert eines Eingabeparameters stammt.
    /// </summary>
    public enum ParameterBindingKind
    {
        /// <summary>Ein konstanter, fest hinterlegter Wert.</summary>
        Literal,

        /// <summary>Der aktuelle Wert einer Instanz-Variable (per Name).</summary>
        Variable,

        /// <summary>Das Ergebnis eines CScript-Ausdrucks ueber den Variablen der Instanz.</summary>
        Expression
    }

    /// <summary>
    /// Bindet einen deklarierten <b>Eingabe</b>-Parameter einer Aktivitaet an eine Wertquelle.
    /// Die Engine loest die Bindung vor der Ausfuehrung auf und legt das Ergebnis in
    /// <see cref="Activities.WorkflowActivityContext.Inputs"/> ab.
    /// </summary>
    /// <remarks>
    /// Bewusst stark typisiert (nicht im <c>object</c>-Dictionary des Knotens): so ueberlebt die
    /// Bindung den JSON-Round-Trip des Stores typtreu, statt als <c>JsonElement</c> zurueckzukommen.
    /// </remarks>
    public class ActivityInputBinding
    {
        /// <summary>Der Name des deklarierten Eingabeparameters, den diese Bindung fuellt.</summary>
        public string Parameter { get; set; }

        /// <summary>Die Art der Wertquelle (Standard: konstanter Wert).</summary>
        public ParameterBindingKind Kind { get; set; } = ParameterBindingKind.Literal;

        /// <summary>Der konstante Wert - nur bei <see cref="ParameterBindingKind.Literal"/> relevant.</summary>
        public object Literal { get; set; }

        /// <summary>
        /// Der Variablenname (bei <see cref="ParameterBindingKind.Variable"/>) bzw. der
        /// CScript-Ausdruck (bei <see cref="ParameterBindingKind.Expression"/>).
        /// </summary>
        public string Source { get; set; }
    }

    /// <summary>
    /// Bildet einen deklarierten <b>Ausgabe</b>-Parameter einer Aktivitaet auf eine Instanz-Variable
    /// ab. Nach der Ausfuehrung schreibt die Engine den von der Aktivitaet in
    /// <see cref="Activities.WorkflowActivityContext.Outputs"/> abgelegten Wert in diese Variable.
    /// </summary>
    public class ActivityOutputBinding
    {
        /// <summary>Der Name des deklarierten Ausgabeparameters.</summary>
        public string Parameter { get; set; }

        /// <summary>Die Ziel-Variable, in der das Ergebnis abgelegt wird.</summary>
        public string Variable { get; set; }
    }
}
