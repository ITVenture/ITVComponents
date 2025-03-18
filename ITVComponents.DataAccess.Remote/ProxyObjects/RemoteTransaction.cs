using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.DataAccess.Remote.RemoteInterface;
using ITVComponents.Threading;

namespace ITVComponents.DataAccess.Remote.ProxyObjects
{
    public class RemoteTransaction:ITransaction
    {
        /// <summary>
        /// The unique id of this object
        /// </summary>
        private long objectId;

        /// <summary>
        /// the proxy that is used to control the remote object
        /// </summary>
        private IRemoteProxyDefinition proxy;

        /// <summary>
        /// Initializes a new instance of the RemoteTransaction class
        /// </summary>
        /// <param name="proxy">the proxy object that enables thsi object to control the original object on the server</param>
        /// <param name="objectId">the id of the original object</param>
        public RemoteTransaction(IRemoteProxyDefinition proxy, long objectId)
        {
            this.objectId = objectId;
            this.proxy = proxy;
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            proxy.DestroyTransaction(objectId);
            OnDisposed();
        }

        /// <summary>
        /// Gets the inner lock of this Resource Lock instance
        /// </summary>
        public IResourceLock InnerLock { get { return null; } }

        public void Exclusive(bool autoLock, Action action)
        {
            action();
        }

        public T Exclusive<T>(bool autoLock, Func<T> action)
        {
            return action();
        }

        public void SynchronizeContext()
        {
            InnerLock?.SynchronizeContext();
        }

        public void LeaveSynchronizeContext()
        {
            InnerLock?.LeaveSynchronizeContext();
        }


        public IDisposable PauseExclusive()
        {
            return new ExclusivePauseHelper(()=>InnerLock?.PauseExclusive());
        }

        public bool RollbackAtEnd
        {
            get { return proxy.GetTransactionRollbackState(objectId); }
            set { proxy.SetTransactionRollbackState(objectId, value); }
        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            EventHandler handler = Disposed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        /// <summary>
        /// Provides an event informing a client class that the transaction has ended
        /// </summary>
        public event EventHandler Disposed;
    }
}
