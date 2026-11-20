#nullable enable
using System;
using System.Threading.Tasks;
using ITVComponents.Plugins.Config;
using ITVComponents.Plugins.Initialization;

namespace ITVComponents.Plugins.Modules
{
    /// <summary>
    /// An instance from <c>plugins.json</c> that passed the checks of the <see cref="ModulePluginLoader"/>.
    /// </summary>
    public sealed class ModulePluginEntry
    {
        internal ModulePluginEntry(ModulePluginInstance instance, ModulePluginKind kind, PluginConfigurationItem item)
        {
            Instance = instance;
            Kind = kind;
            Item = item;
        }

        /// <summary>
        /// Gets the instance as the file has it.
        /// </summary>
        public ModulePluginInstance Instance { get; }

        /// <summary>
        /// Gets the kind of the instance.
        /// </summary>
        public ModulePluginKind Kind { get; }

        /// <summary>
        /// Gets when the instance is built.
        /// </summary>
        public PluginLoadType LoadType => Item.LoadType;

        /// <summary>
        /// Gets the plugin as the factory builds it, with the construction string made by the loader.
        /// </summary>
        public PluginConfigurationItem Item { get; }
    }

    /// <summary>
    /// A type of an installed module that can be instantiated under a name of one's own (mark
    /// <see cref="ModulePluginKind.Template"/>).
    /// </summary>
    public sealed class ModulePluginTemplate
    {
        /// <summary>
        /// The character between module and type in <see cref="Key"/>; neither a module id nor a type name contains it.
        /// </summary>
        public const char KeySeparator = '/';

        internal ModulePluginTemplate(string moduleId, string assemblyPath, ModulePluginMark mark)
        {
            ModuleId = moduleId;
            AssemblyPath = assemblyPath;
            Mark = mark;
        }

        /// <summary>
        /// Gets the module.
        /// </summary>
        public string ModuleId { get; }

        /// <summary>
        /// Gets the assembly of the type, with its full path in the module folder.
        /// </summary>
        public string AssemblyPath { get; }

        /// <summary>
        /// Gets the mark of the type.
        /// </summary>
        public ModulePluginMark Mark { get; }

        /// <summary>
        /// Gets the key the template is chosen by: <c>&lt;module&gt;/&lt;type&gt;</c>.
        /// </summary>
        public string Key => $"{ModuleId}{KeySeparator}{Mark.TypeName}";

        /// <summary>
        /// Gets a construction string that names the type, to resolve it with <see cref="PluginFactory.TryGetPluginType"/>
        /// (never to build an instance: it has no parameters).
        /// </summary>
        public string TypeConstructionString => $"[{AssemblyPath}]<{Mark.TypeName}>";

        /// <summary>
        /// Splits a <see cref="Key"/>.
        /// </summary>
        /// <param name="key">the key</param>
        /// <param name="moduleId">the module</param>
        /// <param name="typeName">the type</param>
        /// <returns>whether the key has both parts</returns>
        public static bool TrySplitKey(string? key, out string moduleId, out string typeName)
        {
            moduleId = string.Empty;
            typeName = string.Empty;
            var at = key?.IndexOf(KeySeparator) ?? -1;
            if (at <= 0 || at == key!.Length - 1)
            {
                return false;
            }

            moduleId = key.Substring(0, at);
            typeName = key.Substring(at + 1);
            return true;
        }
    }

    /// <summary>
    /// Whoever may add and remove instances of templates in <c>plugins.json</c> - the update service, which alone writes
    /// the file (issue ModulePlugins-Instances, C.2 and C.3). The application reaches it with <c>UpdateServiceClient</c>.
    /// </summary>
    public interface IModulePluginInstanceService
    {
        /// <summary>
        /// Creates an instance of a template.
        /// </summary>
        /// <param name="moduleId">the module of the template</param>
        /// <param name="typeName">the type of the template</param>
        /// <param name="name">the name of the new instance</param>
        /// <returns>whether it was created, and if not, why</returns>
        Task<ModulePluginInstanceOutcome> CreateInstanceAsync(string moduleId, string typeName, string name);

        /// <summary>
        /// Removes an instance of a template.
        /// </summary>
        /// <param name="name">the name of the instance</param>
        /// <returns>whether it was removed, and if not, why</returns>
        Task<ModulePluginInstanceOutcome> RemoveInstanceAsync(string name);
    }

    /// <summary>
    /// The answer of <see cref="IModulePluginInstanceService"/>.
    /// </summary>
    public sealed class ModulePluginInstanceOutcome
    {
        /// <summary>
        /// Gets or sets a value indicating whether the change was made.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets why it was not made.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// The change was made.
        /// </summary>
        public static ModulePluginInstanceOutcome Done() => new ModulePluginInstanceOutcome { Success = true };

        /// <summary>
        /// The change was not made.
        /// </summary>
        public static ModulePluginInstanceOutcome Refused(string reason) => new ModulePluginInstanceOutcome { Reason = reason };
    }
}
