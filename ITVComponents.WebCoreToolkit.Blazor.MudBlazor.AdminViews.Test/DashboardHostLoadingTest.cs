using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Widgets;
using ITVComponents.WebCoreToolkit.EntityFramework.DiagnosticsQueries;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.ServiceShared.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.Test
{
    /// <summary>
    /// Das Dashboard laedt Kachel fuer Kachel: das Raster steht sofort, jede Kachel erscheint, sobald ihre
    /// Abfrage fertig ist.
    /// </summary>
    [TestClass]
    public class DashboardHostLoadingTest : BunitContext
    {
        private const string PlaceholderMarker = "aria-busy=\"true\"";
        private static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(10);

        private readonly FakeQueryService queries = new();

        private IRenderedComponent<DashboardHost> RenderHost(params string[] widgets)
        {
            JSInterop.Mode = JSRuntimeMode.Loose;
            Services.AddMudServices();
            this.AddAuthorization();
            Services.AddSingleton<IDiagnosticsStore>(new FakeStore(widgets));
            Services.AddSingleton<IDiagnosticsQueryService>(queries);
            Services.AddSingleton(queries.Log);
            Services.Configure<WidgetRendererConfiguration>(
                c => c.RegisterRenderer<RecordingRenderer>(key: "rec"));
            return Render<DashboardHost>();
        }

        [TestMethod]
        public void EachTile_IsDrawnBeforeTheNextQueryRuns()
        {
            IRenderedComponent<DashboardHost> cut = RenderHost("A", "B", "C");

            cut.WaitForAssertion(() => CollectionAssert.AreEqual(
                new[] { "query:A", "drawn:A", "query:B", "drawn:B", "query:C", "drawn:C" },
                queries.Log.Snapshot()));
            // Eigene Wartestelle: das Protokoll entsteht waehrend des Zeichnens, das Markup erst danach.
            cut.WaitForAssertion(() =>
                Assert.IsFalse(cut.Markup.Contains(PlaceholderMarker), "Alle Kacheln sind geladen."));
        }

        [TestMethod]
        public void WhileAQueryRuns_EarlierTilesAreThereAndLaterOnesArePlaceholders()
        {
            var reached = new ManualResetEventSlim();
            var release = new ManualResetEventSlim();
            queries.OnQuery = name =>
            {
                if (name == "B")
                {
                    reached.Set();
                    release.Wait(GateTimeout);
                }
            };

            IRenderedComponent<DashboardHost> cut = RenderHost("A", "B", "C");
            Assert.IsTrue(reached.Wait(GateTimeout), "Die Abfrage von B lief nie an.");
            string markup = cut.Markup;
            release.Set();

            StringAssert.Contains(markup, "drawn-A");
            Assert.AreEqual(2, markup.Split(PlaceholderMarker).Length - 1,
                "B und C warten noch und zeigen je einen Platzhalter.");
        }

        [TestMethod]
        public void AFailingQuery_DoesNotStopTheOtherTiles()
        {
            queries.OnQuery = name =>
            {
                if (name == "B")
                {
                    throw new InvalidOperationException("Absicht");
                }
            };

            IRenderedComponent<DashboardHost> cut = RenderHost("A", "B", "C");

            cut.WaitForAssertion(() =>
            {
                StringAssert.Contains(cut.Markup, "drawn-C");
                StringAssert.Contains(cut.Markup, cut.Instance.QueryFailedText);
                Assert.IsFalse(cut.Markup.Contains("drawn-B"));
            });
        }

        [TestMethod]
        public async Task LeavingTheDashboard_StopsTheRemainingQueries()
        {
            var reached = new ManualResetEventSlim();
            var release = new ManualResetEventSlim();
            queries.OnQuery = name =>
            {
                if (name == "A")
                {
                    reached.Set();
                    release.Wait(GateTimeout);
                }
            };

            IRenderedComponent<DashboardHost> cut = RenderHost("A", "B", "C");
            Assert.IsTrue(reached.Wait(GateTimeout), "Die Abfrage von A lief nie an.");
            cut.Instance.Dispose();
            release.Set();

            cut.WaitForAssertion(() => CollectionAssert.Contains(queries.Log.Snapshot(), "drawn:A"));
            await Task.Delay(200);
            CollectionAssert.DoesNotContain(queries.Log.Snapshot(), "query:B");
        }

        /// <summary>
        /// Umsortieren aendert nichts an den Daten. Sie neu zu holen kostete die volle Wartezeit und liesse
        /// jede Kachel als Platzhalter aufblitzen.
        /// </summary>
        [TestMethod]
        public void SavingTheArrangement_DoesNotRunTheQueriesAgain()
        {
            IRenderedComponent<DashboardHost> cut = RenderHost("A", "B");
            cut.WaitForAssertion(() => CollectionAssert.Contains(queries.Log.Snapshot(), "drawn:B"));

            cut.Find("button.mud-icon-button").Click();
            cut.WaitForAssertion(() => StringAssert.Contains(cut.Markup, cut.Instance.DoneText));
            cut.FindAll("button").First(b => b.TextContent.Contains(cut.Instance.DoneText)).Click();

            cut.WaitForAssertion(() => Assert.IsFalse(cut.Markup.Contains(cut.Instance.DoneText)));
            Assert.AreEqual(2, queries.Log.Snapshot().Count(e => e.StartsWith("query:", StringComparison.Ordinal)));
            StringAssert.Contains(cut.Markup, "drawn-A");
        }

        /// <summary>Was in welcher Reihenfolge geschah. Abfragen und Zeichnen laufen auf verschiedenen Wegen hierher.</summary>
        public sealed class EventLog
        {
            private readonly List<string> entries = new();

            public void Add(string entry)
            {
                lock (entries)
                {
                    entries.Add(entry);
                }
            }

            public string[] Snapshot()
            {
                lock (entries)
                {
                    return entries.ToArray();
                }
            }
        }

        /// <summary>Haelt fest, wann eine Kachel ihre Daten gezeichnet hat.</summary>
        public sealed class RecordingRenderer : ComponentBase, IWidgetRenderer
        {
            private WidgetTemplateModel? lastData;

            [Inject] public EventLog Log { get; set; } = default!;

            [Parameter] public string TemplateSource { get; set; } = string.Empty;

            [Parameter] public WidgetTemplateModel? Data { get; set; }

            [Parameter] public IReadOnlyDictionary<string, string?> Options { get; set; }
                = new Dictionary<string, string?>();

            [Parameter] public EventCallback<WidgetAction> OnAction { get; set; }

            [Parameter] public EventCallback<Exception> OnRenderError { get; set; }

            protected override void OnParametersSet()
            {
                if (!ReferenceEquals(lastData, Data))
                {
                    lastData = Data;
                    Log.Add($"drawn:{TemplateSource}");
                }
            }

            protected override void BuildRenderTree(RenderTreeBuilder builder)
                => builder.AddContent(0, $"drawn-{TemplateSource}");
        }

        private sealed class FakeQueryService : IDiagnosticsQueryService
        {
            public EventLog Log { get; } = new();

            public Action<string>? OnQuery { get; set; }

            public IEnumerable Execute(string queryName, string area, IDiagnosticsQueryContext context,
                IDictionary<string, string> arguments)
            {
                Log.Add($"query:{queryName}");
                OnQuery?.Invoke(queryName);
                return new object[] { queryName };
            }
        }

        /// <summary>Liefert bei jedem Lesen neue Definitionen, so wie der echte Speicher auch.</summary>
        private sealed class FakeStore : IDiagnosticsStore
        {
            private readonly string[] widgets;

            public FakeStore(string[] widgets)
            {
                this.widgets = widgets;
            }

            public DashboardWidgetDefinition[] GetUserWidgets(string userName, string targetCulture)
                => widgets.Select((name, index) => new DashboardWidgetDefinition
                {
                    DashboardWidgetId = index + 1,
                    UserWidgetId = index + 1,
                    SystemName = name,
                    DisplayName = name,
                    Template = name,
                    RendererKey = "rec",
                    DiagnosticsQuery = new DiagnosticsQueryDefinition { DiagnosticsQueryName = name }
                }).ToArray();

            public DashboardWidgetDefinition[] GetWidgetTemplates(string targetCulture)
                => Array.Empty<DashboardWidgetDefinition>();

            public Task<DashboardWidgetDefinition[]> SetUserWidgets(DashboardWidgetDefinition[] userWidgets, string userName)
                => Task.FromResult(userWidgets);

            public DiagnosticsQueryDefinition GetQuery(string queryName)
                => throw new NotSupportedException();

            public DashboardWidgetDefinition GetDashboard(string dashboardName, string targetCulture, int? userDashboardId = null)
                => throw new NotSupportedException();
        }
    }
}
