using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.Threading
{
    /// <summary>
    /// Use this class if you use a component, that performs some initialization tasks on its first usage, which alre not thread-save.
    /// </summary>
    public static class FirstRunInitializationHelper
    {
        /// <summary>
        /// Holds components that may require initialization
        /// </summary>
        private static ConcurrentDictionary<string, ManualResetEvent> initializedComponents = new ConcurrentDictionary<string, ManualResetEvent>();

        /// <summary>
        /// Holds objects to ensure, that the initialization is performed exclusively
        /// </summary>
        private static ConcurrentDictionary<string, object> initializingComponents = new ConcurrentDictionary<string, object>();

        /// <summary>
        /// Makes sure that the first time the provided action is performed, it runs exclusively. After the first time, the other runs are performed simultaneously.
        /// </summary>
        /// <param name="componentToInitialize">the name of the component that is being initialized</param>
        /// <param name="actionToPerform">the action that is performed and will lead to an implicit initialization of the component</param>
        public static void WhenInitialized(string componentToInitialize, Action actionToPerform)
        {
            var handle = initializedComponents.GetOrAdd(componentToInitialize, s => new ManualResetEvent(false));
            if (!handle.WaitOne(500))
            {
                var lk = initializingComponents.GetOrAdd(componentToInitialize, s => new object());
                if (AsyncMonitor.TryEnter(lk, 500))
                {
                    try
                    {
                        actionToPerform();
                        handle.Set();
                        return;
                    }
                    finally
                    {
                        AsyncMonitor.Exit(lk);
                    }
                }

                handle.WaitOne();
            }

            actionToPerform();
        }
    }
}
