using System;
using System.Data;
using System.Threading;
using ITVComponents.Threading;

namespace ITVComponents.DataAccess
{
    /// <summary>
    /// Describes a primitive implementation of the ITransaction interface
    /// </summary>
    public class Transaction : ITransaction
    {
        /// <summary>
        /// the transaction that is used to isolate the current database modifications
        /// </summary>
        private IDbTransaction transaction;

        /// <summary>
        /// an object that can be used by the monitor for locking access of specific resources
        /// </summary>
        private object synchronizedObject;

        /// <summary>
        /// Initializes a new instance of the Transaction class
        /// </summary>
        /// <param name="value">the inner value of this transaction</param>
        /// <param name="synchronizedObject">an object that can be used by the monitor for locking access of specific resources</param>
        /// <param name="innerLock">an inner lock object that can be used to lock and free objects cascaded</param>
        public Transaction(IDbTransaction value, object synchronizedObject, IResourceLock innerLock)
            : this(value, synchronizedObject)
        {
            this.InnerLock = innerLock;
        }

        /// <summary>
        /// Initializes a new instance of the Transaction class
        /// </summary>
        /// <param name="value">the inner value of this transaction</param>
        /// <param name="synchronizedObject">an object that can be used by the monitor for locking access of specific resources</param>
        public Transaction(IDbTransaction value, object synchronizedObject)
            : this()
        {
            AsyncMonitor.Enter(synchronizedObject);
            this.synchronizedObject = synchronizedObject;
            transaction = value;
        }

        /// <summary>
        /// Prevents a default instance of the Transaction class from being created
        /// </summary>
        private Transaction()
        {
        }

        /// <summary>
        /// Gets or sets a value indicating whether to rollback this transaction at the end of its lifetime
        /// </summary>
        public bool RollbackAtEnd
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the inner lock of this Resource lock instance
        /// </summary>
        public IResourceLock InnerLock { get; private set; }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            if (AsyncMonitor.IsEntered(synchronizedObject))
            try
            {
                if (transaction != null)
                {
                    try
                    {
                        if (!RollbackAtEnd)
                        {
                            transaction.Commit();
                        }
                        else
                        {
                            transaction.Rollback();
                        }
                    }
                    finally
                    {
                        transaction = null;
                    }

                    Disposed?.Invoke(this, EventArgs.Empty);
                    if (InnerLock != null)
                    {
                        InnerLock.Dispose();
                        InnerLock = null;
                    }
                }
            }
            finally
            {
                AsyncMonitor.Exit(synchronizedObject);
                synchronizedObject = null;
            }
        }

        /// <summary>
        /// Provides an event informing a client class that the transaction has ended
        /// </summary>
        [field: NonSerialized]
        public event EventHandler Disposed;
    }
}
