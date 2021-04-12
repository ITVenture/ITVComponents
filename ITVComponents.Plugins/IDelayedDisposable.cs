// //---------------------------------------------------------------------------------------
// // <copyright file="IDelayedDisposable.cs" company="IT-Venture GmbH">
//      2009 by IT-Venture GmbH
// // </copyright>
// //---------------------------------------------------------------------------------------

using System;

namespace ITVComponents.Plugins
{
    /// <summary>
    /// Allows an object to delay its disposal when it is busy
    /// </summary>
    interface IDelayedDisposable : IDisposable
    {
        /// <summary>
        /// Tries to dispose the object and waits for the provided timeout
        /// </summary>
        /// <param name="timeout">the timeout to wait for the object to dispose</param>
        /// <returns>a value indicating whether the object could be disposed</returns>
        bool Dispose(int timeout);
    }
}
