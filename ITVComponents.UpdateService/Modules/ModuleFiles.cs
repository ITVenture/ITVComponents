using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Plugins.Modules;
using ITVComponents.UpdateService.Abstractions.Feed;

namespace ITVComponents.UpdateService.Modules
{
    /// <summary>
    /// The files of the modules: <c>&lt;modules&gt;\&lt;id&gt;\</c> is the active version, <c>&lt;id&gt;.new\</c> the one
    /// prepared, <c>&lt;id&gt;.old\</c> the former one until the application confirms the new one.
    /// </summary>
    /// <remarks>
    /// The folder lies below the application's installation folder, where only SYSTEM and the administrators may write.
    /// Switching happens only while the application is not running: it holds the files of the modules it loaded.
    /// </remarks>
    public sealed class ModuleFiles
    {
        private readonly string modulesRoot;
        private readonly string hostDirectory;

        /// <param name="modulesRoot">the folder of the modules</param>
        /// <param name="hostDirectory">the installation folder of the application; a contract found there may not come with a module</param>
        public ModuleFiles(string modulesRoot, string hostDirectory)
        {
            this.modulesRoot = Path.GetFullPath(modulesRoot);
            this.hostDirectory = Path.GetFullPath(hostDirectory);
        }

        /// <summary>
        /// Gets the installation folder of the application.
        /// </summary>
        public string HostDirectory => hostDirectory;

        /// <summary>
        /// Gets the folder of the active version of a module.
        /// </summary>
        public string ActiveDirectory(string moduleId) => Path.Combine(modulesRoot, SafeId(moduleId));

        /// <summary>
        /// Gets the folder of the prepared version of a module.
        /// </summary>
        public string PreparedDirectory(string moduleId) => Path.Combine(modulesRoot, SafeId(moduleId) + ".new");

        /// <summary>
        /// Unpacks a checked package next to the active version and writes its <c>module.json</c>.
        /// </summary>
        /// <exception cref="InvalidDataException">when the package is not a usable module; nothing is left behind</exception>
        public void Stage(string packagePath, UpdatePackage package)
        {
            var id = SafeId(package.Id);
            var target = Path.Combine(modulesRoot, id + ".new");
            DeleteIfExists(target);
            Directory.CreateDirectory(target);
            try
            {
                Extract(packagePath, target);
                var main = MainAssemblyOf(target);
                var contract = Directory.EnumerateFiles(target, "*.dll", SearchOption.AllDirectories)
                    .FirstOrDefault(f => PluginContract.IsContract(f) && File.Exists(Path.Combine(hostDirectory, Path.GetFileName(f))));
                if (contract != null)
                {
                    throw new InvalidDataException($"Module {id} brings the contract {Path.GetFileName(contract)}, which the application has itself. Build it with <Private>false</Private> / ExcludeAssets=\"runtime\" for its contract references.");
                }

                File.WriteAllText(Path.Combine(target, ModuleManifest.FileName), JsonSerializer.Serialize(new ModuleManifest { Id = id, Version = package.Version, MainAssembly = main }));
            }
            catch
            {
                DeleteIfExists(target);
                throw;
            }
        }

        /// <summary>
        /// Switches to the prepared version; the active one is kept as former version.
        /// </summary>
        /// <returns>whether there was a former version</returns>
        public bool Activate(string moduleId)
        {
            var id = SafeId(moduleId);
            var active = Path.Combine(modulesRoot, id);
            var prepared = Path.Combine(modulesRoot, id + ".new");
            var former = Path.Combine(modulesRoot, id + ".old");
            if (!Directory.Exists(prepared))
            {
                throw new InvalidOperationException($"Module {id} has no prepared version to switch to.");
            }

            DeleteIfExists(former);
            var hadFormer = Directory.Exists(active);
            if (hadFormer)
            {
                Directory.Move(active, former);
            }

            Directory.Move(prepared, active);
            return hadFormer;
        }

        /// <summary>
        /// The new version is confirmed: the former one is deleted.
        /// </summary>
        public void Commit(string moduleId)
        {
            DeleteIfExists(Path.Combine(modulesRoot, SafeId(moduleId) + ".old"));
        }

        /// <summary>
        /// Switches back to the former version, or removes the module if it had none.
        /// </summary>
        public void Rollback(string moduleId)
        {
            var id = SafeId(moduleId);
            var active = Path.Combine(modulesRoot, id);
            var former = Path.Combine(modulesRoot, id + ".old");
            DeleteIfExists(active);
            if (Directory.Exists(former))
            {
                Directory.Move(former, active);
            }
        }

        /// <summary>
        /// Removes a module entirely.
        /// </summary>
        public void Remove(string moduleId)
        {
            var id = SafeId(moduleId);
            DeleteIfExists(Path.Combine(modulesRoot, id));
            DeleteIfExists(Path.Combine(modulesRoot, id + ".old"));
            DeleteIfExists(Path.Combine(modulesRoot, id + ".new"));
        }

        /// <summary>
        /// Throws a prepared version away.
        /// </summary>
        public void Discard(string moduleId)
        {
            DeleteIfExists(Path.Combine(modulesRoot, SafeId(moduleId) + ".new"));
        }

        /// <summary>
        /// Unpacks a zip; an entry that would end up outside the target (<c>..\</c>, absolute path) refuses the whole package.
        /// </summary>
        internal static void Extract(string zipPath, string target)
        {
            var root = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            using var zip = ZipFile.OpenRead(zipPath);
            foreach (var entry in zip.Entries)
            {
                var destination = Path.GetFullPath(Path.Combine(root, entry.FullName));
                if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"The package contains the entry '{entry.FullName}', which points outside the module folder.");
                }

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destination);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                entry.ExtractToFile(destination, false);
            }
        }

        private static string MainAssemblyOf(string directory)
        {
            var manifest = ModuleManifest.Read(directory);
            if (!string.IsNullOrWhiteSpace(manifest?.MainAssembly) && File.Exists(Path.Combine(directory, manifest.MainAssembly)))
            {
                return manifest.MainAssembly;
            }

            var deps = Directory.GetFiles(directory, "*.deps.json");
            if (deps.Length == 1)
            {
                var main = Path.GetFileName(deps[0])[..^".deps.json".Length] + ".dll";
                if (File.Exists(Path.Combine(directory, main)))
                {
                    return main;
                }
            }

            throw new InvalidDataException("The package names no main assembly: it needs a module.json with mainAssembly, or exactly one .deps.json next to its assembly.");
        }

        private static string SafeId(string? id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || id.Contains("..") || id.EndsWith(".new", StringComparison.OrdinalIgnoreCase) || id.EndsWith(".old", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"'{id}' is no valid module id.");
            }

            return id;
        }

        private static void DeleteIfExists(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            try
            {
                Directory.Delete(directory, true);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"{directory} could not be deleted: {ex.OutlineException()}", LogSeverity.Error);
                throw;
            }
        }
    }
}
