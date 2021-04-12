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
        /// Gets a list of available plugins
        /// </summary>
        /// <returns>an array of string representing all available plugins</returns>
        string[] GetAvailablePlugins();
    }
}
