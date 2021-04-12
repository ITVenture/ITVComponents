using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.ParallelProcessing
{

    /// <summary>
    /// Enables a consumer to get a callback when this resource is released
    /// </summary>
    public class UnsafeLock:IDisposable

    {
        /// <summary>
        /// the release callback that is called when this object disposes
        /// </summary>
        private Action releaseCallback;

        /// <summary>
        /// Initializes a new instance of the UnsafeLock class
        /// </summary>
        /// <param name="releaseCallback"></param>
        public UnsafeLock(Action releaseCallback)
        {
            this.releaseCallback = releaseCallback;
        }

        /// <summary>
        ///   Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        public void Dispose()
        {
            releaseCallback?.Invoke();
            releaseCallback = null;
        }
    }
}
