﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImpromptuInterface;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.InterProcessCommunication.Shared.Proxying;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.Scripting.CScript.ReflectionHelpers;
using ITVComponents.Security;
using ITVComponents.Threading;

namespace ITVComponents.InterProcessCommunication.Shared.Base
{
    public abstract class BaseClient:IBidirectionalClient, IDeferredInit
    {
        /// <summary>
        /// a reconnection timeout after which the client tries to re-establish the connection to the server
        /// </summary>
        private const int ReconnectLooptime = 2000;

        /// <summary>
        /// indicates whether this client has been disposed
        /// </summary>
        private bool disposed = false;

        /// <summary>
        /// indicates whether the connection to the remote service is currently established
        /// </summary>
        private bool connectionStatus = false;

        /// <summary>
        /// Prevents the client object from multiple attempts of reconnecting to the server
        /// </summary>
        private object reconnectLock;

        /// <summary>
        /// a timer object that is used to reconnect to the target service when the connection is lost
        /// </summary>
        private Timer reconnector;

        /// <summary>
        /// indicates whether the reconnection timer is currently active
        /// </summary>
        private bool reconnectorActive;

        /// <summary>
        /// A Locker-instance that allows synchronous manipulation of this object
        /// </summary>
        private object lockHelper;

        /// <summary>
        /// Makes sure, that the test/reconnect routine is not being executed parallel
        /// </summary>
        private object testingLock = new object();

        /// <summary>
        /// a dictionary containing all events that are required to subscribe
        /// </summary>
        private ConcurrentDictionary<string, List<Delegate>> eventSubscriptions = new ConcurrentDictionary<string, List<Delegate>>();

        /*/// <summary>
        /// indicates whether this connection is managed by a different object that will check periodically for reconnecting
        /// </summary>
        private bool managedConnection;*/

        /// <summary>
        /// Holds a list of proxies that can and must be abandonned, before this client can be properly disposed
        /// </summary>
        private ConcurrentDictionary<string, IObjectProxy> abandonnableProxies = new ConcurrentDictionary<string, IObjectProxy>();

        /// <summary>
        /// Holds occurred exception if told to do so
        /// </summary>
        private List<SerializedException> exceptionCollector = new List<SerializedException>();

        /// <summary>
        /// Object used to avoid Disposal of poll timer while poll is in progress
        /// </summary>
        private object reconnectingLock = new object();

        /// <summary>
        /// the ownerName for all threads generated by this object
        /// </summary>
        private string threadsOwner;


        /// <summary>
        /// Initializes a default instance of the BaseClient class
        /// </summary>
        protected BaseClient(string target)
        {
            reconnectLock = new object();
            reconnector = new Timer(PollConnection);
            lockHelper = new object();
            threadsOwner = $"::{Guid.NewGuid()}::";
            Target = target;
        }

        public string Target { get; }

        /// <summary>
        /// Gets a value indicating whether this object is operational
        /// </summary>
        public bool Operational { get { return connectionStatus; } }

        /// <summary>
        /// Gets an object that provides sync - capabilities for this object
        /// </summary>
        public object Sync { get { return lockHelper; } }

        /// <summary>
        /// Indicates whethe r this deferrable init-object is already initialized
        /// </summary>
        public bool Initialized { get; private set; }

        /// <summary>
        /// Indicates whether this Object requires immediate Initialization right after calling the constructor
        /// </summary>
        public bool ForceImmediateInitialization => false;

        /// <summary>
        /// Gets or sets a value indicating whether exceptions should be collected on occurrence
        /// </summary>
        public bool CollectExceptions { get; set; }

        /// <summary>
        /// Gets a value indicating whether this client is operated bi-directional
        /// </summary>
        public abstract bool IsBidirectional { get; }

