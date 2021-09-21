using System;
using System.Collections.Generic;
using System.Reflection;

namespace ITVComponents.Plugins.PluginServices
{
    public class AssemblyPluginAnalyzer:ILoaderInterface, IPlugin
    {
        /// <summary>
        /// holds a list of a available plugins
        /// </summary>
        private List<string> availablePlugins = new List<string>();

        /// <summary>
        /// Initializes a new instance of the AssemblyPluginAnalyzerPlugin class
        /// </summary>
        /// <param name="factory">the plugin-factory that is used to load plugins</param>
        public AssemblyPluginAnalyzer(PluginFactory factory)
        {
            factory.PluginInitialized += PluginInitialized;
            factory.Disposed += FactoryGone;
        }

        private void FactoryGone(object? sender, EventArgs e)
        {
            if (sender is PluginFactory factory)
            {
                factory.PluginInitialized -= PluginInitialized;
                factory.Disposed -= FactoryGone;
            }
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Gets a list of declared types in the given assembly
        /// </summary>
        /// <param name="assemblyName">the assembly-Name for which to get declared types</param>
        /// <returns>a list of Type-Descriptors</returns>
        public TypeDescriptor[] DescribeAssembly(string assemblyName)
        {
            return AssemblyAnalyzer.DescribeAssembly(assemblyName, AnalyzeType, AnalyzeConstructor, AnalyzeArgument);
        }

        /// <summary>
        /// Gets a list of available plugins
        /// </summary>
        /// <returns>an array of string representing all available plugins</returns>
        public string[] GetAvailablePlugins()
        {
            return availablePlugins.ToArray();
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            availablePlugins.Clear();
            OnDisposed();
        }

        /// <summary>
        /// Analyzes the the current type and extends the descriptor with custom information
        /// </summary>
        /// <param name="currentType">the current type that is being described</param>
        /// <param name="descriptor">the type description that was estimated by the default-implementation</param>
        protected virtual void AnalyzeType(Type currentType, TypeDescriptor descriptor)
        {
        }

        /// <summary>
        /// Analyzes the the current constructor and extends the descriptor with custom information
        /// </summary>
        /// <param name="currentConstructor">the current constructor that is being described</param>
        /// <param name="descriptor">the constructor description that was estimated by the default-implementation</param>
        protected virtual void AnalyzeConstructor(ConstructorInfo currentConstructor, ConstructorDescriptor descriptor)
        {
        }

        /// <summary>
        /// Analyzes the the current parameter and extends the descriptor with custom information
        /// </summary>
        /// <param name="currentParameter">the current constructor-parameter that is being described</param>
        /// <param name="descriptor">the constructor-parameter description that was estimated by the default-implementation</param>
        protected virtual void AnalyzeArgument(ParameterInfo currentParameter, ConstructorParameterDescriptor descriptor)
        {
        }

        /// <summary>
        /// Raises the disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            EventHandler handler = Disposed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        /// <summary>
        /// Adds the names of all loaded plugins to a list of plugins that can be used by other plugins that need to be initialized
        /// </summary>
        /// <param name="sender">the event-sender</param>
        /// <param name="e">information about the loaded plugin</param>
        private void PluginInitialized(object sender, PluginInitializedEventArgs e)
        {
            availablePlugins.Add(e.PluginName);
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
