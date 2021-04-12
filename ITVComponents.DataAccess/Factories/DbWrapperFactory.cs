using System;
using ITVComponents.Plugins;

namespace ITVComponents.DataAccess.Factories
{
    /// <summary>
    /// Factory used to load the database layer
    /// </summary>
    public class DbWrapperFactory:IDisposable
    {
        /// <summary>
        /// the factory used to load the database layer
        /// </summary>
        private PluginFactory innerFactory;

        /// <summary>
        /// Initializes a new instance of the DbWrapperFactory class
        /// </summary>
        public DbWrapperFactory()
        {
            innerFactory = new PluginFactory(false);
        }

        /// <summary>
        /// Initializes a Wrapper object defined by the provided constructor string
        /// </summary>
        /// <param name="constructorString">a plugin string for loading a db component</param>
        /// <returns>a created wrapper</returns>
        public IDbWrapper GetWrapper(string constructorString)
        {
            return innerFactory.LoadPlugin<IDbWrapper>(".", constructorString);
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public virtual void Dispose()
        {
            innerFactory.Dispose();
        }
    }
}
