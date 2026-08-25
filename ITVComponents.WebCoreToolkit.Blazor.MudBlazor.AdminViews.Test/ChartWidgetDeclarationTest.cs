using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Widgets;
using ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Widgets.Charts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.Test
{
    /// <summary>
    /// Der Weg von der Deklaration zum Diagramm - alles, was nicht im Browser haengt.
    /// </summary>
    /// <remarks>
    /// Beide Renderer enden in derselben Zwischenform; deshalb ist hier geprueft, was danach kommt, und
    /// nicht zweimal dasselbe je Sprache.
    /// </remarks>
    [TestClass]
    public class ChartWidgetDeclarationTest
    {
        private static IDictionary<string, object?> Map(params (string Key, object? Value)[] entries)
            => entries.ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase);

        [TestMethod]
        public void MinimalDeclaration_IsRead()
        {
            var errors = new List<string>();
            ChartWidgetDeclaration? declaration = ChartWidgetDeclaration.FromMap(Map(
                ("type", "pie"),
                ("labels", new object?[] { "Offen", "Erledigt" }),
                ("series", new object?[]
                {
                    Map(("name", "Anzahl"), ("data", new object?[] { 3, 7 }))
                })), errors);

            Assert.IsNotNull(declaration);
            CollectionAssert.AreEqual(Array.Empty<string>(), errors);
            Assert.AreEqual(ChartType.Pie, declaration!.Type);
            CollectionAssert.AreEqual(new[] { "Offen", "Erledigt" },
                declaration.Labels.Select(l => l.Text).ToArray());
            Assert.AreEqual(1, declaration.Series.Count);
            CollectionAssert.AreEqual(new[] { 3d, 7d }, declaration.Series[0].Data.ToArray());
        }

        /// <summary>
        /// Gross-/Kleinschreibung darf nicht entscheiden: 'Type' und 'type' sind sonst genau der
        /// Unterschied, der eine leere Kachel erzeugt - und CScript schreibt sich anders als JSON.
        /// </summary>
        [TestMethod]
        public void FieldNames_AreCaseInsensitive()
        {
            var errors = new List<string>();
            ChartWidgetDeclaration? declaration = ChartWidgetDeclaration.FromMap(Map(
                ("Type", "Bar"),
                ("Series", new object?[] { Map(("Name", "x"), ("Data", new object?[] { 1 })) })), errors);

            Assert.IsNotNull(declaration);
            Assert.AreEqual(ChartType.Bar, declaration!.Type);
        }

        /// <summary>Ein echter Enum-Wert aus CScript (<c>ChartType.Donut</c>) kommt genauso an.</summary>
        [TestMethod]
        public void TypeMayBeTheEnumItself()
        {
            var errors = new List<string>();
            ChartWidgetDeclaration? declaration = ChartWidgetDeclaration.FromMap(Map(
                ("type", ChartType.Donut),
                ("series", new object?[] { Map(("name", "x"), ("data", new object?[] { 1 })) })), errors);

            Assert.AreEqual(ChartType.Donut, declaration!.Type);
        }

        [TestMethod]
        public void LabelMayCarryANavigationTarget()
        {
            var errors = new List<string>();
            ChartWidgetDeclaration? declaration = ChartWidgetDeclaration.FromMap(Map(
                ("type", "pie"),
                ("labels", new object?[]
                {
                    Map(("text", "Offen"), ("navigateTo", "Orders?status=open")),
                    "Erledigt"
                }),
                ("series", new object?[] { Map(("name", "x"), ("data", new object?[] { 1, 2 })) })), errors);

            Assert.AreEqual("Offen", declaration!.Labels[0].Text);
            Assert.AreEqual("Orders?status=open", declaration.Labels[0].NavigateTo);
            Assert.AreEqual("Erledigt", declaration.Labels[1].Text);
            Assert.IsNull(declaration.Labels[1].NavigateTo);
        }

        /// <summary>
        /// Zahlen und Beschriftungen muessen zusammenpassen. Stillschweigend abzuschneiden waere die
        /// schlechteste Auskunft: die Zahlen saehen richtig aus und waeren es nicht.
        /// </summary>
        [TestMethod]
        public void MismatchedLengths_AreReported()
        {
            var errors = new List<string>();
            ChartWidgetDeclaration.FromMap(Map(
                ("type", "pie"),
                ("labels", new object?[] { "a", "b", "c" }),
                ("series", new object?[] { Map(("name", "x"), ("data", new object?[] { 1, 2 })) })), errors);

            Assert.IsTrue(errors.Any(e => e.Contains("label(s)", StringComparison.Ordinal)),
                string.Join(" | ", errors));
        }

        [TestMethod]
        public void UnknownType_NamesTheKnownOnes()
        {
            var errors = new List<string>();
            ChartWidgetDeclaration.FromMap(Map(
                ("type", "kuchen"),
                ("series", new object?[] { Map(("name", "x"), ("data", new object?[] { 1 })) })), errors);

            Assert.IsTrue(errors.Any(e => e.Contains("Pie", StringComparison.Ordinal)), string.Join(" | ", errors));
        }

        [TestMethod]
        public void NonNumericValue_IsReported()
        {
            var errors = new List<string>();
            ChartWidgetDeclaration.FromMap(Map(
                ("type", "bar"),
                ("series", new object?[] { Map(("name", "x"), ("data", new object?[] { 1, "viele" })) })), errors);

            Assert.IsTrue(errors.Any(e => e.Contains("not a number", StringComparison.Ordinal)),
                string.Join(" | ", errors));
        }

        /// <summary>Alles, was kein aufbereitetes Feld ist, geht als Durchreiche weiter.</summary>
        [TestMethod]
        public void OtherFields_EndUpInExtra()
        {
            var errors = new List<string>();
            ChartWidgetDeclaration? declaration = ChartWidgetDeclaration.FromMap(Map(
                ("type", "pie"),
                ("series", new object?[] { Map(("name", "x"), ("data", new object?[] { 1 })) }),
                ("width", "300px")), errors);

            Assert.IsTrue(declaration!.Extra.ContainsKey("width"));
        }

        // ---- Durchreiche-Bindung ------------------------------------------------------------------

        [TestMethod]
        public void KnownParameter_IsConvertedAndNamedAsTheComponentDoes()
        {
            var errors = new List<string>();
            Dictionary<string, object?> bound = ChartParameterBinder.Bind(
                Map(("width", "300px"), ("canHideSeries", true), ("legendPosition", "bottom"))
                    .ToDictionary(e => e.Key, e => e.Value),
                typeof(MudChart<double>), errors);

            CollectionAssert.AreEqual(Array.Empty<string>(), errors);
            Assert.AreEqual("300px", bound["Width"]);
            Assert.AreEqual(true, bound["CanHideSeries"]);
            Assert.AreEqual(Position.Bottom, bound["LegendPosition"]);
        }

        /// <summary>
        /// Der wichtigste Test dieser Datei: ein verschriebener Name wirft bei MudBlazor NICHT (die
        /// Komponenten fangen unbekannte Attribute ueber UserAttributes ab), er waere also wirkungslos
        /// und unsichtbar. Deshalb muss die Bindung ihn selbst beanstanden.
        /// </summary>
        [TestMethod]
        public void UnknownParameter_IsReported()
        {
            var errors = new List<string>();
            ChartParameterBinder.Bind(
                new Dictionary<string, object?> { { "legendPositon", "bottom" } },
                typeof(MudChart<double>), errors);

            Assert.AreEqual(1, errors.Count, string.Join(" | ", errors));
            StringAssert.Contains(errors[0], "legendPositon");
        }

        [TestMethod]
        public void ReservedParameter_IsReported()
        {
            var errors = new List<string>();
            ChartParameterBinder.Bind(
                new Dictionary<string, object?> { { "selectedIndexChanged", "x" } },
                typeof(MudChart<double>), errors);

            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains(errors[0], "renderer");
        }

        /// <summary>
        /// Ein Objekt-Wert auf einem Objekt-Parameter wird rekursiv befuellt - so muss die Bindung die
        /// Optionen von MudBlazor nicht kennen. ChartOptions ist dabei eine Schnittstelle am Parameter.
        /// </summary>
        [TestMethod]
        public void NestedObject_IsFilledRecursively()
        {
            var errors = new List<string>();
            Dictionary<string, object?> bound = ChartParameterBinder.Bind(
                new Dictionary<string, object?>
                {
                    { "chartOptions", Map(("showLegend", false), ("chartPalette", new object?[] { "#111111" })) }
                },
                typeof(MudChart<double>), errors);

            CollectionAssert.AreEqual(Array.Empty<string>(), errors);
            var options = (ChartOptions)bound["ChartOptions"]!;
            Assert.IsFalse(options.ShowLegend);
            CollectionAssert.AreEqual(new[] { "#111111" }, options.ChartPalette);
        }

        [TestMethod]
        public void NotConvertibleValue_IsReported()
        {
            var errors = new List<string>();
            ChartParameterBinder.Bind(
                new Dictionary<string, object?> { { "canHideSeries", "vielleicht" } },
                typeof(MudChart<double>), errors);

            Assert.AreEqual(1, errors.Count, string.Join(" | ", errors));
        }

        // ---- JSON-Seite ---------------------------------------------------------------------------

        [TestMethod]
        public void RenderedJson_BecomesOneEntry()
        {
            var errors = new List<string>();
            IReadOnlyList<object?>? entries = ChartJson.ToEntries(
                "{ \"type\": \"pie\", \"series\": [ { \"name\": \"x\", \"data\": [1, 2] } ] }", errors);

            Assert.IsNotNull(entries);
            Assert.AreEqual(1, entries!.Count);
            CollectionAssert.AreEqual(Array.Empty<string>(), errors);
            Assert.IsNotNull(ChartWidgetDeclaration.FromMap(
                (IDictionary<string, object?>)entries[0]!, errors));
        }

        [TestMethod]
        public void BrokenJson_IsReportedWithItsReason()
        {
            var errors = new List<string>();
            Assert.IsNull(ChartJson.ToEntries("{ \"type\": ", errors));
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains(errors[0], "not valid JSON");
        }

        /// <summary>
        /// Ein Array an der Wurzel sind MEHRERE Diagramme aus derselben Abfrage - der ganze Zweck dieser
        /// Form. Ein Objekt bleibt daneben gueltig, sonst waere jede bestehende Kachel kaputt.
        /// </summary>
        [TestMethod]
        public void JsonArray_BecomesOneEntryPerChart()
        {
            var errors = new List<string>();
            IReadOnlyList<object?>? entries = ChartJson.ToEntries(
                "[ { \"type\": \"pie\", \"series\": [ { \"name\": \"x\", \"data\": [1] } ] }, " +
                "  { \"type\": \"bar\", \"series\": [ { \"name\": \"y\", \"data\": [2] } ] } ]", errors);

            Assert.IsNotNull(entries);
            Assert.AreEqual(2, entries!.Count);
            CollectionAssert.AreEqual(Array.Empty<string>(), errors);

            IReadOnlyList<ChartWidgetPanel> panels = ChartWidgetPanel.Build(entries);
            Assert.AreEqual(2, panels.Count);
            Assert.AreEqual(ChartType.Pie, panels[0].Declaration!.Type);
            Assert.AreEqual(ChartType.Bar, panels[1].Declaration!.Type);
            Assert.IsTrue(panels.All(p => p.Errors.Count == 0));
        }

        /// <summary>Weder Zahl noch Text sind eine Deklaration - aber die Kachel steht trotzdem.</summary>
        [TestMethod]
        public void JsonScalarsInAnArray_BecomeErrorsAtTheirOwnPlace()
        {
            var errors = new List<string>();
            IReadOnlyList<object?>? entries = ChartJson.ToEntries("[1, 2]", errors);

            Assert.IsNotNull(entries);
            CollectionAssert.AreEqual(Array.Empty<string>(), errors);

            IReadOnlyList<ChartWidgetPanel> panels = ChartWidgetPanel.Build(entries!);
            Assert.AreEqual(2, panels.Count);
            Assert.IsTrue(panels.All(p => p.Declaration is null && p.Errors.Count == 1),
                string.Join(" | ", panels.SelectMany(p => p.Errors)));
        }

        /// <summary>Ein Skalar an der Wurzel ist dagegen gar nichts - das betrifft die ganze Kachel.</summary>
        [TestMethod]
        public void JsonScalarAtTheRoot_IsNoDeclarationAtAll()
        {
            var errors = new List<string>();
            Assert.IsNull(ChartJson.ToEntries("42", errors));
            Assert.AreEqual(1, errors.Count);
        }

        // ---- Mehrere Diagramme in einer Kachel ------------------------------------------------------

        /// <summary>
        /// Der Punkt, um den es bei mehreren Diagrammen geht: ein kaputter Eintrag nimmt die uebrigen
        /// NICHT mit. Sonst loescht ein Tippfehler im zweiten Diagramm das erste gleich mit aus, und was
        /// noch richtig ist, sieht man nicht mehr.
        /// </summary>
        [TestMethod]
        public void ABrokenEntry_DoesNotTakeTheOthersDown()
        {
            IReadOnlyList<ChartWidgetPanel> panels = ChartWidgetPanel.Build(new object?[]
            {
                Map(("type", "pie"), ("series", new object?[] { Map(("name", "x"), ("data", new object?[] { 1 })) })),
                Map(("type", "kuchen"), ("series", new object?[] { Map(("name", "y"), ("data", new object?[] { 2 })) })),
                Map(("type", "bar"), ("series", new object?[] { Map(("name", "z"), ("data", new object?[] { 3 })) }))
            });

            Assert.AreEqual(3, panels.Count);
            Assert.AreEqual(0, panels[0].Errors.Count);
            Assert.AreNotEqual(0, panels[1].Errors.Count);
            Assert.AreEqual(0, panels[2].Errors.Count);
        }

        /// <summary>
        /// title, minWidth und action gehoeren dem Mantel und duerfen NICHT als Parameter der
        /// Diagramm-Komponente durchgereicht werden - dort waeren sie unbekannt und die Kachel zeigte
        /// statt des Diagramms eine Beanstandung.
        /// </summary>
        [TestMethod]
        public void PanelFields_AreReadAndNotPassedToTheChart()
        {
            IReadOnlyList<ChartWidgetPanel> panels = ChartWidgetPanel.Build(new object?[]
            {
                Map(("type", "pie"),
                    ("title", "Nach Status"),
                    ("minWidth", 400),
                    ("action", "status"),
                    ("series", new object?[] { Map(("name", "x"), ("data", new object?[] { 1 })) }))
            });

            ChartWidgetPanel panel = panels.Single();
            CollectionAssert.AreEqual(Array.Empty<string>(), panel.Errors.ToArray());
            Assert.AreEqual("Nach Status", panel.Declaration!.Title);
            Assert.AreEqual(400, panel.Declaration.MinWidth);
            Assert.AreEqual("status", panel.Declaration.Action);
            Assert.AreEqual(0, panel.Declaration.Extra.Count);
            Assert.AreEqual(0, panel.Parameters.Count);
        }

        /// <summary>Ohne Angabe bleibt es bei den Vorgaben - eine bestehende Konfiguration aendert sich nicht.</summary>
        [TestMethod]
        public void PanelFields_HaveDefaults()
        {
            var errors = new List<string>();
            ChartWidgetDeclaration? declaration = ChartWidgetDeclaration.FromMap(Map(
                ("type", "pie"),
                ("series", new object?[] { Map(("name", "x"), ("data", new object?[] { 1 })) })), errors);

            Assert.IsNull(declaration!.Title);
            Assert.AreEqual(ChartWidgetDeclaration.DefaultMinWidth, declaration.MinWidth);
            Assert.AreEqual(ChartWidgetDeclaration.DefaultAction, declaration.Action);
        }

        /// <summary>
        /// minWidth ist eine Zahl von Pixeln, keine CSS-Laenge: mit "50%" koennte der Umbruch nicht
        /// rechnen. Wer die Groesse des Diagramms selbst meint, setzt width/height durch.
        /// </summary>
        [TestMethod]
        public void MinWidth_MustBeANumber()
        {
            var errors = new List<string>();
            ChartWidgetDeclaration? declaration = ChartWidgetDeclaration.FromMap(Map(
                ("type", "pie"),
                ("minWidth", "50%"),
                ("series", new object?[] { Map(("name", "x"), ("data", new object?[] { 1 })) })), errors);

            Assert.AreEqual(1, errors.Count, string.Join(" | ", errors));
            StringAssert.Contains(errors[0], "minWidth");
            Assert.AreEqual(ChartWidgetDeclaration.DefaultMinWidth, declaration!.MinWidth);
        }

        // ---- Eigene Groesse ----------------------------------------------------------------------

        /// <summary>
        /// Eine absolute Groesse am Diagramm muss auch den PLATZ bestimmen, den es einnimmt - sonst nimmt
        /// der Platz weiter seine Mindestbreite plus allen Restplatz, und alles, was darin ausgerichtet
        /// wird (die Ueberschrift), sitzt neben dem Diagramm statt darueber.
        /// </summary>
        [TestMethod]
        public void AbsoluteSize_IsTheDeclaredSize()
        {
            ChartWidgetPanel panel = OnePanel(("width", "12rem"), ("height", "150px"));

            CollectionAssert.AreEqual(Array.Empty<string>(), panel.Errors.ToArray());
            Assert.AreEqual("12rem", panel.DeclaredWidth);
        }

        /// <summary>Eine nackte Zahl ist als Pixel gemeint - so liest sie auch das svg-Attribut.</summary>
        [TestMethod]
        public void BareNumberSize_IsPixels()
        {
            ChartWidgetPanel panel = OnePanel(("width", 150));

            Assert.AreEqual("150px", panel.DeclaredWidth);
        }

        /// <summary>
        /// Eine relative Angabe rechnet gegen den Platz - der darf sich dann nicht umgekehrt nach ihr
        /// richten, das waere zirkulaer. Sie ist deshalb KEINE eigene Groesse.
        /// </summary>
        [TestMethod]
        public void RelativeSize_IsNoDeclaredSize()
        {
            Assert.IsNull(OnePanel(("width", "80%")).DeclaredWidth);
            Assert.IsNull(OnePanel(("width", "auto")).DeclaredWidth);
            Assert.IsNull(OnePanel(("width", "calc(100% - 20px)")).DeclaredWidth);
        }

        /// <summary>Ohne Angabe bleibt es beim bisherigen Verhalten: Mindestbreite und Restplatz.</summary>
        [TestMethod]
        public void NoSize_IsNoDeclaredSize()
        {
            Assert.IsNull(OnePanel().DeclaredWidth);
        }

        /// <summary>
        /// Eine deklarierte HOEHE bestimmt den Platz nicht: wie hoch er ist, ergibt sich aus seiner Zeile -
        /// nur so stehen die Diagramme einer Zeile auf einer Linie.
        /// </summary>
        [TestMethod]
        public void Height_IsPassedThroughButShapesNoPlace()
        {
            ChartWidgetPanel panel = OnePanel(("height", "150px"));

            CollectionAssert.AreEqual(Array.Empty<string>(), panel.Errors.ToArray());
            Assert.AreEqual("150px", panel.Parameters["Height"]);
            Assert.IsNull(panel.DeclaredWidth);
        }

        /// <summary>Ein zeichenbares Diagramm mit den uebergebenen Zusatzfeldern.</summary>
        private static ChartWidgetPanel OnePanel(params (string Key, object? Value)[] extra)
        {
            var fields = new List<(string, object?)>
            {
                ("type", "pie"),
                ("series", new object?[] { Map(("name", "x"), ("data", new object?[] { 1 })) })
            };
            fields.AddRange(extra.Select(e => (e.Key, e.Value)));

            return ChartWidgetPanel.Build(new object?[] { Map(fields.ToArray()) }).Single();
        }

        /// <summary>
        /// Das ObjectLiteral von CScript ist selbst aufzaehlbar. Wuerde die Liste zuerst geprueft, zerfiele
        /// EIN Diagramm in so viele "Diagramme", wie seine Deklaration Felder hat.
        /// </summary>
        [TestMethod]
        public void CScriptResult_IsOneEntryForAMapAndOnePerItemForAList()
        {
            var errors = new List<string>();
            IDictionary<string, object?> one = Map(("type", "pie"));

            IReadOnlyList<object?>? single = CScriptChartRenderer.AsEntries(one, errors);
            Assert.AreEqual(1, single!.Count);
            Assert.AreSame(one, single[0]);

            IReadOnlyList<object?>? many = CScriptChartRenderer.AsEntries(
                new List<object?> { one, Map(("type", "bar")) }, errors);
            Assert.AreEqual(2, many!.Count);

            CollectionAssert.AreEqual(Array.Empty<string>(), errors);
            Assert.IsNull(CScriptChartRenderer.AsEntries(42, errors));
            Assert.AreEqual(1, errors.Count);
        }

        // ---- Template-Funktionen und Spaltenzugriff ------------------------------------------------

        /// <summary>
        /// Der Spaltenzugriff muss beide Zeilenformen bedienen: Woerterbuecher (dynamischer Adapter) und
        /// Objekte mit Eigenschaften (LINQ-/CScript-Abfrage ueber einen DbContext).
        /// </summary>
        [TestMethod]
        public void Column_ReadsBothRowShapes()
        {
            var rows = new object?[]
            {
                new Dictionary<string, object?> { { "Status", "Offen" } },
                new { Status = "Erledigt" }
            };

            CollectionAssert.AreEqual(new object?[] { "Offen", "Erledigt" },
                WidgetRowAccessor.Column(rows, "Status").ToArray());
        }

        /// <summary>Fehlt die Spalte, steht dort null - keine kuerzere Liste, die alles verschoebe.</summary>
        [TestMethod]
        public void Column_KeepsItsLengthWhenAColumnIsMissing()
        {
            var rows = new object?[] { new { A = 1 }, new { B = 2 } };

            Assert.AreEqual(2, WidgetRowAccessor.Column(rows, "A").Count);
        }

        [TestMethod]
        public void Json_EscapesAndStaysInvariant()
        {
            CultureInfo before = Thread.CurrentThread.CurrentCulture;
            try
            {
                // Eine Kultur mit Dezimalkomma: die Deklaration muss trotzdem 1.5 schreiben, sonst ist
                // sie kein gueltiges JSON mehr.
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-CH");

                Assert.AreEqual("1.5", WidgetTemplateFunctions.Json(1.5d));
                Assert.AreEqual("\"a\\\"b\"", WidgetTemplateFunctions.Json("a\"b"));
                // Umlaute bleiben lesbar statt als Escape-Sequenz zu erscheinen - wer eine Kachel
                // repariert, liest diesen Text.
                Assert.AreEqual("\"Grösse\"", WidgetTemplateFunctions.Json("Grösse"));
                Assert.AreEqual("[1,2]", WidgetTemplateFunctions.Json(new object?[] { 1, 2 }));
                Assert.AreEqual("null", WidgetTemplateFunctions.Json(null));
                Assert.AreEqual("true", WidgetTemplateFunctions.Json(true));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = before;
            }
        }

        // ---- Die Pruefung beim Speichern -----------------------------------------------------------

        [TestMethod]
        public void ScribanValidation_AcceptsAGoodTemplateAndRejectsABrokenOne()
        {
            Assert.IsNull(ScribanChartRenderer.Validate(
                "{ \"type\": \"pie\", \"series\": [ { \"name\": \"x\", \"data\": [] } ] }",
                new Dictionary<string, string?>()));

            // Ein Scriban-Fehler: die Schleife wird nie geschlossen. Unvollstaendiges JSON ist dagegen
            // KEINE Beanstandung mehr - dazu muesste man rendern, und was ein Template mit echten Zeilen
            // erzeugt, weiss man beim Speichern nicht.
            Assert.IsNotNull(ScribanChartRenderer.Validate(
                "{{ for row in Rows }} x", new Dictionary<string, string?>()));
        }

        /// <summary>
        /// Der Fall aus dem Betrieb: eine voellig richtige Konfiguration, die auf Daten zugreift. Frueher
        /// wertete die Pruefung sie mit LEEREN Zeilen aus - Rows[0] schlug mit "Index was outside the
        /// bounds of the array" fehl, und der Editor liess sich nicht speichern. Geprueft wird jetzt nur
        /// noch, ob sich der Text uebersetzen laesst.
        /// </summary>
        [TestMethod]
        public void CScriptValidation_AcceptsAnExpressionThatReadsData()
        {
            Assert.IsNull(CScriptChartRenderer.Validate(
                "{ type: ChartType.Donut, " +
                "labels: Rows[0].Months, " +
                "series: [ { name: \"Total\", data: Rows[0][\"All-Over\"] } ] }",
                new Dictionary<string, string?>()));
        }

        /// <summary>Dasselbe fuer die Scriban-Seite: ein Template, das erst mit Zeilen JSON ergibt.</summary>
        [TestMethod]
        public void ScribanValidation_AcceptsATemplateThatOnlyYieldsJsonWithData()
        {
            Assert.IsNull(ScribanChartRenderer.Validate(
                "{ \"type\": \"pie\", \"labels\": [ {{ for row in Rows }}{{ json row.Topic }},{{ end }} ] }",
                new Dictionary<string, string?>()));
        }

        /// <summary>
        /// Zwei CScript-Eigenheiten stecken in diesem einen Fall: Text braucht DOPPELTE Anfuehrungszeichen
        /// (einfache bezeichnen einen Typ), und ein Ausdruck darf nicht mit '{' beginnen - deshalb klammert
        /// der Renderer das Objektliteral selbst ein. Ohne diese Klammer wuerde hier jede Deklaration
        /// abgelehnt.
        /// </summary>
        [TestMethod]
        public void CScriptValidation_AcceptsAnObjectLiteral()
        {
            Assert.IsNull(CScriptChartRenderer.Validate(
                "{type: \"pie\", series: [{name: \"x\", data: [1]}]}",
                new Dictionary<string, string?>()));
        }

        /// <summary>
        /// Mehrere Diagramme sind eine LISTE von Objektliteralen. Der Renderer klammert sie genauso ein
        /// wie ein einzelnes - ohne diese Klammer entscheidet die Grammatik darueber, und darauf soll sich
        /// niemand verlassen muessen.
        /// </summary>
        [TestMethod]
        public void CScriptValidation_AcceptsAListOfDeclarations()
        {
            Assert.IsNull(CScriptChartRenderer.Validate(
                "[{title: \"Nach Status\", type: \"pie\", series: [{name: \"x\", data: [1]}]}, " +
                "{title: \"Verlauf\", type: ChartType.Line, minWidth: 400, series: [{name: \"y\", data: [2]}]}]",
                new Dictionary<string, string?>()));
        }

        /// <summary>Dasselbe auf der JSON-Seite: ein Array an der Wurzel ist eine gueltige Konfiguration.</summary>
        [TestMethod]
        public void ScribanValidation_AcceptsAnArrayOfDeclarations()
        {
            Assert.IsNull(ScribanChartRenderer.Validate(
                "[ { \"type\": \"pie\", \"series\": [ { \"name\": \"x\", \"data\": [1] } ] }, " +
                "  { \"type\": \"bar\", \"series\": [ { \"name\": \"y\", \"data\": [2] } ] } ]",
                new Dictionary<string, string?>()));
        }

        /// <summary>Enum-Bezuege wie ChartType.Pie gehen, weil der Typ im Geltungsbereich liegt.</summary>
        [TestMethod]
        public void CScriptValidation_AcceptsAnEnumReference()
        {
            Assert.IsNull(CScriptChartRenderer.Validate(
                "{type: ChartType.Donut, series: [{name: \"x\", data: [1]}]}",
                new Dictionary<string, string?>()));
        }

        /// <summary>
        /// CScript kennt KEINE Lambda-Ausdruecke - Rows.Select(r => r.Status) ist ein Syntaxfehler, und ein
        /// Funktions-Literal nehmen die LINQ-Methoden nicht an. Deshalb liegt column(...) im
        /// Geltungsbereich; ohne diesen Helfer bliebe fuer die haeufigste Aufgabe nur die native
        /// Einbettung. Der Aufruf muss auch MITTEN im Objektliteral gehen.
        /// </summary>
        [TestMethod]
        public void CScriptValidation_AcceptsTheColumnHelper()
        {
            Assert.IsNull(CScriptChartRenderer.Validate(
                "{type: \"pie\", labels: column(Rows, \"Status\"), " +
                "series: [{name: \"Anzahl\", data: column(Rows, \"Anzahl\")}]}",
                new Dictionary<string, string?>()));
        }

        /// <summary>
        /// Der Ausweg fuer alles, was column nicht abdeckt: native Einbettung als WERT in der Deklaration.
        /// Merke: das "with {}" ist Pflicht, auch leer - ohne es ist der Ausdruck ein Syntaxfehler.
        /// </summary>
        [TestMethod]
        public void CScriptValidation_AcceptsNativeEmbedding()
        {
            Assert.IsNull(CScriptChartRenderer.Validate(
                "{type: \"pie\", " +
                "labels: `E(#DEFAULT)::@#return new string[]{\"a\"};# with {}, " +
                "series: [{name: \"x\", data: [1]}]}",
                new Dictionary<string, string?>()));
        }

        /// <summary>
        /// Pruefung und Ausfuehrung MUESSEN denselben Typ reichen. Vorher lag beim Ausfuehren eine
        /// List&lt;object&gt; im Geltungsbereich (so baut sie der Runner) und beim Pruefen ein object[] -
        /// ein nativer Cast konnte also entweder gespeichert werden ODER laufen, nie beides.
        /// </summary>
        [TestMethod]
        public void CScriptVariables_OfferRowsAsTheSameTypeWhenCheckingAndWhenRunning()
        {
            object? whenChecking = CScriptChartRenderer.BuildVariables(null)["Rows"];

            var model = new WidgetTemplateModel { Rows = new List<object?> { new { A = 1 } } };
            object? whenRunning = CScriptChartRenderer.BuildVariables(model)["Rows"];

            Assert.IsInstanceOfType<object[]>(whenChecking);
            Assert.IsInstanceOfType<object[]>(whenRunning);
            Assert.AreEqual(whenChecking.GetType(), whenRunning.GetType());
        }

        /// <summary>Der dokumentierte Cast im nativen Code muss die Pruefung ueberstehen.</summary>
        [TestMethod]
        public void CScriptValidation_AcceptsTheDocumentedNativeCast()
        {
            Assert.IsNull(CScriptChartRenderer.Validate(
                "{type: \"pie\", " +
                // Ziel-Form: der Code steht als STRING-Literal (@"..."), nicht als @#...#-Block - den
                // gibt es nur bei der Form ohne Zielobjekt.
                "labels: `E(Rows as Rows->DEFAULT)::@\"object[] rw = (object[])Global.Rows; " +
                "return (from t in rw select t.ToString()).ToArray();\" with {}, " +
                "series: [{name: \"x\", data: [1]}]}",
                new Dictionary<string, string?>()));
        }

        /// <summary>
        /// Der Knopf "Insert parameters" haengt seinen Block als Kommentar UNTER die Konfiguration. Beide
        /// Sprachen muessen das aushalten - sonst macht die Stuetze die Konfiguration kaputt.
        /// </summary>
        [TestMethod]
        public void BothRenderers_AcceptATrailingCommentBlock()
        {
            // Je Sprache ihre eigene Schreibweise: JSON verlangt gequotete Schluessel, CScript nicht.
            const string asJson = "{\"type\": \"pie\", \"series\": [{\"name\": \"x\", \"data\": [1]}]}";
            const string declaration = "{type: \"pie\", series: [{name: \"x\", data: [1]}]}";

            string json = asJson + Environment.NewLine + Environment.NewLine
                          + ScribanChartRenderer.DescribeParameters();
            string? jsonMessage = ScribanChartRenderer.Validate(json, new Dictionary<string, string?>());
            Assert.IsNull(jsonMessage,
                $"Die JSON-Seite muss den angehaengten Kommentarblock lesen koennen: {jsonMessage}");

            string cscript = declaration + Environment.NewLine + Environment.NewLine
                             + CScriptChartRenderer.DescribeParameters();
            string? cscriptMessage = CScriptChartRenderer.Validate(cscript, new Dictionary<string, string?>());
            Assert.IsNull(cscriptMessage,
                $"Die CScript-Seite muss den angehaengten Kommentarblock lesen koennen: {cscriptMessage}");
        }

        /// <summary>Die Uebersicht muss die Parameter nennen, die es wirklich gibt - und die reservierten nicht.</summary>
        [TestMethod]
        public void Describe_ListsPassThroughParametersAndOmitsTheReservedOnes()
        {
            string text = ChartParameterBinder.Describe(typeof(MudChart<double>), asJson: true);

            StringAssert.Contains(text, "legendPosition");
            StringAssert.Contains(text, "chartOptions");
            // Die Aufzaehlungswerte sind der eigentliche Nutzen: sie stehen sonst nur in der Fremdquelle.
            StringAssert.Contains(text, "Bottom");
            Assert.IsFalse(text.Contains("chartSeries", StringComparison.OrdinalIgnoreCase),
                "Reservierte Parameter gehoeren nicht in die Uebersicht - sie waeren nur eine Einladung zum Fehler.");
        }

        /// <summary>Ein Lambda ist und bleibt ein Syntaxfehler - die Meldung soll ihn nennen.</summary>
        [TestMethod]
        public void CScriptValidation_RejectsALambda()
        {
            Assert.IsNotNull(CScriptChartRenderer.Validate(
                "{type: \"pie\", labels: Rows.Select(r => r.Status)}",
                new Dictionary<string, string?>()));
        }

        /// <summary>Im Block-Modus ist dasselbe mit einem return zu schreiben.</summary>
        [TestMethod]
        public void CScriptValidation_AcceptsABlockWithReturn()
        {
            Assert.IsNull(CScriptChartRenderer.Validate(
                "o = {type: \"pie\", series: [{name: \"x\", data: [1]}]}; return o;",
                new Dictionary<string, string?> { { CScriptChartRenderer.ScriptModeOption, CScriptChartRenderer.BlockMode } }));
        }

        /// <summary>
        /// Ein Block ohne <c>return</c> liefert null - und genau das ist der Fall, den ein geratener
        /// Modus verschweigen wuerde. Die Meldung nennt ihn beim Namen.
        /// </summary>
        [TestMethod]
        public void CScriptValidation_SaysWhenAReturnIsMissing()
        {
            string? message = CScriptChartRenderer.Validate(
                "x = 1;",
                new Dictionary<string, string?> { { CScriptChartRenderer.ScriptModeOption, CScriptChartRenderer.BlockMode } });

            Assert.IsNotNull(message);
            StringAssert.Contains(message!, "return");
        }

        // --- Texte im Diagramm ------------------------------------------------------------------------

        /// <summary>
        /// Der Regelfall: zwei Zeilen in der Mitte eines Rings - die grosse Zahl und darunter, wogegen
        /// sie zaehlt.
        /// </summary>
        [TestMethod]
        public void Overlay_IsReadWithItsPositions()
        {
            var errors = new List<string>();
            ChartWidgetDeclaration? declaration = ChartWidgetDeclaration.FromMap(Map(
                ("type", "donut"),
                ("series", new object?[] { Map(("name", "n"), ("data", new object?[] { 1 })) }),
                ("overlay", new object?[]
                {
                    Map(("text", "12"), ("class", "mud-typography-h3"), ("posY", "45%")),
                    Map(("text", "von 20"), ("class", "mud-typography-h6"), ("posY", "60%"))
                })), errors);

            Assert.IsNotNull(declaration);
            CollectionAssert.AreEqual(Array.Empty<string>(), errors);
            Assert.AreEqual(2, declaration!.Overlay.Count);
            Assert.AreEqual("12", declaration.Overlay[0].Text);
            Assert.AreEqual("mud-typography-h3", declaration.Overlay[0].Class);
            Assert.AreEqual("45%", declaration.Overlay[0].PosY);
            Assert.AreEqual("50%", declaration.Overlay[0].PosX, "ohne Angabe steht der Text mittig.");
            Assert.AreEqual("middle", declaration.Overlay[0].Anchor,
                "in SVG ist x der ANFANG des Textes - ohne diese Vorgabe staende er rechts der Mitte.");
        }

        /// <summary>
        /// Eine falsche Ausrichtung wird gemeldet, nicht still auf die Vorgabe gedreht: sie verschiebt den
        /// Text SICHTBAR, und dann sucht man den Fehler im Diagramm statt im Text.
        /// </summary>
        [TestMethod]
        public void Overlay_UnknownAnchor_IsReported()
        {
            var errors = new List<string>();
            ChartWidgetDeclaration? declaration = ChartWidgetDeclaration.FromMap(Map(
                ("type", "donut"),
                ("series", new object?[] { Map(("name", "n"), ("data", new object?[] { 1 })) }),
                ("overlay", new object?[] { Map(("text", "x"), ("anchor", "center")) })), errors);

            Assert.IsNotNull(declaration);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains(errors[0], "center");
            StringAssert.Contains(errors[0], "middle");
            Assert.AreEqual("middle", declaration!.Overlay[0].Anchor);
        }

        /// <summary>
        /// Ein Eintrag ohne Aufbau bekommt eine Meldung. Anders als bei 'labels' gibt es hier bewusst
        /// keine Kurzform: ein blosser Text haette keine Position, und die zu raten hiesse, ihn irgendwo
        /// hinzuschreiben.
        /// </summary>
        [TestMethod]
        public void Overlay_PlainString_IsReported()
        {
            var errors = new List<string>();
            ChartWidgetDeclaration.FromMap(Map(
                ("type", "donut"),
                ("series", new object?[] { Map(("name", "n"), ("data", new object?[] { 1 })) }),
                ("overlay", new object?[] { "nur ein Text" })), errors);

            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains(errors[0], "overlay");
        }

        /// <summary>
        /// Eine Zahl als Koordinate wird IMMER mit Punkt geschrieben. Unter deutschem Gebietsschema waere
        /// ein Komma in einem SVG-Attribut ein zweiter Wert und nicht ein Dezimaltrennzeichen - die Form
        /// zerfiele.
        /// </summary>
        [TestMethod]
        public void Overlay_NumericCoordinate_UsesTheInvariantForm()
        {
            CultureInfo before = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-CH");
                var errors = new List<string>();
                ChartWidgetDeclaration? declaration = ChartWidgetDeclaration.FromMap(Map(
                    ("type", "donut"),
                    ("series", new object?[] { Map(("name", "n"), ("data", new object?[] { 1 })) }),
                    ("overlay", new object?[] { Map(("text", "x"), ("posX", 12.5)) })), errors);

                Assert.AreEqual("12.5", declaration!.Overlay[0].PosX);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = before;
            }
        }

        /// <summary>
        /// Der Grund, warum die Deklaration aus der Datenbank kommen darf: Text UND Attributwerte werden
        /// kodiert. Ein Eintrag, der ein Element schliessen will, schliesst keines.
        /// </summary>
        /// <remarks>
        /// Geprueft ueber den oeffentlichen Weg (<c>ChartWidgetPanel.Build</c>) und nicht am Erzeuger
        /// direkt: so haengt der Test an der Kette, die im Betrieb laeuft, und nicht an einem Baustein
        /// daraus.
        /// </remarks>
        [TestMethod]
        public void OverlayMarkup_EncodesEverythingItWrites()
        {
            IReadOnlyList<ChartWidgetPanel> panels = ChartWidgetPanel.Build(new object?[]
            {
                Map(("type", "donut"),
                    ("series", new object?[] { Map(("name", "n"), ("data", new object?[] { 1 })) }),
                    ("overlay", new object?[]
                    {
                        Map(("text", "</text><script>alert(1)</script>"), ("class", "a\" onload=\"x"))
                    }))
            });

            string markup = panels[0].OverlayMarkup;
            StringAssert.Contains(markup, "&lt;script&gt;");
            Assert.IsFalse(markup.Contains("<script>"), "kein Element aus dem Text.");
            Assert.IsFalse(markup.Contains("onload=\"x\""), "kein Attribut aus der Klasse.");
            Assert.AreEqual(1, markup.Split(new[] { "</text>" }, StringSplitOptions.None).Length - 1,
                "genau ein schliessendes text-Element - der Text hat keines beigesteuert.");
        }

        /// <summary>Ohne Texte entsteht kein Markup - dann rendert die Ansicht schlicht nichts.</summary>
        [TestMethod]
        public void OverlayMarkup_WithoutTexts_IsEmpty()
        {
            IReadOnlyList<ChartWidgetPanel> panels = ChartWidgetPanel.Build(new object?[]
            {
                Map(("type", "donut"),
                    ("series", new object?[] { Map(("name", "n"), ("data", new object?[] { 1 })) }))
            });

            Assert.AreEqual(string.Empty, panels[0].OverlayMarkup);
        }
    }
}
