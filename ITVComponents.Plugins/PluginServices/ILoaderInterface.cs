using System;
using System.Reflection;

namespace ITVComponents.Plugins.PluginServices
{
    /// <summary>
    /// 
    /// </summary>
    public interface ILoaderInterface
    {
        /// <summary>
        /// Gets a list of declared types in the given assembly
        /// </summary>
        /// <param name="assemblyName">the assembly-Name for which to get declared types</param>
        /// <returns>a list of Type-Descriptors</returns>
        TypeDescriptor[] DescribeAssembly(string assemblyName);


        /// <summary>
        /// Gets a list of declared types in the given assembly
        /// </summary>
        /// <param name="assembly">the assembly for which to get declared types</param>
        /// <returns>a list of Type-Descriptors</returns>
        TypeDescriptor[] DescribeAssembly(Assembly assembly);

        /// <summary>
        /// Describes a single type
        /// </summary>
        /// <param name="type">the type to describe</param>
        /// <returns>a description of the given type</returns>
        TypeDescriptor DescribeType(Type type);

        /// <summary>
        /// Gets a list of available plugins
        /// </summary>
        /// <returns>an array of string representing all available plugins</returns>
        string[] GetAvailablePlugins();
    }
}
