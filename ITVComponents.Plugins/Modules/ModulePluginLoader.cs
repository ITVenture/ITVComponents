#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using ITVComponents.ExtendedFormatting;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Plugins.Config;
using ITVComponents.Plugins.Initialization;

namespace ITVComponents.Plugins.Modules
{
    /// <summary>
    /// An <see cref="IDynamicLoader"/> for the plugin instances of the modules, as the update service keeps them in
    /// <c>plugins.json</c> (issue ModulePlugins-Instances, B). Load it as a plugin of the application's own configuration,
    /// like <see cref="SettingsDrivenLoader"/>:
    /// <code>
    /// modulePlugins  [ITVComponents.Plugins.dll]&lt;ITVComponents.Plugins.Modules.ModulePluginLoader&gt;"C:\ProgramData\…\targets\default\plugins.json","C:\Program Files\…\modules",$factory
    /// </code>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The file holds data, no construction strings. Every instance is checked before its construction string is built:
    /// a valid name, an installed module, the assembly in the module folder, and the type carrying the
    /// <c>ModulePlugin</c> mark with the same kind and load type. Whatever fails is logged as error and skipped; the
    /// application starts all the same.
    /// </para>
    /// <para>
    /// A type that implements <c>IHasDeviceSettings&lt;T&gt;</c> gets <c>$DeviceSettings</c> as its only constructor
    /// parameter; any other type is built without parameters.
    /// </para>
    /// <para>
    /// The file is read again whenever it changed, so a new <see cref="PluginLoadType.Scope"/> instance is found without a
    /// restart. A plugin of the same name from another loader (the application's own configuration) goes first: the
    /// instance is not loaded, and that is logged as error.
    /// </para>
    /// </remarks>
    public sealed class ModulePluginLoader : IPlugin, IDynamicLoader
    {
        /// <summary>
        /// The constructor parameter of a device with settings.
        /// </summary>
        public const string DeviceSettingsParameter = "$DeviceSettings";

        private readonly string instancesFile;
        private readonly string modulesDirectory;
        private readonly IPluginFactory factory;
        private readonly object sync = new object();
        private readonly ThreadLocal<bool> checkingShadow = new ThreadLocal<bool>();
        private readonly Dictionary<string, (DateTime Stamp, IReadOnlyList<ModulePluginMark> Marks)> marksByAssembly = new Dictionary<string, (DateTime, IReadOnlyList<ModulePluginMark>)>(StringComparer.OrdinalIgnoreCase);
        private Snapshot current = Snapshot.Empty;

        /// <param name="instancesFile">the <c>plugins.json</c> the update service writes</param>
        /// <param name="modulesDirectory">the folder of the modules (the same the <see cref="ModuleContextSelector"/> uses)</param>
        /// <param name="factory">the factory that builds the instances</param>
        public ModulePluginLoader(string instancesFile, string modulesDirectory, IPluginFactory factory)
        {
            this.instancesFile = Path.GetFullPath(instancesFile);
            this.modulesDirectory = Path.GetFullPath(modulesDirectory);
            this.factory = factory;
        }

        /// <summary>
        /// Uses the folder <c>modules</c> next to the application.
        /// </summary>
        public ModulePluginLoader(string instancesFile, IPluginFactory factory)
            : this(instancesFile, Path.Combine(AppContext.BaseDirectory, "modules"), factory)
        {
        }

        /// <inheritdoc />
        public string? UniqueName { get; set; }

        /// <inheritdoc />
        public event EventHandler? Disposed;

        /// <inheritdoc />
        public IEnumerable<string> LoadDynamicAssemblies(PluginLoadType currentLoadType, bool writeAccess = true)
        {
            var loaded = new List<string>();
            foreach (var entry in Current().Entries.Where(e => e.LoadType == currentLoadType))
            {
                if (factory is PluginFactory pluginFactory && pluginFactory.Contains(entry.Item.Name))
                {
                    LogEnvironment.LogEvent($"Module plugin instance {entry.Item.Name} is not loaded: a plugin of that name is already loaded, and the application's own configuration goes first.", LogSeverity.Error);
                    continue;
                }

                try
                {
                    factory.LoadPlugin<IPlugin>(entry.Item.Name, entry.Item.ConstructionString);
                    loaded.Add(entry.Item.Name);
                }
                catch (Exception ex)
                {
                    // A module instance that can not be built must not keep the application from starting (C.4).
                    LogEnvironment.LogEvent($"Module plugin instance {entry.Item.Name} ({entry.Instance.Type} of module {entry.Instance.Module}) could not be built: {ex.OutlineException()}", LogSeverity.Error);
                }
            }

            return loaded;
        }

