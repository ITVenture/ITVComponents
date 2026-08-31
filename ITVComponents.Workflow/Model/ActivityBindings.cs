using System.Collections.Generic;
using ITVComponents.Workflow.Expressions;

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
        Expression,

        /// <summary>
        /// Der Wert kommt von einem <see cref="ValueHandles.IWorkflowValueHandler"/>, der ueber
        /// <see cref="ActivityInputBinding.HandlerName"/> aufgeloest wird. Der Variablen-Stack traegt
        /// den Wert nicht - er wird geholt, wenn er gebraucht wird, und geschrieben, wenn er sich
        /// geaendert hat.
        /// </summary>
        ValueHandle
    }

    /// <summary>
    /// Was die Aktivitaet bei einer <see cref="ParameterBindingKind.ValueHandle"/>-Bindung bekommt.
    /// </summary>
    public enum ValueDelivery
    {
        /// <summary>
        /// Der Griff (<see cref="ValueHandles.ValueHandle"/>) selbst: die Aktivitaet kennt den
        /// Mechanismus und entscheidet selbst, <b>was</b> sie schreibt und <b>wann</b>. Der Standard,
        /// weil eine Aktivitaet, die den Griff nicht erwartet, ihn sofort und laut als Typfehler
        /// meldet - waehrend ein still ausgepackter Wert erst spaeter auffiele.
        /// </summary>
        Handle,

        /// <summary>
        /// Der ausgepackte Wert: die Aktivitaet weiss von nichts. Bei <b>Referenztypen</b> ist das der
        /// eigentliche Gewinn - sie mutiert dasselbe Objekt, das im Backing-Feld liegt, und die Engine
        /// schreibt es danach zurueck (siehe <see cref="ActivityInputBinding.WriteBack"/>). Bei
        /// Werttypen und Zeichenketten bliebe eine Aenderung unsichtbar; dort muss der Griff durch.
        /// </summary>
        Value
    }

    /// <summary>
    /// Wann die Engine einen ausgepackten Wert (<see cref="ValueDelivery.Value"/>) zurueckschreibt.
    /// </summary>
    public enum ValueWriteBackMode
    {
        /// <summary>Gar nicht - die Bindung ist nur eine Beschaffung.</summary>
        Never,

        /// <summary>
        /// Nach erfolgreicher Ausfuehrung, vor dem Uebernehmen der Ausgaben. Schlaegt die Aktivitaet
        /// fehl, wird nichts geschrieben.
        /// </summary>
        OnSuccess
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

        /// <summary>
        /// Wie <see cref="Source"/> bei <see cref="ParameterBindingKind.Expression"/> zu lesen ist:
        /// EIN Ausdruck (Standard) oder ein ganzes Skript mit <c>return</c>. Bei den anderen
        /// Bindungsarten ohne Bedeutung.
        /// </summary>
        public ScriptMode SourceMode { get; set; } = ScriptMode.Expression;

        /// <summary>
        /// Der konfigurierte Name des <see cref="ValueHandles.IWorkflowValueHandler"/> - nur bei
        /// <see cref="ParameterBindingKind.ValueHandle"/> relevant.
        /// </summary>
        /// <remarks>
        /// Aufgeloest wird ueber die konfigurierten Namen des ausfuehrenden Mandanten, mit Typpruefung -
        /// dieselbe Latte wie bei <c>ActivityRef</c>. Bei einer oeffentlichen Definition trifft der Name
        /// also je Mandant, was dort unter ihm eingerichtet ist.
        /// </remarks>
        public string HandlerName { get; set; }

        /// <summary>
        /// Die Argumente, mit denen der Handler gefragt wird - ueber dieselbe Bindungs-Maschinerie
        /// (Literal, Variable, Ausdruck). Mindestens eines; ein Handler ohne Argument wuesste nicht,
        /// welchen Datensatz er liefern soll.
        /// </summary>
        /// <remarks>
        /// <b>Keine Rekursion:</b> ein Argument darf selbst keine
        /// <see cref="ParameterBindingKind.ValueHandle"/>-Bindung sein.
        /// </remarks>
        public List<ActivityInputBinding> HandlerArguments { get; set; }

        /// <summary>
        /// Ob die Aktivitaet den Griff oder den ausgepackten Wert bekommt - nur bei
        /// <see cref="ParameterBindingKind.ValueHandle"/> relevant.
        /// </summary>
        public ValueDelivery Delivery { get; set; } = ValueDelivery.Handle;

        /// <summary>
        /// Ob die Engine den ausgepackten Wert nach erfolgreicher Ausfuehrung zurueckschreibt - nur bei
        /// <see cref="ValueDelivery.Value"/> relevant. Bei <see cref="ValueDelivery.Handle"/> schreibt
        /// die Aktivitaet selbst.
        /// </summary>
        public ValueWriteBackMode WriteBack { get; set; } = ValueWriteBackMode.Never;

        /// <summary>
        /// Erlaubt eine schreibende Bindung ausdruecklich <b>innerhalb einer parallelen Region</b>.
        /// </summary>
        /// <remarks>
        /// Vorgabe false, und das ist ein Validator-Fehler: schreiben zwei Zweige ueber denselben
        /// Handler mit denselben Argumenten, gewinnt der letzte, und der Zweig-Commit sieht es nicht -
        /// die Konflikterkennung vergleicht Werte aus dem Variablen-Blob, und dort steht bei dieser
        /// Bindungsart nichts. Auf true wird daraus eine Warnung; die Verantwortung (Versionsstempel)
        /// liegt dann beim Handler.
        /// </remarks>
        public bool AllowInParallelRegion { get; set; }
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
