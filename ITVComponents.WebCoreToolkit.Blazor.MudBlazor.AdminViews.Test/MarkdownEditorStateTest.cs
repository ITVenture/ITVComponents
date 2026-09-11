using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using ITVComponents.WebCoreToolkit.Blazor.SharedComponents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.Test
{
    /// <summary>
    /// Der Umgang des <see cref="MarkdownEditor"/> mit seinem Text - der Teil, an dem Getipptes
    /// verloren gehen kann.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Der Editor BESITZT seinen Text und meldet ihn nur gebuendelt an Blazor. Fuer den Aufrufer steht
    /// in seiner Variablen also laufend ein veralteter Stand - und genau den wieder in den Editor zu
    /// schieben, ist die naheliegendste und teuerste Verwechslung: das Ergebnis waere, dass die
    /// zuletzt getippten Zeichen beim naechsten Rendern verschwinden. Diese Tests halten fest, wann
    /// nachgezogen wird und wann nicht.
    /// </para>
    /// <para>
    /// Gerendert statt beschrieben, weil die Regel aus dem Zusammenspiel von OnParametersSet und
    /// OnAfterRenderAsync entsteht - ein Aufruf der einzelnen Methoden sagte darueber nichts.
    /// </para>
    /// </remarks>
    [TestClass]
    public class MarkdownEditorStateTest : BunitContext
    {
        private const string Init = "init";
        private const string SetMarkdown = "setMarkdown";

        /// <summary>
        /// Der Wirt des Editors. Ein ES-Modul und kein Host-Skript - deshalb wird er hier als Modul
        /// eingerichtet; ein Setup auf der globalen Ebene liefe ins Leere.
        /// </summary>
        private const string ModulePath =
            "./_content/ITVComponents.WebCoreToolkit.Blazor.MudBlazor/markdown-editor.js";

        private BunitJSModuleInterop editorModule = default!;

        /// <summary>Der Editor meldet sich als aufgebaut - der Weg ohne Rueckfall-Textfeld.</summary>
        private void ArrangeWorkingEditor()
        {
            editorModule = JSInterop.SetupModule(ModulePath);
            editorModule.Mode = JSRuntimeMode.Loose;
            editorModule.Setup<bool>(Init, _ => true).SetResult(true);
            JSInterop.Mode = JSRuntimeMode.Loose;
            Services.AddMudServices();
            // Die Komponente protokolliert (Rueckfall, gescheitertes Einfuegen) - ohne Logging-Dienste
            // scheiterte schon das Aufloesen ihres ILogger.
            Services.AddLogging();
        }

        private int SetMarkdownCalls => editorModule.Invocations[SetMarkdown].Count;

        [TestMethod]
        public void EditorChange_KeepsTheEditorsText_WhenTheCallerRendersAgainWithTheOldValue()
        {
            ArrangeWorkingEditor();
            var cut = Render<MarkdownEditor>(p => p.Add(x => x.Value, "start"));

            // Der Benutzer tippt: der Editor meldet seinen neuen Stand. Die Variable des Aufrufers
            // bleibt dabei bewusst auf "start" - genau so sieht einfache Bindung aus.
            cut.InvokeAsync(() => cut.Instance.OnEditorChanged("start und mehr")).GetAwaiter().GetResult();

            var before = SetMarkdownCalls;
            cut.Render(p => p.Add(x => x.Value, "start"));

            Assert.AreEqual(before, SetMarkdownCalls,
                "Ein Rendern mit dem UNVERAENDERTEN Parameter darf den Editor nicht zuruecksetzen - "
                + "sonst verschwindet das zuletzt Getippte.");
        }

        [TestMethod]
        public void NewParameterValue_IsPushedIntoTheEditor()
        {
            ArrangeWorkingEditor();
            var cut = Render<MarkdownEditor>(p => p.Add(x => x.Value, "start"));

            var before = SetMarkdownCalls;
            cut.Render(p => p.Add(x => x.Value, "etwas ganz anderes"));

            Assert.AreEqual(before + 1, SetMarkdownCalls,
                "Reicht der Aufrufer einen NEUEN Wert herein (anderer Datensatz, Formular zurueckgesetzt), "
                + "muss der Editor ihn uebernehmen.");
        }

        [TestMethod]
        public void SetValueAsync_PushesEvenWhenTheValueLooksUnchanged()
        {
            ArrangeWorkingEditor();
            var cut = Render<MarkdownEditor>(p => p.Add(x => x.Value, string.Empty));

            // Der Fall aus dem Hilfe-Editor: zwei Sprachen, beide (noch) leer. An den Werten ist der
            // Wechsel nicht zu erkennen - der Text der einen Sprache bliebe sonst in der anderen stehen.
            cut.InvokeAsync(() => cut.Instance.OnEditorChanged("Text der ersten Sprache")).GetAwaiter().GetResult();

            var before = SetMarkdownCalls;
            cut.InvokeAsync(() => cut.Instance.SetValueAsync(string.Empty)).GetAwaiter().GetResult();

            Assert.AreEqual(before + 1, SetMarkdownCalls,
                "SetValueAsync ist der ausdrueckliche Weg und muss immer laden - auch wenn der neue Text "
                + "dem zuletzt hereingereichten gleicht.");
        }

        [TestMethod]
        public void WhenTheEditorCannotBeCreated_TheComponentFallsBackToAPlainField()
        {
            // Der Wirt ist da, aber sein Aufbau scheitert - der realistische Fall (fehlende Datei,
            // fremder AMD-Loader, kaputtes Bundle). Die Komponente faengt das und schaltet um.
            JSInterop.SetupModule(ModulePath)
                .Setup<bool>(Init, _ => true)
                .SetException(new InvalidOperationException("bundle did not register as window.toastui"));
            JSInterop.Mode = JSRuntimeMode.Loose;
            Services.AddMudServices();
            Services.AddLogging();

            var cut = Render<MarkdownEditor>(p => p.Add(x => x.Value, "roher Text"));

            Assert.AreEqual(0, cut.FindAll("div.itv-markdown-editor").Count,
                "Ohne Editor darf keine leere Flaeche stehenbleiben.");
            Assert.AreNotEqual(0, cut.FindAll("textarea").Count,
                "Der Rueckfall ist ein Textfeld mit rohem Markdown.");
        }

        [TestMethod]
        public void WithoutAResourcePicker_NoToolbarButtonIsRequested()
        {
            ArrangeWorkingEditor();
            Render<MarkdownEditor>(p => p.Add(x => x.Value, string.Empty));

            var options = InitOptions();
            Assert.AreEqual(0, ((Array)Value(options, "pickers")!).Length,
                "Ohne registrierten IMarkdownResourcePicker darf der Editor keinen Knopf anbieten - "
                + "ein Knopf, der nur eine Rechte-Meldung oeffnet, ist keiner.");
            Assert.AreEqual(false, Value(options, "allowUpload"));
        }

        [TestMethod]
        public void WithAResourcePicker_ButtonsAndSchemesReachTheEditor()
        {
            ArrangeWorkingEditor();
            Services.AddScoped<IMarkdownResourcePicker>(_ => new StubPicker());

            Render<MarkdownEditor>(p => p.Add(x => x.Value, string.Empty));

            var options = InitOptions();
            Assert.AreEqual(2, ((Array)Value(options, "pickers")!).Length, "Bild und Video.");
            Assert.AreEqual(true, Value(options, "allowUpload"));

            var schemes = (IReadOnlyDictionary<string, string>)Value(options, "schemes")!;
            Assert.AreEqual("/t/help/res/", schemes["resource:"],
                "Ohne die Praefix-Tabelle zeigte der Editor beim Schreiben lauter kaputte Bilder.");
        }

        [TestMethod]
        public void InsertResourceAsync_GoesThroughTheEditorsOwnCommand()
        {
            ArrangeWorkingEditor();
            var cut = Render<MarkdownEditor>(p => p.Add(x => x.Value, string.Empty));

            cut.InvokeAsync(() => cut.Instance.InsertResourceAsync(new MarkdownResourceReference
            {
                Kind = MarkdownResourceKind.Image, Url = "resource:logo", Text = "logo"
            })).GetAwaiter().GetResult();

            // Nicht ueber "insert": das schiebt Rohtext an die Einfuegemarke, und der bleibt im
            // WYSIWYG-Modus Text - beim Speichern escaped der Editor ihn, und im fertigen Dokument
            // steht statt des Bildes sein Alt-Text.
            Assert.AreEqual(0, editorModule.Invocations["insert"].Count, "Rohtext waere der falsche Weg.");

            var call = editorModule.Invocations["insertResource"].Single();
            Assert.AreEqual("Image", call.Arguments[1]);
            Assert.AreEqual("resource:logo", call.Arguments[2]);
            Assert.AreEqual("logo", call.Arguments[3]);
        }

        /// <summary>Die Optionen, mit denen die Komponente den Editor aufgebaut hat.</summary>
        private object InitOptions()
        {
            return editorModule.Invocations[Init].Single().Arguments[2]!;
        }

        /// <summary>
        /// Liest ein Feld des anonymen Options-Objekts. Ueber Reflexion, weil ein anonymer Typ genau
        /// das ist, was die Komponente an JavaScript reicht - ihn fuer den Test zu benennen hiesse,
        /// etwas anderes zu pruefen als das, was ausgeliefert wird.
        /// </summary>
        private static object? Value(object options, string name)
            => options.GetType().GetProperty(name)!.GetValue(options);

        /// <summary>Eine Ablage, die es gibt - mehr braucht dieser Test von ihr nicht.</summary>
        private sealed class StubPicker : IMarkdownResourcePicker
        {
            public IReadOnlyDictionary<string, string> UrlPrefixes { get; }
                = new Dictionary<string, string> { ["resource:"] = "/t/help/res/" };

            public Task<bool> CanPickAsync(CancellationToken ct = default) => Task.FromResult(true);

            public Task<bool> CanUploadAsync(CancellationToken ct = default) => Task.FromResult(true);

            public Task<MarkdownResourceReference?> PickAsync(MarkdownResourceKind kind,
                CancellationToken ct = default)
                => Task.FromResult<MarkdownResourceReference?>(new MarkdownResourceReference
                {
                    Kind = kind, Url = "resource:x", Text = "x"
                });

            public Task<string?> UploadAsync(MarkdownResourceUpload upload, CancellationToken ct = default)
                => Task.FromResult<string?>("x");
        }
    }
}
