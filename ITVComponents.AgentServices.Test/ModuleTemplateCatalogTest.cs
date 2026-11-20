using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ITVComponents.AgentServices.Abstractions;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Modules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.AgentServices.Test
{
    /// <summary>
    /// Vorlagen aus Modulen im Katalog (Issue ModulePlugins-Instances, C.2/C.3): anbieten, mit geprüften Einstellungen
    /// anlegen, entfernen - eingetragen wird nur über den Update-Dienst.
    /// </summary>
    [TestClass]
    public class ModuleTemplateCatalogTest
    {
        private const string PluginBody = """
            public string UniqueName { get; set; }
            public event System.EventHandler Disposed;
            public void Dispose() => Disposed?.Invoke(this, System.EventArgs.Empty);
            """;

        private const string PrinterKey = "Shop.Printer/Shop.Printer.EscPos";

        private string root = null!;
        private string modules = null!;
        private string instancesFile = null!;
        private PluginFactory factory = null!;
        private ModulePluginLoader loader = null!;
        private FakeUpdateService service = null!;
        private MemoryStore store = null!;
        private PluginAgentObjectCatalog catalog = null!;

        [TestInitialize]
        public void Setup()
        {
            root = Directory.CreateTempSubdirectory("itv-module-templates").FullName;
            modules = Path.Combine(root, "modules");
            instancesFile = Path.Combine(root, "targets", "default", ModulePluginInstanceFile.FileName);
            var folder = Directory.CreateDirectory(Path.Combine(modules, "Shop.Printer")).FullName;
            Compile($$"""
                using ITVComponents.AgentServices.Abstractions;
                using ITVComponents.Plugins.Initialization;
                using ITVComponents.Plugins.Modules;
                namespace Shop.Printer
                {
                    public interface IShopPrinter { }

                    public class PrinterSettings
                    {
                        [DeviceSetting("Port", Required = true)]
                        public int Port { get; set; }

                        [DeviceSetting("Queue", ChoiceSource = "printerChoices")]
                        public string Queue { get; set; }
                    }

                    [ModulePlugin(ModulePluginKind.Template, PluginLoadType.Scope, DisplayName = "ESC/POS")]
                    public class EscPos : ITVComponents.Plugins.IPlugin, IShopPrinter, IHasDeviceSettings<PrinterSettings>
                    {
                        public EscPos(PrinterSettings settings) { Port = settings.Port; }
                        public int Port { get; }
                        {{PluginBody}}
                    }

                    [ModulePlugin(ModulePluginKind.Template, PluginLoadType.Singleton)]
                    public class Worker : ITVComponents.Plugins.IPlugin { {{PluginBody}} }

                    [ModulePlugin(ModulePluginKind.Infrastructure, PluginLoadType.Scope, Name = "printerChoices")]
                    public class Choices : ITVComponents.Plugins.IPlugin, IDeviceChoiceProvider
                    {
                        public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<SettingChoice>> GetChoicesAsync(string settingName, System.Collections.Generic.IReadOnlyDictionary<string, string> draft)
                            => System.Threading.Tasks.Task.FromResult<System.Collections.Generic.IReadOnlyList<SettingChoice>>(new[] { new SettingChoice { Value = "Kitchen" }, new SettingChoice { Value = "Bar" } });
                        {{PluginBody}}
                    }
                }
                """, Path.Combine(folder, "Shop.Printer.dll"));
            File.WriteAllText(Path.Combine(folder, ModuleManifest.FileName), JsonSerializer.Serialize(new ModuleManifest { Id = "Shop.Printer", Version = "1.0", MainAssembly = "Shop.Printer.dll" }));

            factory = new PluginFactory(ScopeMode.PerAsyncContext) { AssemblyContextSelector = new ModuleContextSelector(modules), AllowFactoryParameter = true };
            loader = factory.LoadPlugin<ModulePluginLoader>("modulePlugins",
                $"[{typeof(ModulePluginLoader).Assembly.Location}]<{typeof(ModulePluginLoader).FullName}>\"{Escape(instancesFile)}\",\"{Escape(modules)}\",$factory");
            service = new FakeUpdateService(loader, instancesFile);
            store = new MemoryStore();
            catalog = new PluginAgentObjectCatalog(factory, store, loader, service) { UniqueName = AgentCatalog.DefaultObjectName };
        }

        [TestCleanup]
        public void Cleanup()
        {
            catalog.Dispose();
            factory.Dispose();
            try
            {
                Directory.Delete(root, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Die Modul-Assemblies bleiben in ihrem Kontext geladen, bis der Testprozess endet.
                Console.WriteLine($"Could not remove test directory {root}: {ex}");
            }
        }

        [TestMethod]
        public async Task Templates_Are_Offered_By_Contract_With_Their_Settings_And_Infrastructure_Is_Not()
        {
            var printers = await catalog.GetTemplatesAsync("Shop.Printer.IShopPrinter");

            var printer = printers.Single();
            Assert.AreEqual(PrinterKey, printer.Key);
            Assert.AreEqual("Shop.Printer", printer.ModuleId);
            Assert.AreEqual("ESC/POS", printer.DisplayName);
            Assert.AreEqual("Scope", printer.LoadType);
            Assert.IsTrue(printer.HasSettings);
            CollectionAssert.AreEqual(new[] { "port", "queue" }, printer.Settings.Select(f => f.Name).ToArray());

            CollectionAssert.AreEquivalent(new[] { "Shop.Printer.EscPos", "Shop.Printer.Worker" }, (await catalog.GetTemplatesAsync(string.Empty)).Select(t => t.TypeName).ToArray());
            Assert.AreEqual(0, (await catalog.GetTemplatesAsync("No.Such.Contract")).Length);
        }

        [TestMethod]
        public async Task A_Device_Is_Created_With_Checked_Settings_And_Built_With_Them()
        {
            var refused = await catalog.CreateObjectAsync(PrinterKey, "Bondrucker1", """{ "port": "abc" }""");
            Assert.IsFalse(refused.Success);
            Assert.AreEqual("port", refused.Errors.Single().SettingName);
            Assert.AreEqual(0, service.Calls.Count, "wrong settings never reach the update service");
            Assert.AreEqual(0, store.Values.Count);

            var created = await catalog.CreateObjectAsync(PrinterKey, "Bondrucker1", """{ "port": 9100 }""");

            Assert.IsTrue(created.Success, created.Message);
            Assert.IsFalse(created.EffectiveAfterRestart, "a Scope instance is there at once");
            Assert.AreEqual("create Shop.Printer/Shop.Printer.EscPos Bondrucker1", service.Calls.Single());
            Assert.AreEqual("Bondrucker1", (await catalog.GetObjectsAsync("Shop.Printer.IShopPrinter")).Single().Name);
            StringAssert.Contains(await catalog.GetSettingsAsync("Bondrucker1"), "9100");

            using var scope = factory.NewScope(new Dictionary<string, object>(), null, false);
            var device = scope["Bondrucker1", true];
            Assert.IsNotNull(device);
            Assert.AreEqual(9100, device.GetType().GetProperty("Port")!.GetValue(device), "built with the settings stored before");
        }

        [TestMethod]
        public async Task A_Refusal_Of_The_Service_Leaves_Nothing_Behind()
        {
            service.Refusal = "the module Shop.Printer is not assigned to this machine";

            var result = await catalog.CreateObjectAsync(PrinterKey, "Bondrucker1", """{ "port": 9100 }""");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(service.Refusal, result.Message);
            Assert.AreEqual(0, store.Values.Count, "the settings stored for it are removed again");
        }

        [TestMethod]
        public async Task Names_And_Templates_Are_Checked_Before_The_Service_Is_Asked()
        {
            Assert.IsTrue((await catalog.CreateObjectAsync(PrinterKey, "Bondrucker1", """{ "port": 9100 }""")).Success);

            StringAssert.Contains((await catalog.CreateObjectAsync(PrinterKey, "bondrucker1", """{ "port": 1 }""")).Message, "already used");
            StringAssert.Contains((await catalog.CreateObjectAsync(PrinterKey, "1st", """{ "port": 1 }""")).Message, "no valid name");
            StringAssert.Contains((await catalog.CreateObjectAsync("Shop.Printer/Shop.Printer.Choices", "Other", null)).Message, "no template");
            StringAssert.Contains((await catalog.CreateObjectAsync("Gone/Gone.Type", "Other", null)).Message, "no template");
            Assert.AreEqual(1, service.Calls.Count);
        }

        [TestMethod]
        public async Task A_Singleton_Takes_Effect_After_A_Restart_And_Removing_Removes_Its_Settings_Too()
        {
            var worker = await catalog.CreateObjectAsync("Shop.Printer/Shop.Printer.Worker", "Nightly", null);
            Assert.IsTrue(worker.Success, worker.Message);
            Assert.IsTrue(worker.EffectiveAfterRestart);

            Assert.IsTrue((await catalog.CreateObjectAsync(PrinterKey, "Bondrucker1", """{ "port": 9100 }""")).Success);
            var removed = await catalog.RemoveObjectAsync("Bondrucker1");

            Assert.IsTrue(removed.Success, removed.Message);
            Assert.IsFalse(removed.EffectiveAfterRestart);
            Assert.AreEqual(0, store.Values.Count);
            Assert.IsNull(loader.GetInstance("Bondrucker1"));

            var nightly = await catalog.RemoveObjectAsync("Nightly");
            Assert.IsTrue(nightly.Success);
            Assert.IsTrue(nightly.EffectiveAfterRestart, "a Singleton runs until the restart");
        }

        [TestMethod]
        public async Task Infrastructure_Comes_With_Its_Module_And_Is_Not_Removed()
        {
            service.Add(new ModulePluginInstance { Name = "printerChoices", Module = "Shop.Printer", Assembly = "Shop.Printer.dll", Type = "Shop.Printer.Choices", Kind = "infrastructure", LoadType = "Scope" });

            var result = await catalog.RemoveObjectAsync("printerChoices");

            Assert.IsFalse(result.Success);
            StringAssert.Contains(result.Message, "can not be removed");
            Assert.AreEqual(0, service.Calls.Count);
        }

        [TestMethod]
        public async Task The_Choices_Of_A_Template_Come_Before_The_Device_Exists()
        {
            service.Add(new ModulePluginInstance { Name = "printerChoices", Module = "Shop.Printer", Assembly = "Shop.Printer.dll", Type = "Shop.Printer.Choices", Kind = "infrastructure", LoadType = "Scope" });

            var choices = await catalog.GetTemplateChoicesAsync(PrinterKey, "queue", null);

            CollectionAssert.AreEqual(new[] { "Kitchen", "Bar" }, choices.Select(c => c.Value).ToArray());
            Assert.AreEqual(0, (await catalog.GetTemplateChoicesAsync("Gone/Gone.Type", "queue", null)).Length);
            Assert.AreEqual(0, (await catalog.GetTemplateChoicesAsync(PrinterKey, "noSuchField", null)).Length);
            Assert.AreEqual(0, service.Calls.Count, "nothing is created for a choice");
        }

        [TestMethod]
        public async Task The_Module_Objects_Show_Every_Instance_Also_Those_Whose_Module_Is_Missing()
        {
            Assert.IsTrue((await catalog.CreateObjectAsync(PrinterKey, "Bondrucker1", """{ "port": 9100 }""")).Success);
            Assert.IsTrue((await catalog.CreateObjectAsync("Shop.Printer/Shop.Printer.Worker", "Nightly", null)).Success);
            service.Add(new ModulePluginInstance { Name = "printerChoices", Module = "Shop.Printer", Assembly = "Shop.Printer.dll", Type = "Shop.Printer.Choices", Kind = "infrastructure", LoadType = "Scope" });
            service.Add(new ModulePluginInstance { Name = "OldScale", Module = "Shop.Scale", Assembly = "Shop.Scale.dll", Type = "Shop.Scale.Scale", Kind = "template", LoadType = "Scope" });

            var objects = (await catalog.GetModuleObjectsAsync()).ToDictionary(o => o.Name);

            Assert.AreEqual(4, objects.Count);
            var printer = objects["Bondrucker1"];
            Assert.IsTrue(printer.Usable);
            Assert.IsTrue(printer.Removable);
            Assert.AreEqual(PrinterKey, printer.TemplateKey);
            Assert.AreEqual("Scope", printer.LoadType);
            Assert.AreEqual("Singleton", objects["Nightly"].LoadType);
            Assert.IsTrue(objects["Nightly"].Usable, "a Singleton is listed although it is no scoped plugin");
            Assert.IsFalse(objects["printerChoices"].Removable, "infrastructure comes with its module");
            Assert.IsNull(objects["printerChoices"].TemplateKey);
            var scale = objects["OldScale"];
            Assert.IsFalse(scale.Usable);
            Assert.IsTrue(scale.ModuleMissing);
            Assert.IsTrue(scale.Removable, "an instance of a missing module can still be removed (C.4)");
            StringAssert.Contains(scale.Problem, "not installed");

            var removed = await catalog.RemoveObjectAsync("OldScale");
            Assert.IsTrue(removed.Success, removed.Message);
            Assert.IsFalse((await catalog.GetModuleObjectsAsync()).Any(o => o.Name == "OldScale"));
        }

        [TestMethod]
        public async Task Without_Module_Plugins_There_Are_No_Module_Objects()
        {
            using var plain = new PluginAgentObjectCatalog(factory, store);

            Assert.AreEqual(0, (await plain.GetModuleObjectsAsync()).Length);
            Assert.AreEqual(0, (await plain.GetTemplateChoicesAsync(PrinterKey, "queue", null)).Length);
        }

        private static string Escape(string path) => path.Replace("\\", "\\\\");

        private static void Compile(string source, string output)
        {
            var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Where(p => Path.GetFileName(p).StartsWith("System.", StringComparison.Ordinal) || Path.GetFileName(p) is "mscorlib.dll" or "netstandard.dll");
            var compilation = CSharpCompilation.Create(Path.GetFileNameWithoutExtension(output), new[] { CSharpSyntaxTree.ParseText(source) },
                platform.Append(typeof(IPlugin).Assembly.Location).Append(typeof(IHasDeviceSettings<>).Assembly.Location)
                    .Select(r => MetadataReference.CreateFromFile(r)),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var result = compilation.Emit(output);
            if (!result.Success)
            {
                Assert.Fail(string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
            }
        }

        /// <summary>
        /// Steht für den Update-Dienst: schreibt plugins.json so, wie er es nach seinen Prüfungen tut.
        /// </summary>
        private sealed class FakeUpdateService : IModulePluginInstanceService
        {
            private readonly ModulePluginLoader loader;
            private readonly string file;

            public FakeUpdateService(ModulePluginLoader loader, string file)
            {
                this.loader = loader;
                this.file = file;
            }

            public List<string> Calls { get; } = new List<string>();

            public string? Refusal { get; set; }

            public Task<ModulePluginInstanceOutcome> CreateInstanceAsync(string moduleId, string typeName, string name)
            {
                Calls.Add($"create {moduleId}/{typeName} {name}");
                if (Refusal != null)
                {
                    return Task.FromResult(ModulePluginInstanceOutcome.Refused(Refusal));
                }

                var template = loader.GetTemplates().Single(t => t.ModuleId == moduleId && t.Mark.TypeName == typeName);
                Add(ModulePluginInstance.FromMark(name, moduleId, template.Mark));
                return Task.FromResult(ModulePluginInstanceOutcome.Done());
            }

            public Task<ModulePluginInstanceOutcome> RemoveInstanceAsync(string name)
            {
                Calls.Add($"remove {name}");
                Change(f => f.Instances.RemoveAll(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase)));
                return Task.FromResult(ModulePluginInstanceOutcome.Done());
            }

            public void Add(ModulePluginInstance instance) => Change(f => f.Instances.Add(instance));

            private void Change(Action<ModulePluginInstanceFile> change)
            {
                var before = File.Exists(file) ? File.GetLastWriteTimeUtc(file) : DateTime.MinValue;
                var current = ModulePluginInstanceFile.Read(file);
                change(current);
                current.Write(file);

                // Der Lader erkennt eine Änderung an Zeit und Grösse; die Zeit muss sich auch innerhalb eines Takts bewegen.
                if (File.GetLastWriteTimeUtc(file) <= before)
                {
                    File.SetLastWriteTimeUtc(file, before.AddSeconds(1));
                }
            }
        }
    }
}
