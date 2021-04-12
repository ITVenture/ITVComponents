using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using ITVComponents.DataAccess.Remote.ProxyObjects;

namespace ITVComponents.DataAccess.Remote.RemoteInterface
{
    public interface IRemoteProxyDefinition
    {
        /// <summary>
        /// Acquires a Connection and returns the session handle
        /// </summary>
        /// <returns>the session-handle for the created connection</returns>
        long AcquireConnection();

        /// <summary>
        /// Releases the provided connection
        /// </summary>
        /// <param name="sessionId">the session-id for which to release all open objects</param>
        void ReleaseConnection(long sessionId);

        /// <summary>
        /// Gets the Value of a specific parameter that was created on this service
        /// </summary>
        /// <param name="objectId">the id of the service object</param>
        /// <returns>an instance of a Parameter-object</returns>
        object GetParameterValue(long objectId);

        /// <summary>
        /// Destroys a specific parameter on the server
        /// </summary>
        /// <param name="objectId">the parameter that needs to be destroyed</param>
        void DestroyParameter(long objectId);

        /// <summary>
        /// Creates a Parameter on the Service
        /// </summary>
        /// <param name="sessionId">the id of the current session</param>
        /// <param name="name">the desired name of the parameter</param>
        /// <param name="value">the value of the parameter</param>
        /// <param name="typeName">the typename of structured types</param>
        /// <returns>a Parameter that represents the desired value</returns>
        IDbDataParameter CreateParameter(long sessionId, string name, object value, string typeName);

        /// <summary>
        /// Destroys the Transaction with the given id
        /// </summary>
        /// <param name="objectId">the object id of the transaction on the server</param>
        void DestroyTransaction(long objectId);

        /// <summary>
        /// Gets the RollbackAtEnd - Value of the specified transaction
        /// </summary>
        /// <param name="objectId">the objectid of the server transaction</param>
        /// <returns>a value indicating whether the servertransaction will rolled back on dispose</returns>
        bool GetTransactionRollbackState(long objectId);

        /// <summary>
        /// Sets a value indicating whether a specific transaction must be rolled back after disposal
        /// </summary>
        /// <param name="objectId">the unique id of the transaction</param>
        /// <param name="value">a value indicating whether to rollback on disposal</param>
        void SetTransactionRollbackState(long objectId, bool value);

        /// <summary>
        /// Creates a transaction for the given session
        /// </summary>
        /// <param name="sessionId">the session id for which to open a transaction</param>
        /// <returns>the object-id of the created transaction</returns>
        long AcquireTransaction(long sessionId);

        /// <summary>
        /// Gets the native results of a specific query
        /// </summary>
        /// <param name="sessionId">the session id for which to get the results</param>
        /// <param name="command">the command to run on the target database</param>
        /// <param name="parameters">the parameters that will be used for the command</param>
        /// <returns>a resultset that was returned from the given query</returns>
        DynamicResult[] GetResults(long sessionId, string command, IDbDataParameter[] parameters);

        /// <summary>
        /// Calls a procedure and returns the appropriate results to the caller
        /// </summary>
        /// <param name="sessionId">the id of the calling session</param>
        /// <param name="procedureName">the procedure that needs to be called</param>
        /// <param name="parameters">the parameters that are provided to the procedure</param>
        /// <returns>a list of results that come as result from the procedure</returns>
        List<DynamicResult[]> CallProcedureResults(long sessionId, string procedureName, IDbDataParameter[] parameters);

        /// <summary>
        /// Calls a procecdure and returns the result as an object
        /// </summary>
        /// <param name="sessionId">the id of the calling session</param>
        /// <param name="procedureName">the procecure that needs to be called</param>
        /// <param name="parameters">the parameters for the procedure</param>
        /// <returns>the result of the procedure</returns>
        object CallProcedure(long sessionId, string procedureName, IDbDataParameter[] parameters);

        /// <summary>
        /// Executes a command scalar and returns the first value of the first result that was returned
        /// </summary>
        /// <param name="sessionId">thje id of the calling session</param>
        /// <param name="command">the command that needs to be executed</param>
        /// <param name="parameters">the arguments for the command</param>
        /// <returns>the result of the command</returns>
        object ExecuteCommandScalar(long sessionId, string command, IDbDataParameter[] parameters);

        /// <summary>
        /// Executes a command on the target database
        /// </summary>
        /// <param name="sessionId">the id of the calling session</param>
        /// <param name="command">the command that needs to be executed</param>
        /// <param name="parameters">the arguments for the command</param>
        /// <returns>the result of the complex command as integer</returns>
        int ExecuteComplexCommand(long sessionId, string command, IDbDataParameter[] parameters);

        /// <summary>
        /// Executes a simple command on the target database
        /// </summary>
        /// <param name="sessionId">the id of the calling session</param>
        /// <param name="command">the command that needs to be executed</param>
        /// <param name="parameters">the arguments for the command</param>
        /// <returns>the number of affected rows</returns>
        int ExecuteCommand(long sessionId, string command, IDbDataParameter[] parameters);
    }
}