        /// <inheritdoc />
        /// <remarks>Module plugins are closed types (issue ModulePlugins-Instances, A.2): there are no type arguments to give.</remarks>
        public bool HasParamsFor(string uniqueName) => false;

        /// <inheritdoc />
        public void GetGenericParams(string uniqueName, List<GenericTypeArgument> genericTypeArguments, Dictionary<string, object> customVariables, StringFormatProvider formatter)
        {
        }

        /// <inheritdoc />
        /// <remarks>Answers and never throws - the factory asks every loader (see <see cref="SettingsDrivenLoader"/>).</remarks>
        public bool HasScopedPlugin(string pluginName) => Scoped(pluginName) != null;

        /// <inheritdoc />
        public PluginConfigurationItem? GetScopedPlugin(string pluginName) => Scoped(pluginName)?.Item;

        /// <inheritdoc />
        public IEnumerable<PluginConfigurationItem> GetScopedPluginNames()
        {
            return Current().Entries.Where(e => e.LoadType == PluginLoadType.Scope)
                .Select(e => Scoped(e.Item.Name)?.Item)
                .Where(i => i != null)
                .Select(i => i!)
                .OrderBy(i => i.Name, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Gets the instance of a name, as far as it passed the checks - whatever its load type, and even if a plugin of
        /// the application's own configuration hides it.
        /// </summary>
        /// <param name="name">the name of the instance</param>
        /// <returns>the instance, or null if <c>plugins.json</c> has no usable instance of that name</returns>
        public ModulePluginEntry? GetInstance(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            try
            {
                return Current().ByName.TryGetValue(name, out var entry) ? entry : null;
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"The module plugin instances could not be read: {ex.OutlineException()}", LogSeverity.Error);
                return null;
            }
        }

        /// <summary>
        /// Gets the templates the installed modules offer (issue ModulePlugins-Instances, C.2): every type with the mark
        /// <see cref="ModulePluginKind.Template"/>, read from the metadata. Nothing is loaded.
        /// </summary>
        /// <returns>the templates; a module or assembly that can not be read is logged and left out</returns>
        public IReadOnlyList<ModulePluginTemplate> GetTemplates()
        {
            var templates = new List<ModulePluginTemplate>();
            if (!Directory.Exists(modulesDirectory))
            {
                LogEnvironment.LogDebugEvent($"There is no module folder {modulesDirectory}, so there are no templates.", LogSeverity.Report);
                return templates;
            }

            lock (sync)
            {
                foreach (var moduleDirectory in Directory.GetDirectories(modulesDirectory).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                {
                    var moduleId = Path.GetFileName(moduleDirectory);
                    if (!ModulePluginInstance.IsSafeModuleId(moduleId))
                    {
                        // The versions next to the active one (.new, .old) offer nothing.
                        continue;
                    }

                    foreach (var dll in Directory.GetFiles(moduleDirectory, "*.dll", SearchOption.TopDirectoryOnly).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                    {
                        if (PluginContract.IsContract(dll))
                        {
                            continue;
                        }

                        IReadOnlyList<ModulePluginMark> marks;
                        try
                        {
                            marks = MarksOf(dll, moduleDirectory);
                        }
                        catch (BadImageFormatException ex)
                        {
                            // A native DLL at the top of a module is legitimate; it simply carries no mark.
                            LogEnvironment.LogDebugEvent($"Module {moduleId}: {Path.GetFileName(dll)} is no managed assembly, it offers no template ({ex.Message}).", LogSeverity.Report);
                            continue;
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                        {
                            LogEnvironment.LogEvent($"Module {moduleId}: {dll} could not be read; its templates are not offered: {ex.OutlineException()}", LogSeverity.Error);
                            continue;
                        }

                        foreach (var mark in marks.Where(m => m.Kind == ModulePluginKind.Template))
                        {
                            var problems = new List<string>();
                            ModulePluginMarks.Check(new[] { mark }, problems, new List<string>());
                            if (problems.Count != 0)
                            {
                                LogEnvironment.LogEvent($"Module {moduleId}: template {mark.TypeName} is not offered: {string.Join("; ", problems)}", LogSeverity.Error);
                                continue;
                            }

                            templates.Add(new ModulePluginTemplate(moduleId, dll, mark));
                        }
                    }
                }
            }

            return templates;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            checkingShadow.Dispose();
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        private ModulePluginEntry? Scoped(string pluginName)
        {
            if (string.IsNullOrEmpty(pluginName) || checkingShadow.Value)
            {
                // While this loader asks the factory who else knows the name, it does not know it itself.
                return null;
            }

            Snapshot snapshot;
            try
            {
                snapshot = Current();
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"The module plugin instances could not be read: {ex.OutlineException()}", LogSeverity.Error);
                return null;
            }

            if (!snapshot.ByName.TryGetValue(pluginName, out var entry) || entry.LoadType != PluginLoadType.Scope)
            {
                return null;
            }

            var shadowed = snapshot.Shadowed.GetOrAdd(pluginName, IsShadowed);
            return shadowed ? null : entry;
        }

        private bool IsShadowed(string pluginName)
        {
            if (factory is not PluginFactory pluginFactory)
            {
                return false;
            }

            checkingShadow.Value = true;
            try
            {
                var other = pluginFactory.FindScopedPluginLoader(pluginName);
                if (other != null && !ReferenceEquals(other, this))
                {
                    LogEnvironment.LogEvent($"Module plugin instance {pluginName} is not used: the application's own configuration has a plugin of that name, which goes first.", LogSeverity.Error);
                    return true;
                }

                return false;
            }
            finally
            {
                checkingShadow.Value = false;
            }
        }

        /// <summary>
        /// The instances as of the last change of the file.
        /// </summary>
        private Snapshot Current()
        {
            var info = new FileInfo(instancesFile);
            var stamp = info.Exists ? info.LastWriteTimeUtc : DateTime.MinValue;
            var length = info.Exists ? info.Length : -1;
            lock (sync)
            {
                if (current.Stamp == stamp && current.Length == length)
                {
                    return current;
                }

                ModulePluginInstanceFile file;
                try
                {
                    file = ModulePluginInstanceFile.Read(instancesFile);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
                {
                    // Kept: the instances known so far stay usable. Read again on the next change of the file.
                    LogEnvironment.LogEvent($"{instancesFile} could not be read; the module plugin instances read before stay in use: {ex.OutlineException()}", LogSeverity.Error);
                    current = new Snapshot(stamp, length, current.Entries);
                    return current;
                }

                current = new Snapshot(stamp, length, Build(file));
                LogEnvironment.LogEvent($"Module plugin instances read from {instancesFile}: {(current.Entries.Count == 0 ? "none" : string.Join(", ", current.Entries.Select(e => $"{e.Item.Name} ({e.LoadType})")))}.", LogSeverity.Report);
                return current;
            }
        }

        private List<ModulePluginEntry> Build(ModulePluginInstanceFile file)
        {
            var entries = new List<ModulePluginEntry>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var instance in file.Instances)
            {
                if (!names.Add(instance.Name ?? string.Empty))
                {
                    LogEnvironment.LogEvent($"{instancesFile}: the name {instance.Name} is used more than once; only the first instance is loaded.", LogSeverity.Error);
                    continue;
                }

                var entry = Check(instance, out var problem);
                if (entry == null)
                {
                    LogEnvironment.LogEvent($"Module plugin instance {instance} is not loaded: {problem}.", LogSeverity.Error);
                    continue;
                }

                entries.Add(entry);
            }

            return entries;
        }

        private ModulePluginEntry? Check(ModulePluginInstance instance, out string? problem)
        {
            if (!instance.IsWellFormed(out problem))
            {
                return null;
            }

            instance.TryGetKind(out var kind);
            instance.TryGetLoadType(out var loadType);
            var moduleDirectory = Path.Combine(modulesDirectory, instance.Module!);
            if (!Directory.Exists(moduleDirectory))
            {
                problem = $"the module {instance.Module} is not installed";
                return null;
            }

            var assembly = Path.Combine(moduleDirectory, instance.Assembly!);
            if (!File.Exists(assembly))
            {
                problem = $"the module {instance.Module} has no assembly {instance.Assembly}";
                return null;
            }

            IReadOnlyList<ModulePluginMark> marks;
            try
            {
                marks = MarksOf(assembly, moduleDirectory);
            }
            catch (Exception ex) when (ex is BadImageFormatException or IOException or UnauthorizedAccessException)
            {
                problem = $"{assembly} is not readable: {ex.Message}";
                return null;
            }

            var mark = marks.FirstOrDefault(m => string.Equals(m.TypeName, instance.Type, StringComparison.Ordinal));
            if (mark == null)
            {
                problem = $"the type {instance.Type} does not carry the ModulePlugin mark in {instance.Assembly}";
                return null;
            }

            if (mark.Kind != kind || mark.LoadType != loadType)
            {
                problem = $"the file says {instance.Kind}/{instance.LoadType}, the mark of {mark.TypeName} says {ModulePluginInstance.KindText(mark.Kind)}/{mark.LoadType} - the file was changed by hand";
                return null;
            }

            var problems = new List<string>();
            var warnings = new List<string>();
            ModulePluginMarks.Check(new[] { mark }, problems, warnings);
            if (problems.Count != 0)
            {
                problem = string.Join("; ", problems);
                return null;
            }

            if (kind == ModulePluginKind.Infrastructure && !string.Equals(mark.Name, instance.Name, StringComparison.Ordinal))
            {
                problem = $"an infrastructure instance must have the name of its mark ({mark.Name})";
                return null;
            }

            if (mark.HasDeviceSettings == null)
            {
                LogEnvironment.LogEvent($"Module plugin instance {instance.Name}: whether {mark.TypeName} has device settings could not be decided ({mark.UnverifiedReason}); it is built without them.", LogSeverity.Warning);
            }

            var construction = $"[{assembly}]<{mark.TypeName}>{(mark.HasDeviceSettings == true ? DeviceSettingsParameter : string.Empty)}";
            return new ModulePluginEntry(instance, kind, new PluginConfigurationItem
            {
                Name = instance.Name!,
                ConstructionString = construction,
                LoadType = loadType,
                // A template instance is called by its own name; only the one instance of infrastructure by its mark.
                DisplayName = kind == ModulePluginKind.Infrastructure ? mark.DisplayName : null
            });
        }

        private IReadOnlyList<ModulePluginMark> MarksOf(string assembly, string moduleDirectory)
        {
            var stamp = File.GetLastWriteTimeUtc(assembly);
            if (marksByAssembly.TryGetValue(assembly, out var known) && known.Stamp == stamp)
            {
                return known.Marks;
            }

            Func<string, string?> resolve = name =>
            {
                var local = Path.Combine(moduleDirectory, name + ".dll");
                if (File.Exists(local))
                {
                    return local;
                }

                // The contracts are the application's.
                var host = Path.Combine(AppContext.BaseDirectory, name + ".dll");
                return File.Exists(host) ? host : null;
            };

            var marks = ModulePluginMarks.Read(assembly, resolve);
            marksByAssembly[assembly] = (stamp, marks);
            return marks;
        }


        private sealed class Snapshot
        {
            public static readonly Snapshot Empty = new Snapshot(DateTime.MaxValue, -2, new List<ModulePluginEntry>());

            public Snapshot(DateTime stamp, long length, List<ModulePluginEntry> entries)
            {
                Stamp = stamp;
                Length = length;
                Entries = entries;
                ByName = entries.ToDictionary(e => e.Item.Name, StringComparer.OrdinalIgnoreCase);
            }

            public DateTime Stamp { get; }

            public long Length { get; }

            public List<ModulePluginEntry> Entries { get; }

            public Dictionary<string, ModulePluginEntry> ByName { get; }

            /// <summary>Whether another loader has a plugin of the name, found out once per version of the file.</summary>
            public ConcurrentDictionary<string, bool> Shadowed { get; } = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