        /// <summary>
        /// Creates a proxy object for the requested objectName
        /// </summary>
        /// <typeparam name="T">the Client-interface type for the given objectName</typeparam>
        /// <param name="objectName">the name of the remote-Object to wrap</param>
        /// <returns>a Proxy - object that wraps the requested object</returns>
        public T CreateProxy<T>(string objectName) where T : class
        {
            ObjectAvailabilityResult result;
                try
                {
                    result =  CheckRemoteObjectAvailability(objectName);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogDebugEvent(
                        null, $"Error checking Object-Availability:",
                        (int)LogSeverity.Error, null);
                    LogException(ex, true);
                    throw;
                }

                if (!result.Available)
                {
                    throw new InvalidOperationException(
                        $"The requested object is not present on the connected service. Server-Message: {result.Message}");
                }

                Type t = typeof(T);
                LogEnvironment.LogDebugEvent(null, $"Creating proxy for Type: {t.FullName}", (int)LogSeverity.Report, null);
                if (!CustomProxy.IsProxyAvailable(typeof(T)))
                {
                    ObjectProxy proxy = CreateProxyInternal(objectName, typeof(T));
                    LogEnvironment.LogDebugEvent(null, "Proxy successfully created", (int) LogSeverity.Report, null);
                    return proxy.ActLike<T>(typeof(IObjectProxy));
                }

                return CustomProxy.GetCustomProxy<T>(this, objectName);
        }

        /// <summary>
        /// Checks on the service whether a specific object exists and is ready to be used
        /// </summary>
        /// <param name="uniqueObjectName">the unique object name of the demanded object</param>
        /// <returns>a value indicating whether the remote service has committed the existance of the demanded object</returns>
        public ObjectAvailabilityResult CheckRemoteObjectAvailability(string uniqueObjectName)
        {
            if (ValidateConnection())
            {
                return CheckForAvailableProxy(uniqueObjectName);
            }

            throw new InterProcessException("Currently offline!", null);
        }

        /// <summary>
        /// Removes an object on the Server for the given objectName. Works only for extended Proxy-objects
        /// </summary>
        /// <param name="uniqueObjectName">the objectName to remove from the list of extension.proxies</param>
        /// <returns>a value indicating whether the object could be removed successfully</returns>
        public bool AbandonObject(string uniqueObjectName)
        {
            if (!disposed)
            {
                if (ValidateConnection())
                {
                    return AbandonExtendedProxy(uniqueObjectName) && abandonnableProxies.TryRemove(uniqueObjectName, out _);
                }

                throw new InterProcessException("Currently offline!", null);
            }

            return true;
        }

        /// <summary>
        /// Calls a remote method on the server
        /// </summary>
        /// <param name="objectName">the name of the executing object</param>
        /// <param name="methodName">the method that must be executed</param>
        /// <param name="arguments">arguments for the method call</param>
        /// <returns>a call-wrapper that will be triggered back, when the execution has finished</returns>
        public object CallRemoteMethod(string objectName, string methodName, object[] arguments)
        {
            if (ValidateConnection())
            {
                BuildArguments(arguments);
                var result = ExecuteMethod(objectName, methodName, arguments);
                ReverseArguments(result.Parameters, arguments);
                return ReverseObject(result.Result);
            }

            throw new InterProcessException("Currently offline!", null);
        }

        /// <summary>
        /// Calls a remote method on the server
        /// </summary>
        /// <param name="objectName">the name of the executing object</param>
        /// <param name="methodName">the method that must be executed</param>
        /// <param name="arguments">arguments for the method call</param>
        /// <returns>a call-wrapper that will be triggered back, when the execution has finished</returns>
        public async Task<object> CallRemoteMethodAsync(string objectName, string methodName, object[] arguments)
        {
            if (ValidateConnection())
            {
                BuildArguments(arguments);
                var result = await ExecuteMethodAsync(objectName, methodName, arguments);
                ReverseArguments(result.Parameters, arguments);
                return ReverseObject(result.Result);
            }

            throw new InterProcessException("Currently offline!", null);
        }

        /// <summary>
        /// Gets a property of the targetobject with the provided name
        /// </summary>
        /// <param name="objectName">the target object from which to get a property</param>
        /// <param name="propertyName">the name of the property to read</param>
        /// <param name="index">the index used for indexed properties</param>
        /// <returns>the value of the requested property</returns>
        public object GetProperty(string objectName, string propertyName, object[] index)
        {
            if (ValidateConnection())
            {
                BuildArguments(index);
                return ReverseObject(GetPropertyValue(objectName, propertyName, index));
            }

            throw new InterProcessException("Currently offline!", null);
        }

        /// <summary>
        /// Sets the value of a property on a target object
        /// </summary>
        /// <param name="objectName">the target object on which to set a property</param>
        /// <param name="propertyName">the propertyname to set</param>
        /// <param name="index">the index used for indexed properties</param>
        /// <param name="value">the new value for the specified property</param>
        public void SetProperty(string objectName, string propertyName, object[] index, object value)
        {
            if (ValidateConnection())
            {
                BuildArguments(index);
                SetPropertyValue(objectName, propertyName, index, PrepareProxyRequest(value));
            }
            else
            {
                throw new InterProcessException("Currently offline!", null);
            }
        }

