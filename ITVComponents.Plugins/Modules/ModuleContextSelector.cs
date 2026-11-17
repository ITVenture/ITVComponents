#nullable enable
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Serialization;
using ITVComponents.Helpers;
using ITVComponents.Logging;

namespace ITVComponents.Plugins.Modules
{
    /// <summary>
    /// Chooses the load context for an assembly a <see cref="PluginFactory"/> is about to load.
    /// </summary>
    /// <remarks>
    /// Without a selector the factory loads everything into the default context, as it always did. A host that runs
    /// modules gives its factory a <see cref="ModuleContextSelector"/>.
    /// </remarks>
    public interface IAssemblyContextSelector
    {
        /// <summary>
        /// Gets the context an assembly is loaded into.
        /// </summary>
        /// <param name="assemblyPath">the full path of the assembly</param>
        /// <returns>the context, or null for the default context</returns>
        AssemblyLoadContext? ContextFor(string assemblyPath);
    }

    /// <summary>
    /// The description of an installed module (<c>module.json</c> in its folder), written by whoever installs it.
    /// </summary>
    public sealed class ModuleManifest
    {
        /// <summary>The file name of the description.</summary>
        public const string FileName = "module.json";

        /// <summary>Gets or sets the id of the module.</summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        /// <summary>Gets or sets the version of the module.</summary>
        [JsonPropertyName("version")]
        public string? Version { get; set; }

        /// <summary>Gets or sets the main assembly, relative to the module folder; its <c>.deps.json</c> lists the dependencies.</summary>
        [JsonPropertyName("mainAssembly")]
        public string? MainAssembly { get; set; }

        /// <summary>
        /// Reads the description of a module folder, or null if there is none.
        /// </summary>
        public static ModuleManifest? Read(string moduleDirectory)
        {
            var path = Path.Combine(moduleDirectory, FileName);
            if (!File.Exists(path))
            {
                return null;
            }

            return JsonSerializer.Deserialize<ModuleManifest>(File.ReadAllText(path));
        }
    }

    /// <summary>
    /// Puts every assembly below <c>&lt;modules&gt;\&lt;id&gt;\</c> into the load context of module <c>id</c> - one per
    /// module - and everything else into the default context. The location decides, not an entry in the configuration:
    /// only the installer of the modules can write there.
    /// </summary>
    public sealed class ModuleContextSelector : IAssemblyContextSelector
    {
        private readonly string modulesRoot;
        private readonly ConcurrentDictionary<string, ModuleLoadContext> contexts = new ConcurrentDictionary<string, ModuleLoadContext>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a selector for the modules below a folder.
        /// </summary>
        /// <param name="modulesRoot">the folder that holds one folder per module</param>
        public ModuleContextSelector(string modulesRoot)
        {
            this.modulesRoot = Path.GetFullPath(modulesRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        }

        /// <inheritdoc />
        public AssemblyLoadContext? ContextFor(string assemblyPath)
        {
            var full = Path.GetFullPath(assemblyPath);
            if (!full.StartsWith(modulesRoot, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var relative = full.Substring(modulesRoot.Length);
            var separator = relative.IndexOf(Path.DirectorySeparatorChar);
            if (separator <= 0)
            {
                LogEnvironment.LogEvent($"{full} lies directly in the modules folder, not in a module's folder; it is loaded into the default context.", LogSeverity.Warning);
                return null;
            }

            var moduleDirectory = Path.Combine(modulesRoot, relative.Substring(0, separator));
            return contexts.GetOrAdd(moduleDirectory, directory => Create(directory, full));
        }

        private static ModuleLoadContext Create(string moduleDirectory, string requestedAssembly)
        {
            var id = Path.GetFileName(moduleDirectory);
            var main = requestedAssembly;
            try
            {
                var manifest = ModuleManifest.Read(moduleDirectory);
                if (manifest?.MainAssembly != null)
                {
                    main = Path.Combine(moduleDirectory, manifest.MainAssembly);
                    id = manifest.Id ?? id;
                }
                else
                {
                    LogEnvironment.LogEvent($"Module folder {moduleDirectory} has no {ModuleManifest.FileName} with a main assembly; {requestedAssembly} is taken as such.", LogSeverity.Warning);
                }
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"The {ModuleManifest.FileName} of {moduleDirectory} could not be read; {requestedAssembly} is taken as main assembly: {ex.OutlineException()}", LogSeverity.Error);
            }

            LogEnvironment.LogEvent($"Module {id} gets its own load context ({main}).", LogSeverity.Report);
            return new ModuleLoadContext(id, main);
        }
    }
}
