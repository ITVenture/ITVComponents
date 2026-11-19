#nullable enable
using System;
using ITVComponents.Plugins.Initialization;

namespace ITVComponents.Plugins.Modules
{
    /// <summary>
    /// Marks a type that a module offers as plugin. Without the mark, a type of a module is never offered - not even
    /// when it implements <see cref="IPlugin"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The mark is read from the metadata (<see cref="ModulePluginMarks"/>), by the update service as well as by the
    /// module build, without loading the module. Keep the constructor arguments and properties plain constants.
    /// </para>
    /// <para>
    /// The marked type must be a closed, non-abstract class that implements <see cref="IPlugin"/>. An open-generic
    /// type is refused: its type arguments would have to come from a type expression, and such an expression is code.
    /// Close the type in the module instead (<c>class EscPosPrinter : PrinterBase&lt;EscPosSettings&gt;</c>).
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ModulePluginAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ModulePluginAttribute"/> class
        /// </summary>
        /// <param name="kind">whether the plugin exists exactly once (<see cref="ModulePluginKind.Infrastructure"/>) or
        /// is a template for any number of instances (<see cref="ModulePluginKind.Template"/>)</param>
        /// <param name="loadType">how every instance of the plugin is loaded; only the author of the module knows
        /// whether the type must be built at start-up or on demand</param>
        public ModulePluginAttribute(ModulePluginKind kind, PluginLoadType loadType)
        {
            Kind = kind;
            LoadType = loadType;
        }

        /// <summary>
        /// Whether the plugin exists exactly once or is a template for instances.
        /// </summary>
        public ModulePluginKind Kind { get; }

        /// <summary>
        /// How every instance of the plugin is loaded.
        /// </summary>
        public PluginLoadType LoadType { get; }

        /// <summary>
        /// The fixed name of an <see cref="ModulePluginKind.Infrastructure"/> plugin (e.g. the name a
        /// <c>ChoiceSource</c> refers to). Required for infrastructure, not allowed for a template - the name of a
        /// template instance is given when the instance is created.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// The name shown when the plugin is offered, e.g. "Epson TM (ESC/POS)". Optional; the type name is shown
        /// otherwise.
        /// </summary>
        public string? DisplayName { get; set; }
    }
}
