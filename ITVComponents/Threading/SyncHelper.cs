using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace ITVComponents.Threading
{
    /// <summary>
    /// Contains Helper Extension methods that can be used to get consitent Access to an object
    /// </summary>
    public static class SyncHelper
    {
        /// <summary>
        /// Holds for all initialized threads the owner - object
        /// </summary>
        private static ThreadLocal<string> threadOwner;

        /// <summary>
        /// Holds for all initialized thrads a hashtable with configuration values
        /// </summary>
        private static ThreadLocal<Dictionary<string, object>> threadConfiguration;

        /// <summary>
        /// Initializes static members of the SyncHelper class
        /// </summary>
        static SyncHelper()
        {
            threadOwner = new ThreadLocal<string>();
            threadConfiguration = new ThreadLocal<Dictionary<string, object>>();
        }

        /// <summary>
        /// Gets the owner of the current Thread
        /// </summary>
        /// <param name="target">any object that lives in the current threads context</param>
        /// <returns>the owner name of the current thread</returns>
        public static string LocalOwner(this object target)
        {
            if (threadOwner.IsValueCreated && threadOwner.Value != null)
            {
                return threadOwner.Value;
            }

            return Thread.CurrentThread.ManagedThreadId.ToString();
        }

        /// <summary>
        /// Sets the owner of the current Thread
        /// </summary>
        /// <param name="target">any object that lives in the current threads context</param>
        /// <param name="newValue">the owner name of the current thread</param>
        public static void LocalOwner(this object target, string newValue)
        {
            threadOwner.Value = newValue;
        }

        /// <summary>
        /// Gets a configured value for the current Thread
        /// </summary>
        /// <typeparam name="T">the desired configuration type</typeparam>
        /// <param name="target">any object that lives in the current threads context</param>
        /// <param name="configurationName">the name of the desired configuration</param>
        /// <returns>the value of the requested configuration if it exists</returns>
        public static T LocalConfiguration<T>(this object target, string configurationName)
        {
            Dictionary<string, object> values = null;
            T retVal = default(T);
            if (threadConfiguration.IsValueCreated && threadConfiguration.Value != null)
            {
                values = threadConfiguration.Value;
            }

            if (values != null)
            {
                if (values.ContainsKey(configurationName) && values[configurationName] is T)
                {
                    retVal = (T) values[configurationName];
                }
            }

            return retVal;
        }

        /// <summary>
        /// Sets a local configuration of the current thread
        /// </summary>
        /// <typeparam name="T">the type of which the provided configuration is</typeparam>
        /// <param name="target">any object that lives in the current threads context</param>
        /// <param name="configurationName">the name of the configuration value that is stoerd</param>
        /// <param name="newValue">the stored configuration value</param>
        public static void LocalConfiguration<T>(this object target, string configurationName, T newValue)
        {
            Dictionary<string, object> values;
            if (threadConfiguration.IsValueCreated && threadConfiguration.Value != null)
            {
                values = threadConfiguration.Value;
            }
            else
            {
                values = new Dictionary<string, object>();
                threadConfiguration.Value = values;
            }

            values[configurationName] = newValue;
        }

        /// <summary>
        /// Clears the local configuration
        /// </summary>
        public static void ClearLocalConfiguration(this object target)
        {
            if (threadConfiguration.IsValueCreated && threadConfiguration.Value != null)
            {
                threadConfiguration.Value.Clear();
                threadConfiguration.Value = null;
            }
        }
    }
}
