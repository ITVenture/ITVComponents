using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using ITVComponents.Plugins;
using ITVComponents.UpdateService.Abstractions.Feed;
using ITVComponents.UpdateService.ModuleBuild;
using ITVComponents.UpdateService.Modules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.UpdateService.Test
{
    /// <summary>
    /// Packing a module at the end of its publish (§124): what the packer accepts, the service installs, and what the
    /// service would refuse already fails the publish.
    /// </summary>
    [TestClass]
    public class ModulePackagerTest
    {
        private string root = null!;
        private string publish = null!;

        /// <summary>A managed assembly without the contract mark (this test assembly).</summary>
        private static string PlainAssembly => typeof(ModulePackagerTest).Assembly.Location;

        /// <summary>An assembly with the contract mark (every published ITVComponents package carries it).</summary>
        private static string ContractAssembly => typeof(PluginFactory).Assembly.Location;

        [TestInitialize]
        public void Setup()
        {
            root = Path.Combine(Path.GetTempPath(), "modpack-" + Guid.NewGuid().ToString("N"));
            publish = Path.Combine(root, "publish");
            Directory.CreateDirectory(publish);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Could not remove test directory {root}: {ex}");
            }
        }

        [TestMethod]
        public void A_Plugin_Publish_Is_Packed_At_The_Root_Of_The_Zip_And_The_Service_Installs_It()
        {
            WriteModule("Shop.Printer");
            Directory.CreateDirectory(Path.Combine(publish, "runtimes", "win-x64", "native"));
            File.WriteAllText(Path.Combine(publish, "runtimes", "win-x64", "native", "printer.txt"), "native stand-in");
            var zip = Path.Combine(root, "Shop.Printer-1.0.0.zip");

            var result = ModulePackager.Pack(publish, zip);

            Assert.AreEqual("Shop.Printer.dll", result.MainAssembly);
            Assert.AreEqual(3, result.FileCount);
            Assert.AreEqual(new FileInfo(zip).Length, result.Size);
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(zip))
            {
                Assert.AreEqual(Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant(), result.Sha256);
            }

            using (var archive = ZipFile.OpenRead(zip))
            {
                CollectionAssert.AreEquivalent(new[] { "Shop.Printer.dll", "Shop.Printer.deps.json", "runtimes/win-x64/native/printer.txt" },
                    archive.Entries.Select(e => e.FullName).ToArray(), "relative paths with '/', no folder in front");
            }

            Assert.IsFalse(File.Exists(zip + ".tmp"));

            // The service takes it as it is.
            var host = Directory.CreateDirectory(Path.Combine(root, "host")).FullName;
            File.Copy(ContractAssembly, Path.Combine(host, Path.GetFileName(ContractAssembly)));
            var modules = Directory.CreateDirectory(Path.Combine(root, "modules")).FullName;
            new ModuleFiles(modules, host).Stage(zip, new UpdatePackage { Id = "printer", Kind = PackageKinds.Module, Version = "1.0.0" });
            Assert.IsTrue(File.Exists(Path.Combine(modules, "printer.new", "Shop.Printer.dll")));
            StringAssert.Contains(File.ReadAllText(Path.Combine(modules, "printer.new", "module.json")), "\"mainAssembly\":\"Shop.Printer.dll\"");
        }

        [TestMethod]
        public void Contracts_In_The_Publish_Output_Are_Left_Out_With_What_Belongs_To_Them_And_The_Service_Takes_The_Package()
        {
            // A publish copies them in as soon as a referenced project depends on them - Private=false does not reach
            // transitive project references.
            WriteModule("Shop.Printer");
            var contract = Path.GetFileNameWithoutExtension(ContractAssembly);
            File.Copy(ContractAssembly, Path.Combine(publish, contract + ".dll"));
            File.WriteAllText(Path.Combine(publish, contract + ".pdb"), "symbols");
            File.WriteAllText(Path.Combine(publish, contract + ".xml"), "<doc/>");
            File.WriteAllText(Path.Combine(publish, contract + ".dll.config"), "<configuration/>");
            Directory.CreateDirectory(Path.Combine(publish, "de"));
            // A real satellite is an assembly without the contract mark; any such assembly stands in for it.
            File.Copy(PlainAssembly, Path.Combine(publish, "de", contract + ".resources.dll"));
            var zip = Path.Combine(root, "m.zip");

            var result = ModulePackager.Pack(publish, zip);

            CollectionAssert.AreEqual(new[] { contract + ".dll" }, result.LeftOutContracts.ToArray());
            using (var archive = ZipFile.OpenRead(zip))
            {
                CollectionAssert.AreEquivalent(new[] { "Shop.Printer.dll", "Shop.Printer.deps.json" }, archive.Entries.Select(e => e.FullName).ToArray());
            }

            var host = Directory.CreateDirectory(Path.Combine(root, "host")).FullName;
            File.Copy(ContractAssembly, Path.Combine(host, contract + ".dll"));
            var modules = Directory.CreateDirectory(Path.Combine(root, "modules")).FullName;
            new ModuleFiles(modules, host).Stage(zip, new UpdatePackage { Id = "printer", Kind = PackageKinds.Module, Version = "1.0.0" });
        }

        [TestMethod]
        public void A_Contract_The_Host_Does_Not_Have_Is_Kept_When_Named()
        {
            WriteModule("Shop.Printer");
            File.Copy(ContractAssembly, Path.Combine(publish, Path.GetFileName(ContractAssembly)));
            var zip = Path.Combine(root, "m.zip");

            var result = ModulePackager.Pack(publish, zip, new[] { Path.GetFileName(ContractAssembly).ToUpperInvariant() });

            Assert.AreEqual(0, result.LeftOutContracts.Count);
            using var archive = ZipFile.OpenRead(zip);
            Assert.IsTrue(archive.Entries.Any(e => e.FullName == Path.GetFileName(ContractAssembly)));
        }

        [TestMethod]
        public void A_Main_Assembly_Marked_As_Contract_Is_A_Problem()
        {
            File.Copy(ContractAssembly, Path.Combine(publish, "Shop.Printer.dll"));
            File.WriteAllText(Path.Combine(publish, "Shop.Printer.deps.json"), "{}");

            StringAssert.Contains(ModulePackager.Validate(publish, null, out _).Single(), "marked as contract");
        }

        [TestMethod]
        public void Without_Or_With_Several_Deps_Files_The_Main_Assembly_Must_Be_Named()
        {
            File.Copy(PlainAssembly, Path.Combine(publish, "Shop.Printer.dll"));
            var none = ModulePackager.Validate(publish, null, out var main);
            Assert.IsNull(main);
            StringAssert.Contains(none.Single(), "EnableDynamicLoading");

            WriteModule("Shop.Printer");
            WriteModule("Shop.Other");
            StringAssert.Contains(ModulePackager.Validate(publish, null, out _).Single(), "2 .deps.json");

            File.WriteAllText(Path.Combine(publish, "module.json"), "{ \"mainAssembly\": \"Shop.Other.dll\" }");
            Assert.AreEqual(0, ModulePackager.Validate(publish, null, out main).Count);
            Assert.AreEqual("Shop.Other.dll", main);
        }

        [TestMethod]
        public void A_Module_Json_Naming_A_Missing_Assembly_Is_A_Problem()
        {
            WriteModule("Shop.Printer");
            File.WriteAllText(Path.Combine(publish, "module.json"), "{ \"mainAssembly\": \"Gone.dll\" }");

            StringAssert.Contains(ModulePackager.Validate(publish, null, out _).Single(), "Gone.dll");
        }

        [TestMethod]
        public void A_Self_Contained_Publish_Is_Refused()
        {
            WriteModule("Shop.Printer");
            File.WriteAllText(Path.Combine(publish, "hostfxr.dll"), "native stand-in");

            var problems = ModulePackager.Validate(publish, null, out _);

            Assert.IsTrue(problems.Any(p => p.Contains("self-contained")), string.Join(Environment.NewLine, problems));
        }

        [TestMethod]
        public void The_Package_Must_Not_Lie_In_The_Publish_Folder()
        {
            WriteModule("Shop.Printer");

            var ex = Assert.ThrowsExactly<ModulePackageException>(() => ModulePackager.Pack(publish, Path.Combine(publish, "m.zip")));

            StringAssert.Contains(ex.Problems.Single(), "pack itself");
        }

        [TestMethod]
        public void A_Missing_Or_Empty_Publish_Folder_Is_A_Problem()
        {
            StringAssert.Contains(ModulePackager.Validate(publish, null, out _).Single(), "empty");
            StringAssert.Contains(ModulePackager.Validate(Path.Combine(root, "nope"), null, out _).Single(), "does not exist");
        }

        [TestMethod]
        public void An_Existing_Package_Is_Replaced()
        {
            WriteModule("Shop.Printer");
            var zip = Path.Combine(root, "m.zip");
            File.WriteAllText(zip, "old");

            ModulePackager.Pack(publish, zip);

            using var archive = ZipFile.OpenRead(zip);
            Assert.AreEqual(2, archive.Entries.Count);
        }

        [TestMethod]
        public void A_Module_Without_Any_Plugin_Mark_Packs_With_A_Warning()
        {
            WriteModule("Shop.Printer");

            Assert.AreEqual(0, ModulePackager.Validate(publish, null, out _, out var warnings).Count);

            StringAssert.Contains(warnings.Single(), "no plugin");
            var result = ModulePackager.Pack(publish, Path.Combine(root, "m.zip"));
            Assert.AreEqual(1, result.Warnings.Count);
            Assert.AreEqual(0, result.Plugins.Count);
        }

        [TestMethod]
        public void Marked_Plugins_Are_Listed_And_Their_Contracts_Are_Followed_Before_They_Are_Left_Out()
        {
            // IPlugin comes through a contract that is in the publish output and left out of the package.
            File.Copy(ContractAssembly, Path.Combine(publish, Path.GetFileName(ContractAssembly)));
            CompileModule("Shop.Printer", """
                using ITVComponents.Plugins.Initialization;
                using ITVComponents.Plugins.Modules;
                namespace Shop.Printer
                {
                    [ModulePlugin(ModulePluginKind.Template, PluginLoadType.Scope, DisplayName = "Epson TM")]
                    public class EscPosPrinter : ITVComponents.Plugins.IPlugin
                    {
                        public string UniqueName { get; set; }
                        public event System.EventHandler Disposed;
                        public void Dispose() => Disposed?.Invoke(this, System.EventArgs.Empty);
                    }
                }
                """);

            var result = ModulePackager.Pack(publish, Path.Combine(root, "m.zip"));

            Assert.AreEqual(0, result.Warnings.Count, string.Join(Environment.NewLine, result.Warnings));
            StringAssert.Contains(result.Plugins.Single(), "Shop.Printer.EscPosPrinter in Shop.Printer.dll (Template, Scope)");
            CollectionAssert.AreEqual(new[] { Path.GetFileName(ContractAssembly) }, result.LeftOutContracts.ToArray());
        }

        [TestMethod]
        public void A_Wrong_Mark_Fails_The_Publish()
        {
            CompileModule("Shop.Printer", """
                [ITVComponents.Plugins.Modules.ModulePlugin(ITVComponents.Plugins.Modules.ModulePluginKind.Template, ITVComponents.Plugins.Initialization.PluginLoadType.Scope)]
                public class NotAPlugin { }
                """);

            var ex = Assert.ThrowsExactly<ModulePackageException>(() => ModulePackager.Pack(publish, Path.Combine(root, "m.zip")));

            StringAssert.Contains(ex.Problems.Single(), "NotAPlugin (Shop.Printer.dll): carries the ModulePlugin mark but does not implement");
            Assert.IsFalse(File.Exists(Path.Combine(root, "m.zip")));
        }

        private void WriteModule(string name)
        {
            File.Copy(PlainAssembly, Path.Combine(publish, name + ".dll"), true);
            File.WriteAllText(Path.Combine(publish, name + ".deps.json"), "{}");
        }

        /// <summary>
        /// Compiles a module assembly into the publish folder, against the plugin abstractions.
        /// </summary>
        private void CompileModule(string name, string source)
        {
            var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Where(p => Path.GetFileName(p).StartsWith("System.", StringComparison.Ordinal) || Path.GetFileName(p) is "mscorlib.dll" or "netstandard.dll");
            var compilation = CSharpCompilation.Create(name, new[] { CSharpSyntaxTree.ParseText(source) },
                platform.Append(ContractAssembly).Select(r => MetadataReference.CreateFromFile(r)),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var result = compilation.Emit(Path.Combine(publish, name + ".dll"));
            if (!result.Success)
            {
                Assert.Fail(string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
            }

            File.WriteAllText(Path.Combine(publish, name + ".deps.json"), "{}");
        }
    }
}