        /// <summary>
        /// Validates the server connection
        /// </summary>
        /// <returns>a value indicating whether the connection to the service object is up and running</returns>
        public bool ValidateConnection()
        {
            bool success = false;
            try
            {
                success = TestConnection();
            }
            catch (Exception ex)
            {
                LogException(ex, false);

            }

            return success;
        }

        /// <summary>
        /// Subscribes for a specific event
        /// </summary>
        /// <param name="objectName">the name of the event-providing object</param>
        /// <param name="eventName">the name of the event</param>
        /// <param name="handler">the handler to be added to the list</param>
        public void SubscribeEvent(string objectName, string eventName, Delegate handler)
        {
            string fullEventName = $"{objectName}_{eventName}";
            var l = eventSubscriptions.GetOrAdd(fullEventName, n =>
            {
                RegisterServerEvent(objectName, eventName);
                return new List<Delegate>();
            });
            lock (l)
            {
                l.Add(handler);
            }
        }

        /// <summary>
        /// Removes a specific handler from the subscription list
        /// </summary>
        /// <param name="objectName">the name of the event-providing object</param>
        /// <param name="eventName">the name of the event</param>
        /// <param name="handler">the handler to be removed from the subscription list</param>
        public void UnSubscribeEvent(string objectName, string eventName, Delegate handler)
        {
            string fullEventName = $"{objectName}_{eventName}";
            if (eventSubscriptions.TryGetValue(fullEventName, out var l))
            {
                lock (l)
                {
                    if (l.Contains(handler))
                    {
                        l.Remove(handler);
                    }

                    if (l.Count == 0)
                    {
                        UnRegisterServerEvent(objectName, eventName);
                    }
                }
            }
        }

