using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace ITVComponents.Plugins.SingletonPattern
{
    /*public static class SingletonEnvironment
    {
        /// <summary>
        /// Holds the singletonFactory object
        /// </summary>
        private static PluginFactory singletonFactory;

        /// <summary>
        /// Indicates whether the SingletonEnvironment is currently initialized
        /// </summary>
        private static bool initialized = false;

        /// <summary>
        /// Holds a counter that holds the current number of active factories for this instance
        /// </summary>
        private static int activeFactories = 0;

        /// <summary>
        /// holds a monitor object that prevents simultaneous manipulation of critical local objects
        /// </summary>
        private static object initializationLock = new object();

        /// <summary>
        /// holds local requests of external factories 
        /// </summary>
        private static ThreadLocal<UnknownConstructorParameterEventHandler> requestObjOnLoad = new ThreadLocal<UnknownConstructorParameterEventHandler>();

        /// <summary>
        /// Gets an initialized singleton plugin
        /// </summary>
        /// <typeparam name="T">the Type of the desired object</typeparam>
        /// <param name="uniqueName">the uniqueName of the desired object</param>
        /// <returns>the requested plugin if it is initialized or null, if not</returns>
        public static T GetSingletonPlugin<T>(string uniqueName) where T : class, IPlugin 
        {
            lock (initializationLock)
            {
                if (initialized)
                {
                    return singletonFactory[uniqueName] as T;
                }

                return null;
            }
        }

        /// <summary>
        /// Is called after every Initialization of a PluginFactory.
        /// </summary>
        internal static void FactoryInitializing()
        {
            lock (initializationLock)
            {
                activeFactories++;
            }
        }

        /// <summary>
        /// Is Called after every Dispose of a PluginFactory. When the last Factory is disposed, the Singleton factory is also being disposed
        /// </summary>
        internal static void FactoryDisposed()
        {
            lock (initializationLock)
            {
                activeFactories--;
                if (activeFactories == 0 && initialized)
                {
                    singletonFactory.Dispose();
                    singletonFactory = null;
                    initialized = false;
                }
            }
        }

        /// <summary>
        /// Finds a singleton Plugin with the given name
        /// </summary>
        /// <param name="uniqueName">the unique name that was requested by a plugin-Constructor in a loaded factory</param>
        /// <returns>the desired plugin, if it is present</returns>
        internal static bool FindSingletonPlugin(string uniqueName, out object value)
        {
            value = null;
            if (initialized)
            {
                IPlugin retVal = singletonFactory[uniqueName];
                value = retVal;
                return value != null;
            }

            return false;
        }

        /// <summary>
        /// Initializes a singleton plugin
        /// </summary>
        /// <param name="uniqueName">the unique name of the plugin that is being initialized</param>
        /// <param name="constructorString">the constructor of the demanded plugin</param>
        /// <param name="buffer">indicates whether to buffer the requested item</param>
        /// <param name="unknownParameterCallback">a callback that is used to resolve unkown constructor arguments</param>
        /// <returns>a value indicating whether to buffer the item on the local factory or not</returns>
        internal static IPlugin InitializeSingletonPlugin(string uniqueName, string constructorString, bool buffer, UnknownConstructorParameterEventHandler unknownParameterCallback)
        {
            CheckInitialized();
            lock (initializationLock)
            {
                bool registered = RegisterLocalCallback(unknownParameterCallback);
                try
                {
                    return singletonFactory.LoadPlugin<IPlugin>(uniqueName, constructorString, buffer);
                }
                finally
                {
                    if (registered)
                    {
                        UnregisterLocalCallback();
                    }
                }
            }
        }

        /// <summary>
        /// Checks whether the factory is currently initialized
        /// </summary>
        private static void CheckInitialized()
        {
            lock (initializationLock)
            {
                if (!initialized)
                {
                    singletonFactory = new PluginFactory(true, true, false, false);
                    singletonFactory.AllowFactoryParameter = true;
                    singletonFactory.UnknownConstructorParameter += UnknownPluginRequest;
                    initialized = true;
                }
            }
        }

        /// <summary>
        /// Forwards value requests of the singleton factory if required
        /// </summary>
        /// <param name="sender">the pluginfactory that is trying to load a plugin</param>
        /// <param name="e">the arguments of the calling factory</param>
        private static void UnknownPluginRequest(object sender, UnknownConstructorParameterEventArgs e)
        {
            if (requestObjOnLoad.IsValueCreated)
            {
                requestObjOnLoad.Value?.Invoke(sender, e);
            }
        }

        /// <summary>
        /// Unregisters the local RegistrationCallbacks
        /// </summary>
        private static void UnregisterLocalCallback()
        {
            requestObjOnLoad.Value = null;
        }

        /// <summary>
        /// Registers a local RegistrationCallback
        /// </summary>
        /// <param name="unknownParameterCallback">the callback to use fo local object-requests</param>
        /// <returns>a value indicating whether the callback was registered or ignored due to an already registered callback</returns>
        private static bool RegisterLocalCallback(UnknownConstructorParameterEventHandler unknownParameterCallback)
        {
            if (!requestObjOnLoad.IsValueCreated || requestObjOnLoad.Value == null)
            {
                requestObjOnLoad.Value = unknownParameterCallback;
                return true;
            }

            return false;
        }
    }*/
}
