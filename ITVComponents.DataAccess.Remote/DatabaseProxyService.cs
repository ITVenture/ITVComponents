using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using ITVComponents.DataAccess.Parallel;
using ITVComponents.DataAccess.Remote.ProxyObjects;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.Threading;

namespace ITVComponents.DataAccess.Remote
{
    public class DatabaseProxyService
    {
        /// <summary>
        /// holds all database sessions
        /// </summary>
        private IPluginFactory factory;

        /// <summary>
        /// the name of the connector plugin that is provided to the client by this proxy-instance
        /// </summary>
        private string connectorName;

        /// <summary>
        /// holds all open connections
        /// </summary>
        private Dictionary<long, ConnectionHandle> connections = new Dictionary<long, ConnectionHandle>();

        /// <summary>
        /// Holds all created parameters by sessions in the remote client
        /// </summary>
        private Dictionary<long, IDbDataParameter> parameters = new Dictionary<long, IDbDataParameter>();

        /// <summary>
        /// Holds all open transactions of this service
        /// </summary>
        private Dictionary<long, ITransaction> transactions = new Dictionary<long, ITransaction>(); 

        /// <summary>
        /// the next object id that will be created
        /// </summary>
        private long nextObjectId = DateTime.Now.Ticks;

        /// <summary>
        /// Ensures that only one object id is leased at a time
        /// </summary>
        private object objectIdLock = new object();
 
        /// <summary>
        /// Initializes a new instance of the DatabaseProxyService class
        /// </summary>
        /// <param name="connectionProvider">the connectionProvider that is used to access the database</param>
        public DatabaseProxyService(IPluginFactory factory, string connectorName)
        {
            this.factory= factory;
            this.connectorName= connectorName;
        }

        /// <summary>
        /// Acquires a Connection and returns the session handle
        /// </summary>
        /// <returns>the session-handle for the created connection</returns>
        public long AcquireConnection()
        {
            IDbWrapper conn = (IDbWrapper)factory[connectorName];
            long objectId = NextObjectId();
            lock (connections)
            {
                connections.Add(objectId,
                                new ConnectionHandle {Connection = conn});
            }

            return objectId;
        }

        /// <summary>
        /// Releases the provided connection
        /// </summary>
        /// <param name="sessionId">the session-id for which to release all open objects</param>
        public void ReleaseConnection(long sessionId)
        {
            ConnectionHandle handle;
            lock (connections)
            {
                handle = connections[sessionId];
                connections.Remove(sessionId);
            }

            handle.Connection.Dispose();
        }

        /// <summary>
        /// Gets the Value of a specific parameter that was created on this service
        /// </summary>
        /// <param name="objectId">the id of the service object</param>
        /// <returns>an instance of a Parameter-object</returns>
        public object GetParameterValue(long objectId)
        {
            object retVal = null;
            lock (parameters)
            {
                if (parameters.ContainsKey(objectId))
                {
                    retVal = parameters[objectId].Value;
                }
            }

            return retVal;
        }

        /// <summary>
        /// Destroys a specific parameter on the server
        /// </summary>
        /// <param name="objectId">the parameter that needs to be destroyed</param>
        public void DestroyParameter(long objectId)
        {
            lock (parameters)
            {
                if (parameters.ContainsKey(objectId))
                {
                    parameters.Remove(objectId);
                }
            }
        }

        /// <summary>
        /// Creates a Parameter on the Service
        /// </summary>
        /// <param name="sessionId">the id of the current session</param>
        /// <param name="name">the desired name of the parameter</param>
        /// <param name="value">the value of the parameter</param>
        /// <param name="typeName">the typename of structured types</param>
        /// <returns>a Parameter that represents the desired value</returns>
        public IDbDataParameter CreateParameter(long sessionId, string name, object value, string typeName)
        {
            LogEnvironment.LogDebugEvent(null, "Entering CreateParameter", (int)LogSeverity.Report, null);
            LogEnvironment.LogDebugEvent(null, "Looking up session", (int)LogSeverity.Report, null);
            ConnectionHandle conn = GetSession(sessionId);
            LogEnvironment.LogDebugEvent(null, string.Format("session==null:{0}", conn == null), (int)LogSeverity.Report, null);
            long objectId = NextObjectId();
            LogEnvironment.LogDebugEvent(null, string.Format("ObjectId: {0}",objectId), (int)LogSeverity.Report, null);
            IDbDataParameter param = conn.Connection.GetParameter(name, value, typeName);
            LogEnvironment.LogDebugEvent(null, string.Format("Param.Name: {0}", param.ParameterName), (int)LogSeverity.Report, null);
            lock (parameters)
            {
                LogEnvironment.LogDebugEvent(null, "Addin Parameter", (int)LogSeverity.Report, null);
                parameters.Add(objectId, param);
                LogEnvironment.LogDebugEvent(null, "Ok", (int)LogSeverity.Report, null);
            }

            LogEnvironment.LogDebugEvent(null, "About to return!", (int)LogSeverity.Report, null);
            return new RemoteDataParameter(param, objectId);
        }

