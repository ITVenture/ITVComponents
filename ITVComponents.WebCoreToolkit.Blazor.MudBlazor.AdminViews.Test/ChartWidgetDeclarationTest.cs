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
        public void RenderedJson_BecomesAMap()
        {
            var errors = new List<string>();
            IDictionary<string, object?>? map = ChartJson.ToMap(
                "{ \"type\": \"pie\", \"series\": [ { \"name\": \"x\", \"data\": [1, 2] } ] }", errors);

            Assert.IsNotNull(map);
            CollectionAssert.AreEqual(Array.Empty<string>(), errors);
            Assert.IsNotNull(ChartWidgetDeclaration.FromMap(map, errors));
        }

        [TestMethod]
        public void BrokenJson_IsReportedWithItsReason()
        {
            var errors = new List<string>();
            Assert.IsNull(ChartJson.ToMap("{ \"type\": ", errors));
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains(errors[0], "not valid JSON");
        }

        [TestMethod]
        public void JsonArray_IsNotADeclaration()
        {
            var errors = new List<string>();
            Assert.IsNull(ChartJson.ToMap("[1, 2]", errors));
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
        public void ScribanValidation_AcceptsAGoodTemplateAndRejectsBrokenJson()
        {
            Assert.IsNull(ScribanChartRenderer.Validate(
                "{ \"type\": \"pie\", \"series\": [ { \"name\": \"x\", \"data\": [] } ] }",
                new Dictionary<string, string?>()));

            Assert.IsNotNull(ScribanChartRenderer.Validate(
                "{ \"type\": \"pie\", ", new Dictionary<string, string?>()));
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

        /// <summary>Enum-Bezuege wie ChartType.Pie gehen, weil der Typ im Geltungsbereich liegt.</summary>
        [TestMethod]
        public void CScriptValidation_AcceptsAnEnumReference()
        {
            Assert.IsNull(CScriptChartRenderer.Validate(
                "{type: ChartType.Donut, series: [{name: \"x\", data: [1]}]}",
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
    }
}
