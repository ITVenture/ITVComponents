using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Plugins.Modules;

namespace ITVComponents.UpdateService.Modules
{
    /// <summary>
    /// The plugin instances of the modules in <c>plugins.json</c>, as far as the update service keeps them (issue
    /// ModulePlugins-Instances, B and C.1): the infrastructure instances follow the active version of their module, the
    /// template instances stay as they are, and a module version that would leave a template instance without its type
    /// is refused before it is switched to.
    /// </summary>
    /// <remarks>
    /// Nothing of a module is loaded: the marks are read from the metadata (<see cref="ModulePluginMarks"/>).
    /// </remarks>
    public sealed class ModulePluginRegistry
    {
        private readonly string instancesFile;
        private readonly ModuleFiles files;
        private readonly object sync = new object();

        /// <param name="instancesFile">the <c>plugins.json</c> of the application</param>
        /// <param name="files">the module folders</param>
        public ModulePluginRegistry(string instancesFile, ModuleFiles files)
        {
            this.instancesFile = Path.GetFullPath(instancesFile);
            this.files = files;
        }

        /// <summary>
        /// Gets the file the application reads.
        /// </summary>
        public string InstancesFile => instancesFile;

        /// <summary>
        /// Reads the instances.
        /// </summary>
        public ModulePluginInstanceFile Load()
        {
            lock (sync)
            {
                return ModulePluginInstanceFile.Read(instancesFile);
            }
        }

        /// <summary>
        /// Reads and checks the marks of a module folder.
        /// </summary>
        /// <param name="moduleDirectory">the folder (active or prepared version)</param>
        /// <param name="moduleId">the module, for the messages</param>
        /// <returns>the marks</returns>
        /// <exception cref="InvalidDataException">a mark is wrong</exception>
        public IReadOnlyList<ModulePluginMark> ReadMarks(string moduleDirectory, string moduleId)
        {
            var top = Directory.GetFiles(moduleDirectory, "*.dll", SearchOption.TopDirectoryOnly);
            var available = top.ToDictionary(f => Path.GetFileNameWithoutExtension(f), f => f, StringComparer.OrdinalIgnoreCase);
            Func<string, string?> resolve = name =>
            {
                if (available.TryGetValue(name, out var local))
                {
                    return local;
                }

                // The contracts are the application's.
                var host = Path.Combine(files.HostDirectory, name + ".dll");
                return File.Exists(host) ? host : null;
            };

            var marks = new List<ModulePluginMark>();
            foreach (var dll in top.Where(f => !PluginContract.IsContract(f)).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    marks.AddRange(ModulePluginMarks.Read(dll, resolve));
                }
                catch (BadImageFormatException ex)
                {
                    // A native DLL at the top of a module is legitimate; it simply carries no mark.
                    LogEnvironment.LogDebugEvent($"Module {moduleId}: {Path.GetFileName(dll)} is no managed assembly, it offers no plugin ({ex.Message}).", LogSeverity.Report);
                }
            }

            var problems = new List<string>();
            var warnings = new List<string>();
            ModulePluginMarks.Check(marks, problems, warnings);
            foreach (var warning in warnings)
            {
                LogEnvironment.LogEvent($"Module {moduleId}: {warning}", LogSeverity.Warning);
            }

            if (problems.Count != 0)
            {
                throw new InvalidDataException($"Module {moduleId} offers its plugins wrongly: {string.Join("; ", problems)}");
            }

            return marks;
        }