        /// <summary>
        /// Destroys the Transaction with the given id
        /// </summary>
        /// <param name="objectId">the object id of the transaction on the server</param>
        public void DestroyTransaction(long objectId)
        {
            ITransaction transaction;
            lock (transactions)
            {
                if (!transactions.ContainsKey(objectId))
                {
                    throw new InvalidOperationException("The demanded transaction does not exist on this service");
                }

                transaction = transactions[objectId];
                transactions.Remove(objectId);
            }

            transaction.Dispose();
        }

        /// <summary>
        /// Gets the RollbackAtEnd - Value of the specified transaction
        /// </summary>
        /// <param name="objectId">the objectid of the server transaction</param>
        /// <returns>a value indicating whether the servertransaction will rolled back on dispose</returns>
        public bool GetTransactionRollbackState(long objectId)
        {
            lock (transactions)
            {
                if (!transactions.ContainsKey(objectId))
                {
                    throw new InvalidOperationException("The demanded transaction does not exist on this service");
                }

                return transactions[objectId].RollbackAtEnd;
            }
        }

        /// <summary>
        /// Sets a value indicating whether a specific transaction must be rolled back after disposal
        /// </summary>
        /// <param name="objectId">the unique id of the transaction</param>
        /// <param name="value">a value indicating whether to rollback on disposal</param>
        public void SetTransactionRollbackState(long objectId, bool value)
        {
            lock (transactions)
            {
                if (!transactions.ContainsKey(objectId))
                {
                    throw new InvalidOperationException("The demanded transaction does not exist on this service");
                }

                transactions[objectId].RollbackAtEnd = value;
            }
        }

        /// <summary>
        /// Creates a transaction for the given session
        /// </summary>
        /// <param name="sessionId">the session id for which to open a transaction</param>
        /// <returns>the object-id of the created transaction</returns>
        public long AcquireTransaction(long sessionId)
        {
            ConnectionHandle handle = GetSession(sessionId);
            long retVal = NextObjectId();
            lock (transactions)
            {
                transactions.Add(retVal, handle.Connection.AcquireTransaction());
            }

            return retVal;
        }

        /// <summary>
        /// Gets the native results of a specific query
        /// </summary>
        /// <param name="sessionId">the session id for which to get the results</param>
        /// <param name="command">the command to run on the target database</param>
        /// <param name="parameters">the parameters that will be used for the command</param>
        /// <returns>a resultset that was returned from the given query</returns>
        public DynamicResult[] GetResults(long sessionId, string command, IDbDataParameter[] parameters)
        {
            ConnectionHandle handle = GetSession(sessionId);
            IDbDataParameter[] dbparams = BuildParameters(parameters, handle.Connection);
            LogEnvironment.LogDebugEvent(null, $"Querying: {command}",(int)LogSeverity.Report,null );
            return handle.Connection.GetNativeResults(command, null, dbparams);
        }

        /// <summary>
        /// Calls a procedure and returns the appropriate results to the caller
        /// </summary>
        /// <param name="sessionId">the id of the calling session</param>
        /// <param name="procedureName">the procedure that needs to be called</param>
        /// <param name="parameters">the parameters that are provided to the procedure</param>
        /// <returns>a list of results that come as result from the procedure</returns>
        public List<DynamicResult[]> CallProcedureResults(long sessionId, string procedureName, IDbDataParameter[] parameters)
        {
            ConnectionHandle handle = GetSession(sessionId);
            LogEnvironment.LogDebugEvent(null, $"calling procedure: {procedureName}", (int) LogSeverity.Report, null);
            return handle.Connection.CallProcedure(procedureName, null, BuildParameters(parameters, handle.Connection));
        }

        /// <summary>
        /// Calls a procecdure and returns the result as an object
        /// </summary>
        /// <param name="sessionId">the id of the calling session</param>
        /// <param name="procedureName">the procecure that needs to be called</param>
        /// <param name="parameters">the parameters for the procedure</param>
        /// <returns>the result of the procedure</returns>
        public object CallProcedure(long sessionId, string procedureName, IDbDataParameter[] parameters)
        {
            ConnectionHandle handle = GetSession(sessionId);
            IDbDataParameter[] args = BuildParameters(parameters, handle.Connection);
            LogEnvironment.LogDebugEvent(null, $"Calling Procedure: {procedureName}", (int) LogSeverity.Report, null);
            return handle.Connection.CallProcedure<object>(procedureName, args);
        }

