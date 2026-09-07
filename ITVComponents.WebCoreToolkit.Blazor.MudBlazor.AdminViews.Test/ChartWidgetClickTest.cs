using System;
using System.Collections.Generic;
using System.Linq;
using Bunit;
using Bunit.TestDoubles;
using ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Widgets.Charts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.Test
{
    /// <summary>
    /// Der Klick auf eine Kategorie - der Teil, den eine gepruefte Deklaration NICHT abdeckt.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Warum das eine eigene, gerenderte Pruefung braucht: der Fehler, der zu diesen Tests gefuehrt hat,
    /// sass nicht in der Deklaration - die trug ihr <c>navigateTo</c> die ganze Zeit korrekt. Verloren
    /// ging der Klick erst in MudBlazor, weil <c>SelectedIndexChanged</c> ausschliesslich bei einem
    /// GEAENDERTEN Index feuert und <c>SelectedIndex</c> ohne eigenes Zutun bei 0 beginnt. Nur ein Test,
    /// der wirklich rendert und wirklich klickt, sieht so etwas.
    /// </para>
    /// <para>
    /// Geklickt wird das Ringsegment - <c>path.mud-chart-serie</c>, das Element, an dem
    /// <c>BaseRadialChart</c> seinen <c>OnPathClick</c> haengt.
    /// </para>
    /// </remarks>
    [TestClass]
    public class ChartWidgetClickTest : BunitContext
    {
        private const string SegmentSelector = "path.mud-chart-serie";

        private static IDictionary<string, object?> Map(params (string Key, object? Value)[] entries)
            => entries.ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase);

        /// <summary>Ein Ring mit drei Kategorien, von denen jede woanders hinfuehrt.</summary>
        private static IReadOnlyList<ChartWidgetPanel> ThreeTargets()
            => ChartWidgetPanel.Build(new object?[]
            {
                Map(
                    ("type", "donut"),
                    ("labels", new object?[]
                    {
                        Map(("text", "Erste"), ("navigateTo", "Tasks/A")),
                        Map(("text", "Zweite"), ("navigateTo", "Tasks/B")),
                        Map(("text", "Dritte"), ("navigateTo", "Tasks/C"))
                    }),
                    ("series", new object?[] { Map(("name", "x"), ("data", new object?[] { 3, 4, 5 })) }))
            });

        private BunitNavigationManager Navigation
            => (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        private void ArrangeServices()
        {
            // MudBlazor spricht beim Aufbau mit dem Browser; im Test genuegt es, dass niemand daran
            // scheitert - geprueft wird der Klickweg, nicht das Zeichnen.
            JSInterop.Mode = JSRuntimeMode.Loose;
            Services.AddMudServices();
        }

        /// <summary>
        /// Die ERSTE Kategorie. Sie war der eigentliche Fehler: ohne eigenen Wert steht MudBlazors
        /// <c>SelectedIndex</c> auf 0, ein Klick auf 0 war damit keine Aenderung - und kam nie an.
        /// </summary>
        [TestMethod]
        public void ClickOnTheFirstCategory_Navigates()
        {
            ArrangeServices();
            IRenderedComponent<ChartWidgetView> cut =
                Render<ChartWidgetView>(ps => ps.Add(p => p.Panels, ThreeTargets()));

            cut.FindAll(SegmentSelector)[0].Click();

            Assert.AreEqual(1, Navigation.History.Count);
            StringAssert.EndsWith(Navigation.History.Last().Uri, "Tasks/A");
        }

        /// <summary>
        /// Zweimal dieselbe Kategorie. Ohne Zuruecksetzen bliebe MudBlazors innerer Stand auf ihr stehen,
        /// und der zweite Klick waere stumm - fuer eine Kachel, von der aus man kommt und wieder
        /// zurueckkehrt, der Normalfall.
        /// </summary>
        [TestMethod]
        public void ClickingTheSameCategoryTwice_NavigatesBothTimes()
        {
            ArrangeServices();
            IRenderedComponent<ChartWidgetView> cut =
                Render<ChartWidgetView>(ps => ps.Add(p => p.Panels, ThreeTargets()));

            cut.FindAll(SegmentSelector)[1].Click();
            cut.FindAll(SegmentSelector)[1].Click();

            Assert.AreEqual(2, Navigation.History.Count);
            Assert.IsTrue(Navigation.History.All(h => h.Uri.EndsWith("Tasks/B", StringComparison.Ordinal)),
                string.Join(" | ", Navigation.History.Select(h => h.Uri)));
        }

        /// <summary>
        /// Verschiedene Kategorien nacheinander - dass das Zuruecksetzen die Zuordnung nicht verschiebt.
        /// </summary>
        [TestMethod]
        public void ClickingDifferentCategories_KeepsTheirTargets()
        {
            ArrangeServices();
            IRenderedComponent<ChartWidgetView> cut =
                Render<ChartWidgetView>(ps => ps.Add(p => p.Panels, ThreeTargets()));

            cut.FindAll(SegmentSelector)[2].Click();
            cut.FindAll(SegmentSelector)[0].Click();
            cut.FindAll(SegmentSelector)[2].Click();

            CollectionAssert.AreEqual(new[] { "Tasks/C", "Tasks/A", "Tasks/C" },
                Navigation.History.Select(h => h.Uri[(h.Uri.IndexOf("Tasks/", StringComparison.Ordinal))..])
                    .ToArray());
        }

        /// <summary>
        /// Eine Kategorie OHNE Ziel darf den Ring nicht vergiften: sie loest keine Navigation aus, und die
        /// naechste Kategorie muss trotzdem noch ankommen. Genau dafuer wird die Auswahl auch dann
        /// zurueckgesetzt, wenn der Klick nichts bewirkt.
        /// </summary>
        [TestMethod]
        public void ClickOnACategoryWithoutTarget_LeavesTheOthersWorking()
        {
            ArrangeServices();
            IReadOnlyList<ChartWidgetPanel> panels = ChartWidgetPanel.Build(new object?[]
            {
                Map(
                    ("type", "donut"),
                    ("labels", new object?[]
                    {
                        "Ohne Ziel",
                        Map(("text", "Mit Ziel"), ("navigateTo", "Tasks/B"))
                    }),
                    ("series", new object?[] { Map(("name", "x"), ("data", new object?[] { 1, 2 })) }))
            });

            IRenderedComponent<ChartWidgetView> cut =
                Render<ChartWidgetView>(ps => ps.Add(p => p.Panels, panels));

            cut.FindAll(SegmentSelector)[0].Click();
            Assert.AreEqual(0, Navigation.History.Count);

            cut.FindAll(SegmentSelector)[1].Click();
            Assert.AreEqual(1, Navigation.History.Count);
            StringAssert.EndsWith(Navigation.History.Last().Uri, "Tasks/B");
        }
    }
}