        /// <summary>
        /// Checks a prepared version before it is switched to (C.1): every template instance of the module keeps its
        /// type, and no infrastructure name is taken by an instance of another module or another prepared module.
        /// </summary>
        /// <param name="moduleId">the module</param>
        /// <param name="version">the prepared version, for the messages</param>
        /// <param name="otherPrepared">the infrastructure names the other modules prepared in the same pass bring, by
        /// module; this module's are added</param>
        /// <exception cref="InvalidDataException">the version can not be used; nothing is changed</exception>
        public void CheckPrepared(string moduleId, string version, Dictionary<string, string> otherPrepared)
        {
            var marks = ReadMarks(files.PreparedDirectory(moduleId), moduleId);
            var current = Load();
            var templates = marks.Where(m => m.Kind == ModulePluginKind.Template).Select(m => m.TypeName).ToHashSet(StringComparer.Ordinal);
            var orphaned = current.OfModule(moduleId)
                .Where(i => i.TryGetKind(out var kind) && kind == ModulePluginKind.Template && !templates.Contains(i.Type ?? string.Empty))
                .ToList();
            if (orphaned.Count != 0)
            {
                throw new InvalidDataException(string.Join("; ", orphaned.Select(i => $"type {i.Type} is used by {i.Name} and missing in {version}")));
            }

            var problems = new List<string>();
            foreach (var mark in marks.Where(m => m.Kind == ModulePluginKind.Infrastructure))
            {
                var taken = current.Instances.FirstOrDefault(i => string.Equals(i.Name, mark.Name, StringComparison.OrdinalIgnoreCase)
                                                                  && !(string.Equals(i.Module, moduleId, StringComparison.OrdinalIgnoreCase) && i.Kind == ModulePluginInstance.KindText(ModulePluginKind.Infrastructure)));
                if (taken != null)
                {
                    problems.Add($"the infrastructure name {mark.Name} is already used by {taken}");
                }
                else if (otherPrepared.TryGetValue(mark.Name!, out var other) && !string.Equals(other, moduleId, StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add($"the infrastructure name {mark.Name} is also brought by module {other}");
                }
            }

            if (problems.Count != 0)
            {
                throw new InvalidDataException($"Module {moduleId} {version}: {string.Join("; ", problems)}");
            }

            foreach (var mark in marks.Where(m => m.Kind == ModulePluginKind.Infrastructure))
            {
                otherPrepared[mark.Name!] = moduleId;
            }
        }

        /// <summary>
        /// Brings the instances of a module in line with its active version: infrastructure instances are added, updated
        /// and removed, template instances follow assembly and load type of their type. Part of switching a module and of
        /// switching it back; never throws.
        /// </summary>
        /// <param name="moduleId">the module</param>
        /// <returns>whether the instances are in line (false: the reason is logged as error)</returns>
        public bool Reconcile(string moduleId)
        {
            var directory = files.ActiveDirectory(moduleId);
            if (!Directory.Exists(directory))
            {
                return RemoveModule(moduleId);
            }

            IReadOnlyList<ModulePluginMark> marks;
            try
            {
                marks = ReadMarks(directory, moduleId);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                LogEnvironment.LogEvent($"The plugin instances of module {moduleId} could not be brought in line with its active version; they stay as they are: {ex.OutlineException()}", LogSeverity.Error);
                return false;
            }

            return Change(file =>
            {
                var changed = false;
                var infrastructure = marks.Where(m => m.Kind == ModulePluginKind.Infrastructure).ToDictionary(m => m.Name!, StringComparer.OrdinalIgnoreCase);
                var templates = marks.Where(m => m.Kind == ModulePluginKind.Template).ToDictionary(m => m.TypeName, StringComparer.Ordinal);
                foreach (var instance in file.OfModule(moduleId).ToList())
                {
                    if (!instance.TryGetKind(out var kind))
                    {
                        LogEnvironment.LogEvent($"{instancesFile}: {instance} has an unknown kind; it is left alone.", LogSeverity.Error);
                        continue;
                    }

                    if (kind == ModulePluginKind.Infrastructure)
                    {
                        if (!infrastructure.TryGetValue(instance.Name ?? string.Empty, out var mark))
                        {
                            file.Instances.Remove(instance);
                            LogEnvironment.LogEvent($"Module {moduleId} no longer offers the infrastructure plugin {instance.Name}; its instance was removed.", LogSeverity.Report);
                            changed = true;
                            continue;
                        }

                        infrastructure.Remove(mark.Name!);
                        changed |= Update(file, instance, ModulePluginInstance.FromMark(mark.Name!, moduleId, mark));
                    }
                    else if (templates.TryGetValue(instance.Type ?? string.Empty, out var template))
                    {
                        changed |= Update(file, instance, ModulePluginInstance.FromMark(instance.Name!, moduleId, template));
                    }
                    else
                    {
                        // Only after switching back: a version without the type was refused before switching to it (C.1).
                        // The instance stays, so it works again with a version that has the type; the loader skips it.
                        LogEnvironment.LogEvent($"The active version of module {moduleId} has no template {instance.Type}; the instance {instance.Name} stays but is not loaded.", LogSeverity.Error);
                    }
                }

                foreach (var mark in infrastructure.Values)
                {
                    var taken = file.Instances.FirstOrDefault(i => string.Equals(i.Name, mark.Name, StringComparison.OrdinalIgnoreCase));
                    if (taken != null)
                    {
                        LogEnvironment.LogEvent($"Module {moduleId} offers the infrastructure plugin {mark.Name}, but the name is used by {taken}; it is not created.", LogSeverity.Error);
                        continue;
                    }

                    file.Instances.Add(ModulePluginInstance.FromMark(mark.Name!, moduleId, mark));
                    LogEnvironment.LogEvent($"Module {moduleId}: infrastructure plugin {mark.Name} ({mark.TypeName}, {mark.LoadType}) created.", LogSeverity.Report);
                    changed = true;
                }

                return changed;
            });
        }

        /// <summary>
        /// Removes every instance of a module (C.4: the module was removed). Never throws.
        /// </summary>
        /// <returns>whether the file is in line (false: the reason is logged as error)</returns>
        public bool RemoveModule(string moduleId)
        {
            return Change(file =>
            {
                var removed = file.Instances.RemoveAll(i => string.Equals(i.Module, moduleId, StringComparison.OrdinalIgnoreCase));
                if (removed != 0)
                {
                    LogEnvironment.LogEvent($"Module {moduleId} is gone; its {removed} plugin instance(s) were removed.", LogSeverity.Report);
                }

                return removed != 0;
            });
        }

        private bool Change(Func<ModulePluginInstanceFile, bool> change)
        {
            lock (sync)
            {
                try
                {
                    var file = ModulePluginInstanceFile.Read(instancesFile);
                    if (change(file))
                    {
                        file.Write(instancesFile);
                    }

                    return true;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
                {
                    LogEnvironment.LogEvent($"{instancesFile} could not be updated: {ex.OutlineException()}", LogSeverity.Error);
                    return false;
                }
            }
        }

        private static bool Update(ModulePluginInstanceFile file, ModulePluginInstance current, ModulePluginInstance wanted)
        {
            if (current.SameAs(wanted))
            {
                return false;
            }

            file.Instances[file.Instances.IndexOf(current)] = wanted;
            LogEnvironment.LogEvent($"Plugin instance {wanted.Name} follows its module: {wanted.Type} in {wanted.Assembly}, {wanted.LoadType}.", LogSeverity.Report);
            return true;
        }
    }
}
