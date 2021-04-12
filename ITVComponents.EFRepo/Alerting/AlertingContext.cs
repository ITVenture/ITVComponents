using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Logging;

namespace ITVComponents.EFRepo.Alerting
{
    /// <summary>
    /// Marker-Interface that enables an event-sending instance to provide additional information about the current environment
    /// </summary>
    public class AlertingContext
    {
        /// <summary>
        /// holds context values for the alerting context
        /// </summary>
        private Dictionary<string, object> contextValues = new Dictionary<string, object>();

        /// <summary>
        /// Sets an alerting context value indicated by a name
        /// </summary>
        /// <typeparam name="T">the target type that the object is to be of</typeparam>
        /// <param name="name">the name of the context-variable</param>
        /// <param name="value">the value of the context-variable</param>
        public void SetAlertingContextVariable<T>(string name, T value)
        {
            if (contextValues.ContainsKey(name))
            {
                object tmp = contextValues[name];
                if (tmp is T)
                {
                    contextValues[name] = value;
                }
                else
                {
                    LogEnvironment.LogDebugEvent(null, $"Suppressed the re-set of the value '{name}' to a new value of a different type.", (int) LogSeverity.Warning, null);
                }
            }
            else
            {
                contextValues.Add(name, value);
            }
        }

        /// <summary>
        /// Gets the value of the given context-variable
        /// </summary>
        /// <typeparam name="T">the expected type of the context-variables value</typeparam>
        /// <param name="name">the name of the context-variable</param>
        /// <returns>the value of the requested context-variable</returns>
        public T GetAlertingContextVariable<T>(string name)
        {
            if (contextValues.ContainsKey(name))
            {
                object tmp = contextValues[name];
                if (tmp is T ret)
                {
                    return ret;
                }
            }

            return default(T);
        }
    }
}
