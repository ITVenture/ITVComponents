using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ITVComponents.Plugins.Initialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Plugins.Modules.Test
{
    /// <summary>
    /// The ModulePlugin mark is read from the metadata, without loading the module, and checked the same way by the
    /// module build and the update service (issue ModulePlugins-Instances, A, A.1, A.2).
    /// </summary>
    [TestClass]
    public class ModulePluginMarksTest
    {
        private const string PluginBody = """
            public string UniqueName { get; set; }
            public event System.EventHandler Disposed;
            public void Dispose() => Disposed?.Invoke(this, System.EventArgs.Empty);
            """;

        private string folder = null!;

        private static string PluginsAssembly => typeof(IPlugin).Assembly.Location;

        [TestInitialize]
        public void Setup()
        {
            folder = Directory.CreateTempSubdirectory("itv-marks").FullName;
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                Directory.Delete(folder, true);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Could not remove test directory {folder}: {ex}");
            }
        }

        [TestMethod]
        public void Marks_Are_Read_With_Kind_Load_Type_Names_And_Only_Marked_Types_Are_Offered()
        {
            var dll = Compile("Shop.Printer", $$"""
                using ITVComponents.Plugins.Initialization;
                using ITVComponents.Plugins.Modules;
                namespace Shop.Printer
                {
                    [ModulePlugin(ModulePluginKind.Template, PluginLoadType.Scope, DisplayName = "Epson TM (ESC/POS)")]
                    public class EscPosPrinter : ITVComponents.Plugins.IPlugin { {{PluginBody}} }

                    [ModulePlugin(ModulePluginKind.Infrastructure, PluginLoadType.Singleton, Name = "windowsPrinters")]
                    public class WindowsPrinterChoices : ITVComponents.Plugins.IPlugin { {{PluginBody}} }

                    // A helper: implements IPlugin, but carries no mark - never offered.
                    public class Helper : ITVComponents.Plugins.IPlugin { {{PluginBody}} }

                    public class Outer
                    {
                        [ModulePlugin(ModulePluginKind.Template, PluginLoadType.ScopeStartup)]
                        public class Inner : ITVComponents.Plugins.IPlugin { {{PluginBody}} }
                    }
                }
                """, PluginsAssembly);

            var marks = ModulePluginMarks.Read(dll, Resolver()).OrderBy(m => m.TypeName).ToArray();

            CollectionAssert.AreEqual(new[] { "Shop.Printer.EscPosPrinter", "Shop.Printer.Outer+Inner", "Shop.Printer.WindowsPrinterChoices" },
                marks.Select(m => m.TypeName).ToArray());
            var printer = marks[0];
            Assert.AreEqual(ModulePluginKind.Template, printer.Kind);
            Assert.AreEqual(PluginLoadType.Scope, printer.LoadType);
            Assert.AreEqual("Epson TM (ESC/POS)", printer.DisplayName);
            Assert.IsNull(printer.Name);
            Assert.AreEqual("Shop.Printer.dll", printer.AssemblyFile);
            Assert.AreEqual(PluginLoadType.ScopeStartup, marks[1].LoadType);
            Assert.AreEqual(ModulePluginKind.Infrastructure, marks[2].Kind);
            Assert.AreEqual(PluginLoadType.Singleton, marks[2].LoadType);
            Assert.AreEqual("windowsPrinters", marks[2].Name);
            Assert.IsTrue(marks.All(m => m.ImplementsPlugin == true && !m.IsOpenGeneric && !m.IsAbstract));
            AssertClean(marks);
        }

        [TestMethod]
        public void IPlugin_Is_Found_Through_Base_Classes_Generic_Bases_And_Interfaces_In_Other_Assemblies()
        {
            var contracts = Compile("Shop.Contracts", $$"""
                namespace Shop.Contracts
                {
                    public interface IReceiptPrinter : ITVComponents.Plugins.IPlugin { }
                    public interface IHasSettings<T> : ITVComponents.Plugins.IPlugin { }
                    public abstract class PrinterBase<TSettings> : ITVComponents.Plugins.IPlugin { {{PluginBody}} }
                }
                """, PluginsAssembly);
            var dll = Compile("Shop.Printer", $$"""
                using ITVComponents.Plugins.Initialization;
                using ITVComponents.Plugins.Modules;
                namespace Shop.Printer
                {
                    public class Settings { }

                    [ModulePlugin(ModulePluginKind.Template, PluginLoadType.Scope)]
                    public class ViaInterface : Shop.Contracts.IReceiptPrinter { {{PluginBody}} }

                    [ModulePlugin(ModulePluginKind.Template, PluginLoadType.Scope)]
                    public class ViaGenericInterface : Shop.Contracts.IHasSettings<Settings> { {{PluginBody}} }

                    [ModulePlugin(ModulePluginKind.Template, PluginLoadType.Scope)]
                    public class ViaGenericBase : Shop.Contracts.PrinterBase<Settings> { }

                    public class LocalBase : Shop.Contracts.PrinterBase<int> { }

                    [ModulePlugin(ModulePluginKind.Template, PluginLoadType.Scope)]
                    public class ViaLocalBase : LocalBase { }
                }
                """, PluginsAssembly, contracts);

            var marks = ModulePluginMarks.Read(dll, Resolver(contracts));

            Assert.AreEqual(4, marks.Count);
            foreach (var mark in marks)
            {
                Assert.AreEqual(true, mark.ImplementsPlugin, $"{mark.TypeName}: {mark.UnverifiedReason}");
            }

            AssertClean(marks);
        }

        [TestMethod]
        public void A_Hierarchy_That_Can_Not_Be_Followed_Is_A_Warning_Not_A_Problem()
        {
            var contracts = Compile("Shop.Contracts", $$"""
                namespace Shop.Contracts { public abstract class PrinterBase : ITVComponents.Plugins.IPlugin { {{PluginBody}} } }
                """, PluginsAssembly);
            var dll = Compile("Shop.Printer", """
                [ITVComponents.Plugins.Modules.ModulePlugin(ITVComponents.Plugins.Modules.ModulePluginKind.Template, ITVComponents.Plugins.Initialization.PluginLoadType.Scope)]
                public class Printer : Shop.Contracts.PrinterBase { }
                """, PluginsAssembly, contracts);

            // Shop.Contracts is not available to the reader.
            var mark = ModulePluginMarks.Read(dll, Resolver()).Single();

            Assert.IsNull(mark.ImplementsPlugin);
            StringAssert.Contains(mark.UnverifiedReason, "Shop.Contracts");
            var problems = new List<string>();
            var warnings = new List<string>();
            ModulePluginMarks.Check(new[] { mark }, problems, warnings);
            Assert.AreEqual(0, problems.Count, string.Join(Environment.NewLine, problems));
            StringAssert.Contains(warnings.Single(), "could not verify");
        }

        [TestMethod]
        public void Wrong_Marks_Are_Problems()
        {
            var dll = Compile("Shop.Broken", $$"""
                using ITVComponents.Plugins.Initialization;
                using ITVComponents.Plugins.Modules;
                namespace Shop.Broken
                {
                    [ModulePlugin(ModulePluginKind.Template, PluginLoadType.Scope)]
                    public class NoPlugin { }

                    [ModulePlugin(ModulePluginKind.Template, PluginLoadType.Scope)]
                    public class Open<T> : ITVComponents.Plugins.IPlugin { {{PluginBody}} }

                    [ModulePlugin(ModulePluginKind.Template, PluginLoadType.Scope)]
                    public abstract class Abstract : ITVComponents.Plugins.IPlugin { {{PluginBody}} }

                    [ModulePlugin(ModulePluginKind.Infrastructure, PluginLoadType.Scope)]
                    public class InfraWithoutName : ITVComponents.Plugins.IPlugin { {{PluginBody}} }

                    [ModulePlugin(ModulePluginKind.Infrastructure, PluginLoadType.Scope, Name = "bad:name")]
                    public class InfraBadName : ITVComponents.Plugins.IPlugin { {{PluginBody}} }

                    [ModulePlugin(ModulePluginKind.Infrastructure, PluginLoadType.Scope, Name = "choices")]
                    public class InfraA : ITVComponents.Plugins.IPlugin { {{PluginBody}} }

                    [ModulePlugin(ModulePluginKind.Infrastructure, PluginLoadType.Scope, Name = "Choices")]
                    public class InfraB : ITVComponents.Plugins.IPlugin { {{PluginBody}} }

                    [ModulePlugin(ModulePluginKind.Template, PluginLoadType.Scope, Name = "fixed")]
                    public class TemplateWithName : ITVComponents.Plugins.IPlugin { {{PluginBody}} }

                    [ModulePlugin((ModulePluginKind)7, (PluginLoadType)9)]
                    public class Undefined : ITVComponents.Plugins.IPlugin { {{PluginBody}} }
                }
                """, PluginsAssembly);

            var problems = new List<string>();
            var warnings = new List<string>();
            ModulePluginMarks.Check(ModulePluginMarks.Read(dll, Resolver()), problems, warnings);

            var all = string.Join(Environment.NewLine, problems);
            Assert.AreEqual(0, warnings.Count, string.Join(Environment.NewLine, warnings));
            AssertProblem(problems, "Shop.Broken.NoPlugin", "does not implement", all);
            AssertProblem(problems, "Shop.Broken.Open`1", "closed type", all);
            AssertProblem(problems, "Shop.Broken.Abstract", "abstract", all);
            AssertProblem(problems, "Shop.Broken.InfraWithoutName", "needs its fixed Name", all);
            AssertProblem(problems, "Shop.Broken.InfraBadName", "is not valid", all);
            AssertProblem(problems, "Shop.Broken.InfraB", "also used by Shop.Broken.InfraA", all);
            AssertProblem(problems, "Shop.Broken.TemplateWithName", "must not have a Name", all);
            AssertProblem(problems, "Shop.Broken.Undefined", "unknown kind 7", all);
            AssertProblem(problems, "Shop.Broken.Undefined", "unknown load type 9", all);
            Assert.AreEqual(9, problems.Count, all);
        }

        [TestMethod]
        public void A_File_Without_Metadata_Has_No_Marks()
        {
            var native = Path.Combine(folder, "native.dll");
            File.WriteAllBytes(native, new byte[] { 0x4D, 0x5A });

            Assert.ThrowsExactly<BadImageFormatException>(() => ModulePluginMarks.Read(native, null));
            Assert.AreEqual(0, ModulePluginMarks.Read(PluginsAssembly, null).Count, "the plugin abstractions mark nothing");
        }

        [TestMethod]
        public void Instance_Names()
        {
            foreach (var valid in new[] { "Bondrucker1", "windowsPrinters", "a", "Kasse_2.Drucker-A", new string('x', 64) })
            {
                Assert.IsTrue(ModulePluginMarks.IsValidInstanceName(valid), valid);
            }

            foreach (var invalid in new[] { null, "", "1drucker", "_x", "a:b", "a b", "a/b", "ä", new string('x', 65) })
            {
                Assert.IsFalse(ModulePluginMarks.IsValidInstanceName(invalid), invalid ?? "null");
            }
        }

        private static void AssertClean(IEnumerable<ModulePluginMark> marks)
        {
            var problems = new List<string>();
            var warnings = new List<string>();
            ModulePluginMarks.Check(marks, problems, warnings);
            Assert.AreEqual(0, problems.Count + warnings.Count, string.Join(Environment.NewLine, problems.Concat(warnings)));
        }

        private static void AssertProblem(List<string> problems, string type, string text, string all)
        {
            Assert.IsTrue(problems.Any(p => p.StartsWith(type + " (", StringComparison.Ordinal) && p.Contains(text)), $"expected '{text}' for {type}:{Environment.NewLine}{all}");
        }

        private Func<string, string?> Resolver(params string[] more)
        {
            var known = more.Append(PluginsAssembly).ToDictionary(p => Path.GetFileNameWithoutExtension(p), p => p, StringComparer.OrdinalIgnoreCase);
            return name => known.TryGetValue(name, out var path) ? path : null;
        }

        private string Compile(string assemblyName, string source, params string[] references)
        {
            var output = Path.Combine(folder, assemblyName + ".dll");
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
