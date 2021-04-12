using System;

namespace ITVComponents
{
    /// <summary>
    /// Interface providing the possibility to inform a client about critical errors
    /// </summary>
    public interface ICriticalComponent
    {
        /// <summary>
        /// When raised, a supporting component can take appropriate actions to shutdown an application
        /// </summary>
        event CriticalErrorEventHandler CriticalError;
    }

    /// <summary>
    /// Provides event handling for critical exceptions ocurring in a class on which the proper execution of a program depends
    /// </summary>
    /// <param name="sender">the object that caused the malfunction</param>
    /// <param name="e">the arguments providing information about the ocurred error</param>
    public delegate void CriticalErrorEventHandler(object sender, CriticalErrorEventArgs e);

    /// <summary>
    /// Provides informations about a critical error in a component
    /// </summary>
    public class CriticalErrorEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the CriticalErrorEventArgs class
        /// </summary>
        /// <param name="criticalError">the exception that was caused by a component</param>
        public CriticalErrorEventArgs(ComponentException criticalError)
            : this()
        {
            Error = criticalError;
        }

        /// <summary>
        /// Prevents a default instance of the CriticalErrorEventArgs from being created
        /// </summary>
        private CriticalErrorEventArgs()
        {
        }

        /// <summary>
        /// Gets the error that was caused by the event-sending component
        /// </summary>
        public ComponentException Error { get; private set; }
    }
}
