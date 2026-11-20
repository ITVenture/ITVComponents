using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Widgets;
using ITVComponents.WebCoreToolkit.EntityFramework.DiagnosticsQueries;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.ServiceShared.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.Test
{
    /// <summary>
    /// Ein Dictionary als Abfrage-Ergebnis ist EIN Wert und muss als Objekt ankommen, nicht als Liste von
    /// Schluessel/Wert-Paaren (Issue Diagnostics 01). Zeilenfolgen laufen weiter gestreamt.
    /// </summary>
    [TestClass]
    public class DiagnosticsQueryResultShapeTest
    {
        [TestMethod]
        public void ADictionary_IsSerializedAsAnObject()
        {
            var owner = new CountingOwner();
            var raw = new Dictionary<string, Dictionary<string, object>[]>
            {
                { "Model", new[] { new Dictionary<string, object> { { "Id", 1 } } } },
                { "Columns", new[] { new Dictionary<string, object> { { "Field", "Id" } } } }
            };

            IEnumerable result = DiagnosticsQueryService.TakeOwnership(owner, raw);
            string json = SerializeLikeTheEndpoint(result);

            using JsonDocument doc = JsonDocument.Parse(json);
            Assert.AreEqual(JsonValueKind.Object, doc.RootElement.ValueKind, json);
            Assert.AreEqual(1, doc.RootElement.GetProperty("Model")[0].GetProperty("Id").GetInt32());
            Assert.AreEqual("Id", doc.RootElement.GetProperty("Columns")[0].GetProperty("Field").GetString());
        }

        [TestMethod]
        public void ADictionary_ReleasesTheSourceExactlyOnceAndAtOnce()
        {
            var owner = new CountingOwner();

            IEnumerable result = DiagnosticsQueryService.TakeOwnership(owner, new Hashtable { { 1, "a" } });

            Assert.AreEqual(1, owner.Disposed, "Freigegeben, bevor jemand liest.");
            foreach (object _ in result)
            {
            }

            Assert.AreEqual(1, owner.Disposed, "Lesen gibt nicht ein zweites Mal frei.");
            Assert.AreEqual("a", ((IDictionary<string, object>)result)["1"], "Schluessel werden zu Strings.");
        }

        [TestMethod]
        public void AString_StaysAString()
        {
            var owner = new CountingOwner();

            IEnumerable result = DiagnosticsQueryService.TakeOwnership(owner, "hello");

            Assert.AreEqual("\"hello\"", SerializeLikeTheEndpoint(result));
            Assert.AreEqual(1, owner.Disposed);
        }

        [TestMethod]
        public void RowSequences_AreStreamedAndReleasedAtTheEnd()
        {
            var owner = new CountingOwner();
            var produced = 0;
            IEnumerable Rows()
            {
                for (var i = 0; i < 3; i++)
                {
                    produced++;
                    yield return i;
                }
            }

            IEnumerable result = DiagnosticsQueryService.TakeOwnership(owner, Rows());
            Assert.AreEqual(0, produced, "Nichts gelesen, bevor aufgezaehlt wird.");
            Assert.AreEqual(0, owner.Disposed, "Die Quelle bleibt offen, bis die Folge gelesen ist.");

            CollectionAssert.AreEqual(new object[] { 0, 1, 2 }, result.Cast<object>().ToArray());
            Assert.AreEqual(1, owner.Disposed);
        }

        [TestMethod]
        public void TheDashboard_SeesADictionaryAsOneRow()
        {
            var config = new Dictionary<string, object> { { "Model", new[] { "x" } } };
            var widget = new DashboardWidgetDefinition
            {
                SystemName = "orderHistory",
                DisplayName = "orderHistory",
                DiagnosticsQuery = new DiagnosticsQueryDefinition { DiagnosticsQueryName = "q" }
            };

            WidgetTemplateModel? model = DashboardWidgetRunner.Load(widget, null, new FixedResult(config),
                new DiagnosticsQueryContext(null!, null!), null);

            Assert.IsNotNull(model);
            Assert.AreEqual(1, model.Count);
            Assert.AreSame(config, model.Row);
        }

        /// <summary>
        /// Wie <c>Results.Json(object? data, …)</c> im DiagnosticsHandler: deklariert als <c>object</c>, also nach
        /// dem LAUFZEIT-Typ. Mit dem deklarierten Typ <c>IEnumerable</c> schriebe System.Text.Json jedes Ergebnis
        /// als Folge - auch ein korrekt durchgereichtes Dictionary.
        /// </summary>
        private static string SerializeLikeTheEndpoint(IEnumerable result)
            => JsonSerializer.Serialize<object>(result, new JsonSerializerOptions());

        private sealed class CountingOwner : IDisposable
        {
            public int Disposed { get; private set; }

            public void Dispose() => Disposed++;
        }

        private sealed class FixedResult : IDiagnosticsQueryService
        {
            private readonly IEnumerable result;

            public FixedResult(IEnumerable result)
            {
                this.result = result;
            }

            public IEnumerable Execute(string queryName, string area, IDiagnosticsQueryContext context,
                IDictionary<string, string> arguments) => result;
        }
    }
}
