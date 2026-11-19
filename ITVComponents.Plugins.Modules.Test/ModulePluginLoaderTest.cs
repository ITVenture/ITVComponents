using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using ITVComponents.Helpers;
using ITVComponents.Plugins.Config;
using ITVComponents.Plugins.Initialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Plugins.Modules.Test
{
    /// <summary>
    /// A second loader that knows scoped plugins by name - stands in for the application's own configuration.
    /// </summary>
    public sealed class CompetingLoader : IDynamicLoader
    {
        public static readonly HashSet<string> Names = new HashSet<string>();

        public string UniqueName { get; set; } = string.Empty;

        public event EventHandler? Disposed;

        public IEnumerable<string> LoadDynamicAssemblies(PluginLoadType currentLoadType, bool writeAccess = true) => Array.Empty<string>();

        public bool HasParamsFor(string uniqueName) => false;

        public void GetGenericParams(string uniqueName, List<GenericTypeArgument> genericTypeArguments, Dictionary<string, object> customVariables, StringFormatProvider formatter)
        {
        }

        public bool HasScopedPlugin(string pluginName) => Names.Contains(pluginName);

        public PluginConfigurationItem GetScopedPlugin(string pluginName) => Names.Contains(pluginName) ? new PluginConfigurationItem { Name = pluginName, ConstructionString = "[nowhere.dll]<Nothing>", LoadType = PluginLoadType.Scope } : null!;

        public IEnumerable<PluginConfigurationItem> GetScopedPluginNames() => Names.Select(GetScopedPlugin).ToArray();

        public void Dispose() => Disposed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// The plugin instances of the modules come from plugins.json, written by the update service: checked field by
    /// field, built only from types carrying the mark, read again when the file changes (issue ModulePlugins-Instances, B).
    /// </summary>
    [TestClass]
    public class ModulePluginLoaderTest
    {
        private const string PluginBody = """
            public string UniqueName { get; set; }
            public event System.EventHandler Disposed;
            public void Dispose() => Disposed?.Invoke(this, System.EventArgs.Empty);
            """;

        private string root = null!;
        private string modules = null!;
        private string instancesFile = null!;
        private string printerDll = null!;

        [TestInitialize]
        public void Setup()
        {
            CompetingLoader.Names.Clear();
            root = Directory.CreateTempSubdirectory("itv-moduleplugins").FullName;
            modules = Path.Combine(root, "modules");
            instancesFile = Path.Combine(root, "targets", "default", "plugins.json");
            var folder = Directory.CreateDirectory(Path.Combine(modules, "Shop.Printer")).FullName;

            // The device-settings marker of the agent services, by name - the plugin abstractions do not reference them.
            var agentContracts = Compile("ITVComponents.AgentServices.Abstractions", """
                namespace ITVComponents.AgentServices.Abstractions { public interface IHasDeviceSettings<TSettings> where TSettings : class, new() { } }
                """, Path.Combine(folder, "ITVComponents.AgentServices.Abstractions.dll"));
            printerDll = Compile("Shop.Printer", $$"""
                using ITVComponents.Plugins.Initialization;
                using ITVComponents.Plugins.Modules;
                namespace Shop.Printer
                {
                    public class Settings { }

                    [ModulePlugin(ModulePluginKind.Template, PluginLoadType.Scope, DisplayName = "Epson TM")]
                    public class EscPosPrinter : ITVComponents.Plugins.IPlugin { {{PluginBody}} }

                    [ModulePlugin(ModulePluginKind.Template, PluginLoadType.Scope)]
                    public class SettingsPrinter : ITVComponents.Plugins.IPlugin, ITVComponents.AgentServices.Abstractions.IHasDeviceSettings<Settings>
                    {
                        public SettingsPrinter(Settings settings) { }
                        {{PluginBody}}
                    }

                    [ModulePlugin(ModulePluginKind.Infrastructure, PluginLoadType.Singleton, Name = "choices", DisplayName = "Windows printers")]
                    public class Choices : ITVComponents.Plugins.IPlugin { {{PluginBody}} }

                    public class Helper : ITVComponents.Plugins.IPlugin { {{PluginBody}} }
                }
                """, Path.Combine(folder, "Shop.Printer.dll"), typeof(IPlugin).Assembly.Location, agentContracts);
            File.WriteAllText(Path.Combine(folder, ModuleManifest.FileName), JsonSerializer.Serialize(new ModuleManifest { Id = "Shop.Printer", Version = "1.0", MainAssembly = "Shop.Printer.dll" }));
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The module assemblies stay loaded in their contexts until the test process ends.
                Console.WriteLine($"Could not remove test directory {root}: {ex}");
            }
        }

        [TestMethod]
        public void Only_Checked_Instances_Are_Offered_With_A_Construction_String_Built_By_The_Loader()
        {
            WriteInstances(
                Instance("Bondrucker1", "Shop.Printer.EscPosPrinter", "template", "Scope"),
                Instance("WithSettings", "Shop.Printer.SettingsPrinter", "template", "Scope"),
                Instance("choices", "Shop.Printer.Choices", "infrastructure", "Singleton"),
                Instance("Unmarked", "Shop.Printer.Helper", "template", "Scope"),
                Instance("WrongMode", "Shop.Printer.EscPosPrinter", "template", "Singleton"),
                Instance("WrongKind", "Shop.Printer.EscPosPrinter", "infrastructure", "Scope"),
                Instance("bad:name", "Shop.Printer.EscPosPrinter", "template", "Scope"),
                Instance("Bondrucker1", "Shop.Printer.EscPosPrinter", "template", "Scope"),
                new ModulePluginInstance { Name = "NoModule", Module = "Gone", Assembly = "Gone.dll", Type = "Gone.X", Kind = "template", LoadType = "Scope" },
                new ModulePluginInstance { Name = "Injected", Module = "Shop.Printer", Assembly = "Shop.Printer.dll", Type = "Shop.Printer.EscPosPrinter>\"x\"", Kind = "template", LoadType = "Scope" },
                new ModulePluginInstance { Name = "Escape", Module = "..", Assembly = "Shop.Printer.dll", Type = "Shop.Printer.EscPosPrinter", Kind = "template", LoadType = "Scope" });
            using var factory = Factory();
            var loader = new ModulePluginLoader(instancesFile, modules, factory);

            CollectionAssert.AreEqual(new[] { "Bondrucker1", "WithSettings" }, loader.GetScopedPluginNames().Select(i => i.Name).ToArray());
            var printer = loader.GetScopedPlugin("Bondrucker1")!;
            Assert.AreEqual($"[{printerDll}]<Shop.Printer.EscPosPrinter>", printer.ConstructionString);
            Assert.AreEqual(PluginLoadType.Scope, printer.LoadType);
            Assert.IsNull(printer.DisplayName, "a template instance is called by its own name");
            Assert.AreEqual($"[{printerDll}]<Shop.Printer.SettingsPrinter>{ModulePluginLoader.DeviceSettingsParameter}", loader.GetScopedPlugin("WithSettings")!.ConstructionString);
            foreach (var refused in new[] { "Unmarked", "WrongMode", "WrongKind", "bad:name", "NoModule", "Injected", "Escape", "choices" })
            {
                Assert.IsFalse(loader.HasScopedPlugin(refused), refused);
                Assert.IsNull(loader.GetScopedPlugin(refused), refused);
            }

            Assert.IsFalse(loader.HasParamsFor("Bondrucker1"));
        }

        [TestMethod]
        public void Singleton_Instances_Are_Built_At_Start_And_A_Missing_File_Means_No_Instances()
        {
            using var factory = Factory();
            var loader = new ModulePluginLoader(instancesFile, modules, factory);
            Assert.AreEqual(0, loader.GetScopedPluginNames().Count());

            WriteInstances(Instance("choices", "Shop.Printer.Choices", "infrastructure", "Singleton"), Instance("Bondrucker1", "Shop.Printer.EscPosPrinter", "template", "Scope"));

            CollectionAssert.AreEqual(new[] { "choices" }, loader.LoadDynamicAssemblies(PluginLoadType.Singleton).ToArray());
            Assert.IsTrue(factory.Contains("choices"));
            Assert.AreEqual(0, loader.LoadDynamicAssemblies(PluginLoadType.ScopeStartup).Count());
        }

        [TestMethod]
        public void A_New_Scope_Instance_Is_Found_Without_A_Restart_And_A_Removed_One_Is_Gone()
        {
            WriteInstances(Instance("Bondrucker1", "Shop.Printer.EscPosPrinter", "template", "Scope"));
            using var factory = Factory();
            var loader = factory.LoadPlugin<ModulePluginLoader>("modulePlugins", LoaderConstruction());
            Assert.IsTrue(loader.HasScopedPlugin("Bondrucker1"));
            Assert.IsFalse(loader.HasScopedPlugin("Bondrucker2"));
            Assert.IsNull(factory.DescribeScopedPlugin("Bondrucker2"));

            WriteInstances(Instance("Bondrucker2", "Shop.Printer.EscPosPrinter", "template", "Scope"));

            Assert.IsTrue(loader.HasScopedPlugin("Bondrucker2"));
            Assert.IsFalse(loader.HasScopedPlugin("Bondrucker1"));
            Assert.AreEqual("Shop.Printer.EscPosPrinter", factory.DescribeScopedPlugin("Bondrucker2")?.PluginType.FullName);
            Assert.AreSame(loader, factory.FindScopedPluginLoader("Bondrucker2"));
        }

        [TestMethod]
        public void A_Plugin_Of_The_Same_Name_From_Another_Loader_Goes_First()
        {
            WriteInstances(Instance("Bondrucker1", "Shop.Printer.EscPosPrinter", "template", "Scope"), Instance("Bondrucker2", "Shop.Printer.EscPosPrinter", "template", "Scope"));
            CompetingLoader.Names.Add("Bondrucker1");
            using var factory = Factory();

            // Registered first, so the factory would ask the module loader first - it must give way all the same.
            var loader = factory.LoadPlugin<ModulePluginLoader>("modulePlugins", LoaderConstruction());
            factory.LoadPlugin<CompetingLoader>("ownConfiguration", $"[{typeof(CompetingLoader).Assembly.Location}]<{typeof(CompetingLoader).FullName}>");

            Assert.IsFalse(loader.HasScopedPlugin("Bondrucker1"));
            Assert.IsTrue(loader.HasScopedPlugin("Bondrucker2"));
            CollectionAssert.AreEqual(new[] { "Bondrucker2" }, loader.GetScopedPluginNames().Select(i => i.Name).ToArray());
            Assert.IsInstanceOfType<CompetingLoader>(factory.FindScopedPluginLoader("Bondrucker1"));
        }

        [TestMethod]
        public void An_Unreadable_File_Keeps_The_Instances_Read_Before()
        {
            WriteInstances(Instance("Bondrucker1", "Shop.Printer.EscPosPrinter", "template", "Scope"));
            using var factory = Factory();
            var loader = new ModulePluginLoader(instancesFile, modules, factory);
            Assert.IsTrue(loader.HasScopedPlugin("Bondrucker1"));

            File.WriteAllText(instancesFile, "{ not json");
            File.SetLastWriteTimeUtc(instancesFile, DateTime.UtcNow.AddSeconds(5));

            Assert.IsTrue(loader.HasScopedPlugin("Bondrucker1"));
        }

        private PluginFactory Factory() => new PluginFactory(ScopeMode.PerAsyncContext) { AssemblyContextSelector = new ModuleContextSelector(modules), AllowFactoryParameter = true };

        private string LoaderConstruction()
            => $"[{typeof(ModulePluginLoader).Assembly.Location}]<{typeof(ModulePluginLoader).FullName}>\"{Escape(instancesFile)}\",\"{Escape(modules)}\",$factory";

        private static string Escape(string path) => path.Replace("\\", "\\\\");

        private static ModulePluginInstance Instance(string name, string type, string kind, string loadType)
            => new ModulePluginInstance { Name = name, Module = "Shop.Printer", Assembly = "Shop.Printer.dll", Type = type, Kind = kind, LoadType = loadType };

        private void WriteInstances(params ModulePluginInstance[] instances)
        {
            var before = File.Exists(instancesFile) ? File.GetLastWriteTimeUtc(instancesFile) : DateTime.MinValue;
            new ModulePluginInstanceFile { Instances = instances.ToList() }.Write(instancesFile);

            // The loader notices a change by time and size; make sure the time moves even within one tick of the clock.
            if (File.GetLastWriteTimeUtc(instancesFile) <= before)
            {
                File.SetLastWriteTimeUtc(instancesFile, before.AddSeconds(1));
            }
        }

        private static string Compile(string assemblyName, string source, string output, params string[] references)
        {
            var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Where(p => Path.GetFileName(p).StartsWith("System.", StringComparison.Ordinal) || Path.GetFileName(p) is "mscorlib.dll" or "netstandard.dll");
            var compilation = CSharpCompilation.Create(assemblyName, new[] { CSharpSyntaxTree.ParseText(source) },
                platform.Concat(references).Select(r => MetadataReference.CreateFromFile(r)),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var result = compilation.Emit(output);
            if (!result.Success)
            {
                Assert.Fail(string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
            }

            return output;
        }
    }
}