        /// <summary>
        /// Executes a command scalar and returns the first value of the first result that was returned
        /// </summary>
        /// <param name="sessionId">thje id of the calling session</param>
        /// <param name="command">the command that needs to be executed</param>
        /// <param name="parameters">the arguments for the command</param>
        /// <returns>the result of the command</returns>
        public object ExecuteCommandScalar(long sessionId, string command, IDbDataParameter[] parameters)
        {
            ConnectionHandle handle = GetSession(sessionId);
            IDbDataParameter[] args = BuildParameters(parameters, handle.Connection);
            LogEnvironment.LogDebugEvent(null, $"Executing: {command}", (int)LogSeverity.Report, null);
            return handle.Connection.ExecuteCommandScalar<object>(command, args);
        }

        /// <summary>
        /// Executes a command on the target database
        /// </summary>
        /// <param name="sessionId">the id of the calling session</param>
        /// <param name="command">the command that needs to be executed</param>
        /// <param name="parameters">the arguments for the command</param>
        /// <returns>the result of the complex command as integer</returns>
        public int ExecuteComplexCommand(long sessionId, string command, IDbDataParameter[] parameters)
        {
            ConnectionHandle handle = GetSession(sessionId);
            IDbDataParameter[] args = BuildParameters(parameters, handle.Connection);
            LogEnvironment.LogDebugEvent(null, $"Executing: {command}", (int)LogSeverity.Report, null);
            return handle.Connection.ExecuteComplexCommand(command, args);
        }

        /// <summary>
        /// Executes a simple command on the target database
        /// </summary>
        /// <param name="sessionId">the id of the calling session</param>
        /// <param name="command">the command that needs to be executed</param>
        /// <param name="parameters">the arguments for the command</param>
        /// <returns>the number of affected rows</returns>
        public int ExecuteCommand(long sessionId, string command, IDbDataParameter[] parameters)
        {
            ConnectionHandle handle = GetSession(sessionId);
            IDbDataParameter[] args = BuildParameters(parameters, handle.Connection);
            LogEnvironment.LogDebugEvent(null, $"Executing: {command}", (int) LogSeverity.Report, null);
            return handle.Connection.ExecuteCommand(command, args);
        }

        /// <summary>
        /// Creates an array of Database compatible parameters from the provided array of parameters
        /// </summary>
        /// <param name="inputParams">the parameters that were provided by the client</param>
        /// <param name="connection">the connection that can be used to re-create parameters that were lost</param>
        /// <returns>an array of parameters that is compatible to the underlaying database connection</returns>
        private IDbDataParameter[] BuildParameters(IDbDataParameter[] inputParams, IDbWrapper connection)
        {
            IDbDataParameter[] retVal = null;
            if (parameters != null)
            {
                retVal = new IDbDataParameter[inputParams.Length];
                for (int i = 0; i < inputParams.Length; i++)
                {
                    RemoteDataParameter param = inputParams[i] as RemoteDataParameter;
                    if (param == null)
                    {
                        throw new InvalidOperationException(
                            "Need to provide Parameters that were created by the connection!");
                    }

                    lock (this.parameters)
                    {
                        IDbDataParameter dbParam;
                        if (this.parameters.ContainsKey(param.ObjectId))
                        {
                            dbParam = this.parameters[param.ObjectId];
                        }
                        else
                        {
                            dbParam = connection.GetParameter(param.ParameterName, param.Value);
                        }

                        param.ApplyTo(dbParam);
                        retVal[i] = dbParam;
                    }
                }
            }

            return retVal;
        }

        /// <summary>
        /// Looks up the desired session
        /// </summary>
        /// <param name="sessionId">the session for which to perform a specific action</param>
        /// <returns>a connectionhandle that contains all relevant data</returns>
        private ConnectionHandle GetSession(long sessionId)
        {
            lock (connections)
            {
                if (!connections.ContainsKey(sessionId))
                {
                    throw new InvalidOperationException(string.Format("Unknown Session ID: {0}", sessionId));
                }

                return connections[sessionId];
            }
        }

        /// <summary>
        /// Leases the next object id
        /// </summary>
        /// <returns>the id of the next created object</returns>
        private long NextObjectId()
        {
            lock (objectIdLock)
            {
                return nextObjectId++;
            }
        }

        private class ConnectionHandle
        {
            public IDbWrapper Connection { get; set; }
        }
    }
}
