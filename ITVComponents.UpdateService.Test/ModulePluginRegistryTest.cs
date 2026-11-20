using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Modules;
using ITVComponents.UpdateService.Abstractions.Feed;
using ITVComponents.UpdateService.Feed;
using ITVComponents.UpdateService.Installation;
using ITVComponents.UpdateService.Modules;
using ITVComponents.UpdateService.Packages;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.UpdateService.Test
{
    /// <summary>
    /// The update service keeps plugins.json in line with the modules (issue ModulePlugins-Instances, B, C.1, C.4):
    /// infrastructure instances follow the active version, also on the way back; template instances stay; a version that
    /// would leave one without its type is refused before switching.
    /// </summary>
    [TestClass]
    public class ModulePluginRegistryTest
    {
        private const string PluginBody = """
            public string UniqueName { get; set; }
            public event System.EventHandler Disposed;
            public void Dispose() => Disposed?.Invoke(this, System.EventArgs.Empty);
            """;

        private string directory = null!;
        private string host = null!;
        private string modulesRoot = null!;
        private ModuleFiles files = null!;
        private ModulePluginRegistry registry = null!;
        private InstallStateStore store = null!;
        private ModuleManager manager = null!;
        private ZipDownloader downloader = null!;

        [TestInitialize]
        public void Setup()
        {
            directory = Directory.CreateTempSubdirectory("itv-module-plugins").FullName;
            host = Directory.CreateDirectory(Path.Combine(directory, "app")).FullName;
            File.Copy(typeof(IPlugin).Assembly.Location, Path.Combine(host, Path.GetFileName(typeof(IPlugin).Assembly.Location)));
            modulesRoot = Path.Combine(host, "modules");
            files = new ModuleFiles(modulesRoot, host);
            registry = new ModulePluginRegistry(Path.Combine(directory, "targets", "default", ModulePluginInstanceFile.FileName), files);
            store = new InstallStateStore(directory);
            downloader = new ZipDownloader(directory);
            var options = new UpdateServiceOptions { ProductId = "Shop.Agent", FeedUrl = "https://shop.example/updates", DataDirectory = directory, ApplicationPath = Path.Combine(host, "Agent.exe") };
            manager = new ModuleManager(options, downloader, files, store, null, registry);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                Directory.Delete(directory, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"Could not remove test directory {directory}: {ex}");
            }
        }

        [TestMethod]
        public void Switching_Creates_The_Infrastructure_Instances_And_Switching_Back_Restores_The_Former_Ones()
        {
            Prepare("Printer", "1.0", Infrastructure("OldChoices", "choices") + Template("EscPos", "Scope"));
            var state = store.Load();
            manager.Activate(new[] { Staged("Printer", "1.0") }, state, Array.Empty<int>());
            Assert.AreEqual("choices", Instances().Single().Name);
            Assert.AreEqual("infrastructure", Instances().Single().Kind);
            Assert.AreEqual("Shop.Printer.OldChoices", Instances().Single().Type);
            AddTemplate("Bondrucker1", "Printer", "Shop.Printer.EscPos");

            // Confirmed by the application (as ModuleManager.Report does it).
            files.Commit("Printer");
            state.ModuleActivation = null;

            Prepare("Printer", "2.0", Infrastructure("NewChoices", "windowsPrinters") + Template("EscPos", "Singleton"));
            manager.Activate(new[] { Staged("Printer", "2.0") }, state, Array.Empty<int>());

            CollectionAssert.AreEquivalent(new[] { "Bondrucker1", "windowsPrinters" }, Instances().Select(i => i.Name).ToArray());
            Assert.AreEqual("Singleton", Instances().Single(i => i.Name == "Bondrucker1").LoadType, "a template instance follows the load type of its mark");

            state.ModuleActivation!.RollbackRequested = true;
            manager.Rollback(state);

            CollectionAssert.AreEquivalent(new[] { "Bondrucker1", "choices" }, Instances().Select(i => i.Name).ToArray());
            Assert.AreEqual("Scope", Instances().Single(i => i.Name == "Bondrucker1").LoadType);
            Assert.AreEqual("Shop.Printer.OldChoices", Instances().Single(i => i.Name == "choices").Type);
        }

        [TestMethod]
        public void Switching_Back_A_First_Version_And_Removing_A_Module_Remove_Its_Instances()
        {
            Prepare("Printer", "1.0", Infrastructure("Choices", "choices"));
            var state = store.Load();
            manager.Activate(new[] { Staged("Printer", "1.0") }, state, Array.Empty<int>());
            Assert.AreEqual(1, Instances().Count);

            manager.Rollback(state);
            Assert.AreEqual(0, Instances().Count, "no former version: no instances");

            Prepare("Printer", "1.0", Infrastructure("Choices", "choices"));
            manager.Activate(new[] { Staged("Printer", "1.0") }, state, Array.Empty<int>());
            AddTemplate("Bondrucker1", "Printer", "Shop.Printer.Choices");
            var plan = new ModulePlan();
            plan.Removals.Add("Printer");
            manager.ApplyRemovals(plan, state);

            Assert.AreEqual(0, Instances().Count);
        }

        [TestMethod]
        public void A_Version_Without_The_Type_Of_A_Template_Instance_Is_Refused_Before_Switching()
        {
            Prepare("Printer", "1.0", Template("EscPos", "Scope"));
            var state = store.Load();
            manager.Activate(new[] { Staged("Printer", "1.0") }, state, Array.Empty<int>());
            AddTemplate("Bondrucker1", "Printer", "Shop.Printer.EscPos");

            Prepare("Printer", "2.1.0", Template("Renamed", "Scope"));
            var ex = Assert.ThrowsExactly<InvalidDataException>(() => registry.CheckPrepared("Printer", "2.1.0", new Dictionary<string, string>()));

            Assert.AreEqual("type Shop.Printer.EscPos is used by Bondrucker1 and missing in 2.1.0", ex.Message);
        }

        [TestMethod]
        public async Task A_Refused_Version_Is_Thrown_Away_And_Recorded_As_Failed()
        {
            Prepare("Printer", "1.0", Template("EscPos", "Scope"));
            var state = store.Load();
            manager.Activate(new[] { Staged("Printer", "1.0") }, state, Array.Empty<int>());
            store.Save(state);
            AddTemplate("Bondrucker1", "Printer", "Shop.Printer.EscPos");

            downloader.Zips["Printer-2.1.0"] = Zip("Printer", Template("Renamed", "Scope"));
            var package = new UpdatePackage { Id = "Printer", Kind = PackageKinds.Module, Version = "2.1.0", Url = "Printer-2.1.0.zip", Sha256 = "00" };
            var feed = new UpdateFeed();
            feed.Packages.Add(package);
            var plan = new ModulePlan();
            plan.Installs.Add(package);

            var ex = await Assert.ThrowsExactlyAsync<ModuleStagingException>(() => manager.StageAsync(new FeedResult(feed, new Uri("https://shop.example/updates/stable.json")), plan, CancellationToken.None));

            StringAssert.Contains(ex.Message, "type Shop.Printer.EscPos is used by Bondrucker1 and missing in 2.1.0");
            Assert.IsFalse(Directory.Exists(files.PreparedDirectory("Printer")), "the prepared version is thrown away");
            Assert.AreEqual("2.1.0", store.Load().FailedModules["Printer"].Version);
            Assert.AreEqual("1.0", store.Load().Modules["Printer"].Version);
        }

        [TestMethod]
        public void An_Infrastructure_Name_Taken_By_Another_Module_Or_Another_Prepared_Module_Is_Refused()
        {
            Prepare("Printer", "1.0", Infrastructure("Choices", "choices"));
            var state = store.Load();
            manager.Activate(new[] { Staged("Printer", "1.0") }, state, Array.Empty<int>());

            Prepare("Drawer", "1.0", Infrastructure("Choices", "Choices"));
            StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(() => registry.CheckPrepared("Drawer", "1.0", new Dictionary<string, string>())).Message, "already used by choices");

            var pass = new Dictionary<string, string>();
            Prepare("Terminal", "1.0", Infrastructure("Ports", "ports"));
            registry.CheckPrepared("Terminal", "1.0", pass);
            Prepare("Scanner", "1.0", Infrastructure("Ports", "ports"));
            StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(() => registry.CheckPrepared("Scanner", "1.0", pass)).Message, "also brought by module Terminal");
        }

        [TestMethod]
        public void A_Wrong_Mark_In_A_Prepared_Version_Is_Refused()
        {
            Prepare("Printer", "1.0", """
                [ITVComponents.Plugins.Modules.ModulePlugin(ITVComponents.Plugins.Modules.ModulePluginKind.Template, ITVComponents.Plugins.Initialization.PluginLoadType.Scope)]
                public class NotAPlugin { }
                """);

            var ex = Assert.ThrowsExactly<InvalidDataException>(() => registry.CheckPrepared("Printer", "1.0", new Dictionary<string, string>()));

            StringAssert.Contains(ex.Message, "does not implement ITVComponents.Plugins.IPlugin");
        }

        [TestMethod]
        public void Modules_Installed_Before_Get_Their_Instances_When_The_Service_Starts()
        {
            Prepare("Printer", "1.0", Infrastructure("Choices", "choices"));
            files.Activate("Printer");
            var state = store.Load();
            state.Modules["Printer"] = new InstalledModule { Version = "1.0" };
            store.Save(state);

            manager.ReconcilePlugins();

            Assert.AreEqual("choices", Instances().Single().Name);
        }

        [TestMethod]
        public void Template_Instances_Are_Created_And_Removed_Only_As_The_Service_Allows()
        {
            Prepare("Printer", "1.0", Infrastructure("Choices", "choices") + Template("EscPos", "Singleton"));
            var state = store.Load();
            manager.Activate(new[] { Staged("Printer", "1.0") }, state, Array.Empty<int>());
            state.AssignedModules = new List<string> { "Drawer", "Printer" };
            store.Save(state);

            Assert.IsNull(manager.CreatePluginInstance("printer", "Shop.Printer.EscPos", "Bondrucker1"));

            var created = Instances().Single(i => i.Name == "Bondrucker1");
            Assert.AreEqual("Printer", created.Module, "the module id as the service keeps it");
            Assert.AreEqual("Shop.Printer.dll", created.Assembly);
            Assert.AreEqual("template", created.Kind);
            Assert.AreEqual("Singleton", created.LoadType, "the load type comes from the mark");

            StringAssert.Contains(manager.CreatePluginInstance("Printer", "Shop.Printer.EscPos", "bondrucker1"), "already used by");
            StringAssert.Contains(manager.CreatePluginInstance("Printer", "Shop.Printer.EscPos", "choices"), "already used by");
            StringAssert.Contains(manager.CreatePluginInstance("Printer", "Shop.Printer.Choices", "Second"), "is infrastructure, not a template");
            StringAssert.Contains(manager.CreatePluginInstance("Printer", "Shop.Printer.Missing", "Second"), "has no type Shop.Printer.Missing");
            StringAssert.Contains(manager.CreatePluginInstance("Printer", "Shop.Printer.EscPos", "1bad"), "is not valid");
            StringAssert.Contains(manager.CreatePluginInstance("Printer", "Shop.Printer.EscPos", "bad:name"), "is not valid");
            StringAssert.Contains(manager.CreatePluginInstance("Scanner", "Shop.Printer.EscPos", "Second"), "is not assigned");
            StringAssert.Contains(manager.CreatePluginInstance("Drawer", "Shop.Printer.EscPos", "Second"), "is not installed yet");
            Assert.AreEqual(2, Instances().Count, "nothing refused was written");

            StringAssert.Contains(manager.RemovePluginInstance("choices"), "can not be removed");
            StringAssert.Contains(manager.RemovePluginInstance("Nothing"), "there is no plugin instance Nothing");
            Assert.IsNull(manager.RemovePluginInstance("BONDRUCKER1"));
            Assert.AreEqual("choices", Instances().Single().Name);
        }

        [TestMethod]
        public void A_Template_Missing_In_The_Version_About_To_Be_Installed_Is_Not_Instantiated()
        {
            Prepare("Printer", "1.0", Template("EscPos", "Scope"));
            var state = store.Load();
            manager.Activate(new[] { Staged("Printer", "1.0") }, state, Array.Empty<int>());
            state.AssignedModules = new List<string> { "Printer" };
            store.Save(state);

            // Prepared and checked while there was no instance; switched to once the application is down.
            Prepare("Printer", "2.0", Template("Renamed", "Scope"));
            registry.CheckPrepared("Printer", "2.0", new Dictionary<string, string>());

            StringAssert.Contains(manager.CreatePluginInstance("Printer", "Shop.Printer.EscPos", "Bondrucker1"), "about to be installed has no template");
            Assert.AreEqual(0, Instances().Count);
        }

        private static string Infrastructure(string type, string name) => $$"""
            [ITVComponents.Plugins.Modules.ModulePlugin(ITVComponents.Plugins.Modules.ModulePluginKind.Infrastructure, ITVComponents.Plugins.Initialization.PluginLoadType.Scope, Name = "{{name}}")]
            public class {{type}} : ITVComponents.Plugins.IPlugin { {{PluginBody}} }
            """;

        private static string Template(string type, string loadType) => $$"""
            [ITVComponents.Plugins.Modules.ModulePlugin(ITVComponents.Plugins.Modules.ModulePluginKind.Template, ITVComponents.Plugins.Initialization.PluginLoadType.{{loadType}})]
            public class {{type}} : ITVComponents.Plugins.IPlugin { {{PluginBody}} }
            """;

        private static StagedModule Staged(string id, string version) => new StagedModule { Id = id, Version = version };

        private List<ModulePluginInstance> Instances() => registry.Load().Instances;

        /// <summary>
        /// A template instance as the service will create it on request of the application (step 3 of the issue).
        /// </summary>
        private void AddTemplate(string name, string module, string type)
        {
            var file = registry.Load();
            file.Instances.Add(new ModulePluginInstance { Name = name, Module = module, Assembly = "Shop.Printer.dll", Type = type, Kind = "template", LoadType = "Scope" });
            file.Write(registry.InstancesFile);
        }

        /// <summary>
        /// Builds a prepared version of a module (what <see cref="ModuleFiles.Stage"/> leaves behind).
        /// </summary>
        private void Prepare(string id, string version, string types)
        {
            var folder = files.PreparedDirectory(id);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }

            Directory.CreateDirectory(folder);
            Compile(types, Path.Combine(folder, "Shop.Printer.dll"));
            File.WriteAllText(Path.Combine(folder, ModuleManifest.FileName), JsonSerializer.Serialize(new ModuleManifest { Id = id, Version = version, MainAssembly = "Shop.Printer.dll" }));
        }

        private string Zip(string id, string types)
        {
            var source = Directory.CreateDirectory(Path.Combine(directory, "zip-" + Guid.NewGuid().ToString("N"))).FullName;
            Compile(types, Path.Combine(source, "Shop.Printer.dll"));
            File.WriteAllText(Path.Combine(source, "Shop.Printer.deps.json"), "{}");
            var zip = Path.Combine(directory, $"{id}-{Guid.NewGuid():N}.zip");
            ZipFile.CreateFromDirectory(source, zip);
            return zip;
        }

        private static void Compile(string types, string output)
        {
            var source = "namespace Shop.Printer {" + types + "}";
            var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Where(p => Path.GetFileName(p).StartsWith("System.", StringComparison.Ordinal) || Path.GetFileName(p) is "mscorlib.dll" or "netstandard.dll");
            var compilation = CSharpCompilation.Create("Shop.Printer", new[] { CSharpSyntaxTree.ParseText(source) },
                platform.Append(typeof(IPlugin).Assembly.Location).Select(r => MetadataReference.CreateFromFile(r)),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var result = compilation.Emit(output);
            if (!result.Success)
            {
                Assert.Fail(string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
            }
        }

        /// <summary>
        /// Hands out prebuilt module zips by "id-version".
        /// </summary>
        private sealed class ZipDownloader : IPackageDownloader
        {
            private readonly string directory;

            public ZipDownloader(string directory)
            {
                this.directory = directory;
            }

            public Dictionary<string, string> Zips { get; } = new Dictionary<string, string>();

            public Task<string> DownloadAsync(UpdatePackage package, Uri source, CancellationToken cancellationToken)
            {
                var target = Path.Combine(directory, $"{package.Id}-{package.Version}.zip");
                File.Copy(Zips[$"{package.Id}-{package.Version}"], target, true);
                return Task.FromResult(target);
            }
        }
    }
}
