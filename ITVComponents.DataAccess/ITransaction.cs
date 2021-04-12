using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using ITVComponents.Threading;

namespace ITVComponents.DataAccess
{
    /// <summary>
    /// Interface describing a database transaction
    /// </summary>
    public interface ITransaction : IResourceLock
    {
        /// <summary>
        /// Gets or sets a value indicating whether the given transaction is supposed to rollback when this object disposes
        /// </summary>
        bool RollbackAtEnd { get; set; }

        /// <summary>
        /// Provides an event informing a client class that the transaction has ended
        /// </summary>
        event EventHandler Disposed;
    }
}