        /// <summary>
        /// Removes all Event Subscriptions from the serverobject
        /// </summary>
        /// <param name="objectName">the objectname from which to remove all subscriptions</param>
        public void RemoveAllSubscriptions(string objectName)
        {
            var keys = eventSubscriptions.Keys.Where(n => n.StartsWith($"{objectName}_")).ToArray();
            foreach (var t in keys)
            {
                if (eventSubscriptions.TryRemove(t, out var l))
                {
                    l.Clear();
                    var eventName = t.Substring(objectName.Length + 2);
                    UnRegisterServerEvent(objectName, eventName);
                }
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                AbandonAllObjects();
                lock (reconnectingLock)
                {
                    reconnector.Dispose();
                }

                Dispose(true);
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        protected virtual void Dispose(bool disposing)
        {
        }

        /// <summary>
        /// Informs sub-classes of the BaseClient class that the remote service has been re-connected
        /// </summary>
        protected virtual void OnReconnected()
        {
            if (eventSubscriptions.Count != 0)
            {
                (from t in eventSubscriptions.Keys
                        select
                            new
                            {
                                ObjectName = t.Substring(0, t.LastIndexOf("_")),
                                EventName = t.Substring(t.LastIndexOf("_") + 1)
                            }).ToList()
                    .ForEach(
                        n => RegisterServerEvent(n.ObjectName, n.EventName));
            }
        }

        /// <summary>
        /// Raises the Disconnected event
        /// </summary>
        protected virtual void OnOperationalChanged()
        {
            OperationalChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Informs this object about a remote event that has ocurred
        /// </summary>
        /// <param name="eventName">the eventname that was raised</param>
        /// <param name="arguments">the arguments for the specified event</param>
        protected bool RaiseEvent(string eventName, object[] arguments)
        {
            lock (Sync)
            {
                eventName.LocalOwner(threadsOwner);
                try
                {
                    ReverseArguments(arguments, arguments);
                    bool retVal = false;
                    if (eventSubscriptions.ContainsKey(eventName))
                    {
                        Delegate[] subscribers = eventSubscriptions[eventName].ToArray();
                        foreach (Delegate dlg in subscribers)
                        {
                            retVal = true;
                            try
                            {
                                dlg.DynamicInvoke(arguments);
                            }
                            catch (Exception ex)
                            {
                                LogEnvironment.LogEvent(ex.ToString(), LogSeverity.Error);
                            }
                        }
                    }

                    return retVal;
                }
                finally
                {
                    PrepareProxyRequest(arguments);
                    eventName.LocalOwner(null);
                }
            }
        }

        /// <summary>
        /// Creates an objectProxy for the specified remote object name
        /// </summary>
        /// <param name="objectName">the name of the remote object</param>
        /// <param name="type">the target type to implement</param>
        /// <returns>a proxy object fo the provided objectName</returns>
        internal virtual ObjectProxy CreateProxyInternal(string objectName, Type type)
        {
            if (!IsBidirectional)
                return new ObjectProxy(this, objectName, type);

            return new BidirectionalObjectProxy(this, objectName, type);
        }

       /// <summary>
        /// Implements a method to check on the remote object whether a specific object is available
        /// </summary>
        /// <param name="uniqueName">the unique name of the desired object</param>
        /// <returns>an object that provides information about the proxy-availability</returns>
        protected abstract ObjectAvailabilityResult CheckForAvailableProxy(string uniqueName);

        /// <summary>
        /// Implements a method to abandon an extended proxy-object that was created by a separate client-call before
        /// </summary>
        /// <param name="uniqueName">the unique name of the proxy that needs to be released</param>
        /// <returns>a value indicating whether the release of the object was successful</returns>
        protected abstract bool AbandonExtendedProxy(string uniqueName);

        /// <summary>
        /// Implements a method to get the value of the provided property from the target-object
        /// </summary>
        /// <param name="uniqueName">the name of the target-object in the remote process</param>
        /// <param name="propertyName">the name of the property</param>
        /// <param name="index">an index that is provided to request the value of the index</param>
        /// <returns>the value of the requested property on the target-object</returns>
        protected abstract object GetPropertyValue(string uniqueName, string propertyName, object[] index);


        /// <summary>
        /// Implements a method to get the value of the provided property from the target-object
        /// </summary>
        /// <param name="uniqueName">the name of the target-object in the remote process</param>
        /// <param name="propertyName">the name of the property</param>
        /// <param name="index">an index that is provided to request the value of the index</param>
        /// <param name="value">the value to set on the target-property</param>
        /// <returns>the value of the requested property on the target-object</returns>
        protected abstract void SetPropertyValue(string uniqueName, string propertyName, object[] index, object value); 

        /// <summary>
        /// Implements a method to request the execution of the demanded method with the given arguments on the remote process
        /// </summary>
        /// <param name="uniqueName">the name of the object on which the method must be executed</param>
        /// <param name="methodName">the name of the method to execute</param>
        /// <param name="arguments">the methods arguments</param>
        /// <returns>the result of the executed method</returns>
        protected abstract ExecutionResult ExecuteMethod(string uniqueName, string methodName, object[] arguments);

        /// <summary>
        /// Implements a method to request the execution of the demanded method with the given arguments on the remote process
        /// </summary>
        /// <param name="uniqueName">the name of the object on which the method must be executed</param>
        /// <param name="methodName">the name of the method to execute</param>
        /// <param name="arguments">the methods arguments</param>
        /// <returns>the result of the executed method</returns>
        protected abstract Task<ExecutionResult> ExecuteMethodAsync(string uniqueName, string methodName, object[] arguments);

        /// <summary>
        /// Registers a specific event-subscription on the server
        /// </summary>
        /// <param name="uniqueName">the unique-name of the target-object</param>
        /// <param name="eventName">the event on the target-object to subscribe to</param>
        protected abstract void RegisterServerEvent(string uniqueName, string eventName);

        /// <summary>
        /// removes a specific event-subscription on the server
        /// </summary>
        /// <param name="uniqueName">the unique-name of the target-object</param>
        /// <param name="eventName">the event on the target-object to remove the subscription for</param>
        protected abstract void UnRegisterServerEvent(string uniqueName, string eventName);

        /// <summary>
        /// Tests the connection to the given target-proxy object
        /// </summary>
        /// <returns>a value indicating whether the connection is OK</returns>
        protected abstract bool Test();

        private bool AbandonAllObjects()
        {
            var proxyNames = abandonnableProxies.Keys.ToArray();
            var success = proxyNames.Select(n => AbandonObject(n)).ToArray();
            return success.All(n => n);
        }

        private void BuildArguments(object[] arguments)
        {
            for (int i = 0; i < (arguments?.Length??0); i++)
            {
                arguments[i] = PrepareProxyRequest(arguments[i]);
            }
        }

        private object PrepareProxyRequest(object obj)
        {
            object retVal = obj;
            var packed = obj as TypedParam;
            var proxy = (packed?.GetValue(null)??obj) as IObjectProxy;

            if (proxy != null)
            {
                retVal = new ProxyResult {UniqueName = proxy.ObjectName};
            }

            return retVal;
        }

        private void ReverseArguments(object[] arguments, object[] originalArgs)
        {
            for (int i = 0; i < (arguments?.Length??0); i++)
            {
                originalArgs[i] = ReverseObject(arguments[i]);
            }
        }

        private object ReverseObject(object src)
        {
            object retVal = src;
            ProxyResult prs = src as ProxyResult;
            if (prs != null)
            {
                var fx = new Func<string, IObjectProxy>(s =>
                {
                    IObjectProxy retObj = null;
                    if (!CustomProxy.IsProxyAvailable(prs.Type))
                    {
                        ObjectProxy proxy = CreateProxyInternal(s, prs.Type);
                        retObj = proxy.ActLike<IObjectProxy>(prs.Type);
                    }
                    else
                    {
                        retObj = CustomProxy.GetCustomProxy(prs.Type, this, s);
                    }

                    return retObj;
                });

                retVal = abandonnableProxies.AddOrUpdate(prs.UniqueName, fx, (s, o) => o ?? fx(s));

            }

            return retVal;
        }

        /// <summary>
        /// Tests the connection and establishes a new connection if the connection is faulted
        /// </summary>
        /// <returns>a value indicating whether the connection is running correctly</returns>
        private bool TestConnection()
        {
            lock (testingLock)
            {
                if (!disposed)
                {
                    var tmpConn = connectionStatus;
                    try
                    {
                        connectionStatus = Test();
                        if (tmpConn != connectionStatus)
                        {
                            //tmpConn = connectionStatus;
                            OnOperationalChanged();
                            if (connectionStatus)
                            {
                                OnReconnected();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!tmpConn && connectionStatus)
                        {
                            connectionStatus = false;
                            OnOperationalChanged();
                        }
                    }

                    if (!connectionStatus)
                    {
                        if (!reconnectorActive)
                        {
                            reconnector.Change(ReconnectLooptime, ReconnectLooptime);
                            reconnectorActive = true;
                        }
                    }

                    return connectionStatus;
                }

                return false;
            }
        }

        /// <summary>
        /// polls the connection to the server
        /// </summary>
        /// <param name="state">**ignored parameter**</param>
        private void PollConnection(object state)
        {
            lock (reconnector)
            {
                reconnector.Change(Timeout.Infinite, Timeout.Infinite);
                reconnectorActive = false;
            }

            try
            {
                lock (reconnectingLock)
                {
                    if (!disposed)
                    {
                        TestConnection();
                    }
                }
            }
            finally
            {
                if (!disposed)
                {
                    reconnector.Change(ReconnectLooptime, ReconnectLooptime);
                }
            }
        }

        /// <summary>
        /// Tries to reconnect to the remote service
        /// </summary>
        /// <returns>a value indicating whether the re-connection was successful</returns>
        private bool TryReconnect()
        {
            try
            {
                if (!connectionStatus)
                {
                    return TestConnection();
                }

                return connectionStatus;
            }
            catch (Exception ex)
            {
                LogException(ex, false);
                return false;
            }
        }

        /// <summary>
        /// Initializes this Remote-Client
        /// </summary>
        public void Initialize()
        {
            if (!Initialized)
            {
                try
                {
                    lock (reconnectLock)
                    {
                        connectionStatus = TryReconnect();
                        if (IsBidirectional)
                        {
                            reconnector.Change(ReconnectLooptime, ReconnectLooptime);
                        }
                    }
                }
                finally
                {
                    Initialized = true;
                }
            }
        }

        public SerializedException[] PeekExceptions()
        {
            return exceptionCollector.ToArray();
        }

        public SerializedException[] FlushExceptions()
        {
            try
            {
                return PeekExceptions();
            }
            finally
            {
                exceptionCollector.Clear();
            }
        }

        protected void LogException(SerializedException ex, bool debug)
        {
            if (CollectExceptions)
            {
                exceptionCollector.Add(ex);
            }

            if (debug)
            {
                LogEnvironment.LogDebugEvent(null, ex.ToString(), (int) LogSeverity.Error, null);
            }
            else
            {
                LogEnvironment.LogEvent(ex.ToString(), LogSeverity.Error);
            }
        }

        [field: NonSerialized]
        public event EventHandler OperationalChanged;
    }
}
