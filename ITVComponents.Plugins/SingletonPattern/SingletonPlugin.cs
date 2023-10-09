using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.Plugins.SelfRegistration;

namespace ITVComponents.Plugins.SingletonPattern
{
    /*[Singleton]
    public class SingletonPlugin:IPlugin,IDeferredInit
    {
        private PluginFactory factory;

        private string constructor;

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        public IPlugin Instance { get; set; }

        /// <summary>
        /// Indicates whether this deferrable init-object is already initialized
        /// </summary>
        public bool Initialized { get; private set; }

        /// <summary>
        /// Indicates whether this Object requires immediate Initialization right after calling the constructor
        /// </summary>
        public bool ForceImmediateInitialization => false;

        public SingletonPlugin(PluginFactory factory, string constructor)
        {
            this.factory = factory;
            this.constructor = constructor;
        }

        /// <summary>
        /// Initializes this deferred initializable object
        /// </summary>
        public void Initialize()
        {
            if (!Initialized)
            {
                try
                {
                    Instance = factory.LoadPlugin<IPlugin>(UniqueName, constructor);
                }
                finally
                {
                    Initialized = true;
                }
            }
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }*/
}
