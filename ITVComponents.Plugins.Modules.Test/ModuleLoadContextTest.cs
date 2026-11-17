using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Text.Json;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Modules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Plugins.Modules.Test
{
    /// <summary>
    /// What a module sees of the dependencies another module brings in another version.
    /// </summary>
    public interface IVersionProbe
    {
        string DependencyVersion { get; }
    }

    /// <summary>
    /// One load context per module (issue update service, P5): two modules bring the same assembly in different versions,
    /// both are loaded through a plugin factory with unchanged construction strings, and the contracts stay the host's.
    /// </summary>
    [TestClass]
    public class ModuleLoadContextTest
    {
        private static string root = null!;
        private static string modules = null!;

        [ClassInitialize]
        public static void BuildModules(TestContext context)
        {
            root = Directory.CreateTempSubdirectory("itv-modules").FullName;
            modules = Path.Combine(root, "modules");
            BuildModule("ModuleA", "1");
            BuildModule("ModuleB", "2");

            // ModuleB also brings its own copy of a contract (the plugin abstractions). It must not be used.
            File.Copy(typeof(IPlugin).Assembly.Location, Path.Combine(modules, "ModuleB", Path.GetFileName(typeof(IPlugin).Assembly.Location)));
        }

        [TestMethod]
        public void The_Itv_Packages_Are_Marked_As_Contracts()
        {
            Assert.IsTrue(PluginContract.IsContract(typeof(IPlugin).Assembly.Location), "ITVComponents.Plugins carries the contract mark");
            Assert.IsFalse(PluginContract.IsContract(typeof(ModuleLoadContextTest).Assembly.Location), "a test project is no contract");
            Assert.IsFalse(PluginContract.IsContract(Path.Combine(modules, "ModuleA", "SharedDep.dll")));
        }

        [TestMethod]
        public void Two_Modules_Load_Their_Own_Versions_Of_The_Same_Dependency()
        {
            using var factory = new PluginFactory(ScopeMode.PerAsyncContext) { AssemblyContextSelector = new ModuleContextSelector(modules) };

            var a = factory.LoadPlugin<IPlugin>("a", $"[{Path.Combine(modules, "ModuleA", "ModuleA.dll")}]<ModuleA.Probe>");
            var b = factory.LoadPlugin<IPlugin>("b", $"[{Path.Combine(modules, "ModuleB", "ModuleB.dll")}]<ModuleB.Probe>");

            Assert.AreEqual("1", ((IVersionProbe)a).DependencyVersion);
            Assert.AreEqual("2", ((IVersionProbe)b).DependencyVersion);
            Assert.IsInstanceOfType(AssemblyLoadContext.GetLoadContext(a.GetType().Assembly), typeof(ModuleLoadContext));
            Assert.AreNotSame(AssemblyLoadContext.GetLoadContext(a.GetType().Assembly), AssemblyLoadContext.GetLoadContext(b.GetType().Assembly));
        }

        [TestMethod]
        public void A_Contract_A_Module_Brings_Along_Is_Still_The_Hosts()
        {
            using var factory = new PluginFactory(ScopeMode.PerAsyncContext) { AssemblyContextSelector = new ModuleContextSelector(modules) };

            var b = factory.LoadPlugin<IPlugin>("b", $"[{Path.Combine(modules, "ModuleB", "ModuleB.dll")}]<ModuleB.Probe>");

            Assert.IsInstanceOfType(b, typeof(IPlugin), "the module's IPlugin is the host's IPlugin");
            var pluginsAssembly = b.GetType().GetInterfaces().Single(i => i.Name == nameof(IPlugin)).Assembly;
            Assert.AreSame(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(pluginsAssembly));
        }

        [TestMethod]
        public void Without_A_Selector_Everything_Stays_In_The_Default_Context()
        {
            using var factory = new PluginFactory(ScopeMode.PerAsyncContext);
            Assert.IsNull(factory.AssemblyContextSelector);
            Assert.IsNull(new ModuleContextSelector(modules).ContextFor(typeof(ModuleLoadContextTest).Assembly.Location));
        }

        private static void BuildModule(string name, string dependencyVersion)
        {
            var folder = Directory.CreateDirectory(Path.Combine(modules, name)).FullName;
            var dependency = Compile("SharedDep", $$"""
                [assembly: System.Reflection.AssemblyVersion("{{dependencyVersion}}.0.0.0")]
                namespace SharedDep { public static class Info { public static string Version => "{{dependencyVersion}}"; } }
                """, Path.Combine(folder, "SharedDep.dll"));
            Compile(name, $$"""
                namespace {{name}}
                {
                    public class Probe : ITVComponents.Plugins.IPlugin, ITVComponents.Plugins.Modules.Test.IVersionProbe
                    {
                        public string UniqueName { get; set; }
                        public string DependencyVersion => SharedDep.Info.Version;
                        public event System.EventHandler Disposed;
                        public void Dispose() => Disposed?.Invoke(this, System.EventArgs.Empty);
                    }
                }
                """, Path.Combine(folder, $"{name}.dll"), dependency, typeof(IPlugin).Assembly.Location, typeof(IVersionProbe).Assembly.Location);
            File.WriteAllText(Path.Combine(folder, ModuleManifest.FileName), JsonSerializer.Serialize(new ModuleManifest { Id = name, Version = "1.0", MainAssembly = $"{name}.dll" }));
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
