using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Text;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.DataAccess.Models;
using ITVComponents.DataAccess.Remote.ProxyObjects;
using ITVComponents.DataAccess.Remote.RemoteInterface;
using ITVComponents.InterProcessCommunication.Shared.Base;

namespace ITVComponents.DataAccess.Remote
{
    public class DatabaseProxyClient:IDbWrapper
    {
        private IRemoteProxyDefinition proxy;
        private long sessionId;

        public DatabaseProxyClient(IBaseClient communicationObject, string serverProxyName)
        {
            proxy = communicationObject.CreateProxy<IRemoteProxyDefinition>(serverProxyName);
            sessionId = proxy.AcquireConnection();
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }


        /// <summary>
        /// Executes a command with name-bound parameters
        /// </summary>
        /// <param name="command">the command to execute</param>
        /// <param name="parameters">the parameters to attach to the command</param>
        /// <returns>an integer indicating how many records were affected by the executed command</returns>
        public int ExecuteCommand(string command, params IDbDataParameter[] parameters)
        {
            return proxy.ExecuteCommand(sessionId, command, parameters);
        }

        /// <summary>
        /// Executes a complex command with name-bound parameters
        /// </summary>
        /// <param name="command">the command to execute</param>
        /// <param name="parameters">the parameters to attach to the command</param>
        /// <returns>an integer indicating how many records were affected by the executed command</returns>
        public int ExecuteComplexCommand(string command, params IDbDataParameter[] parameters)
        {
            return proxy.ExecuteComplexCommand(sessionId, command, parameters);
        }

        /// <summary>
        /// Executes command with name-bound parameters that only returns a scalar
        /// </summary>
        /// <typeparam name="T">the type of the expected scalar</typeparam>
        /// <param name="command">the command to execute</param>
        /// <param name="parameters">the parameters to use for the command</param>
        /// <returns>the scalar value resultet by the execution of the command</returns>
        public T ExecuteCommandScalar<T>(string command, params IDbDataParameter[] parameters)
        {
            return (T)proxy.ExecuteCommandScalar(sessionId, command, parameters);
        }

        /// <summary>
        /// Calls a specific stored procedure with the provided parameters
        /// </summary>
        /// <typeparam name="T">the Type that is expected as return type</typeparam>
        /// <param name="procedureName">the name of the stored procedure</param>
        /// <param name="parameters">the parameters used to call the procedure</param>
        /// <returns>the return value in the expected Type</returns>
        public T CallProcedure<T>(string procedureName, params IDbDataParameter[] parameters)
        {
            return (T) proxy.CallProcedure(sessionId, procedureName, parameters);
        }

        /// <summary>
        /// Calls a specific stored procedure with the provided parameters and returns its result set
        /// </summary>
        /// <typeparam name="T">the Type that is expected as entity type</typeparam>
        /// <param name="procedureName">the name of the stored procedure</param>
        /// <param name="parameters">the parameters used to call the procedure</param>
        /// <returns>the return value in the expected Type</returns>
        public IEnumerable<T> ReadProcedure<T>(string procedureName, params IDbDataParameter[] parameters) where T : new()
        {
            return proxy.CallProcedureResults(sessionId, procedureName, parameters).First().GetModelResult<T, T>();
        }

        /// <summary>
        /// Calls a specific stored procedure with the provided parameters and returns its result set
        /// </summary>
        /// <typeparam name="T">the Type that is expected as entity type</typeparam>
        /// <typeparam name="TM">the Meta-Type from which to take the Mapping for the results</typeparam>
        /// <param name="procedureName">the name of the stored procedure</param>
        /// <param name="parameters">the parameters used to call the procedure</param>
        /// <returns>the return value in the expected Type</returns>
        public IEnumerable<T> ReadProcedure<T, TM>(string procedureName, params IDbDataParameter[] parameters) where T : new()
        {
            return proxy.CallProcedureResults(sessionId, procedureName, parameters).First().GetModelResult<T, TM>();
        }

        /// <summary>
        /// Retreives the result of a procedure call in one or more resultsets
        /// </summary>
        /// <param name="procedureName">the procedure that is called</param>
        /// <param name="saveControllers">the save controller for the retreived resultsets</param>
        /// <param name="parameters">the parameters used to execute the command</param>
        /// <returns>a list of resultsets retreived from the called procedure</returns>
        public List<DynamicResult[]> CallProcedure(string procedureName, IController[] saveControllers, params IDbDataParameter[] parameters)
        {
            return proxy.CallProcedureResults(sessionId, procedureName, parameters);
        }

        /// <summary>
        /// Gets a list of dynamic as result for a query
        /// </summary>
        /// <param name="command">the query to execute on the database</param>
        /// <param name="saveController">the controller object used to provide instant-update capabilities on the fetched items</param>
        /// <param name="parameters">the parameters for the query</param>
        /// <returns>a result set for the provided query</returns>
        public dynamic[] GetResults(string command, IController saveController, params IDbDataParameter[] parameters)
        {
            return proxy.GetResults(sessionId, command, parameters);
        }

        /// <summary>
        /// Gets a list of dynamic as result for a query
        /// </summary>
        /// <param name="command">the query to execute on the database</param>
        /// <param name="saveController">the controller object used to provide instant-update capabilities on the fetched items</param>
        /// <param name="parameters">the parameters for the query</param>
        /// <param param name="autoSave">indicates whether items must be automatically saved</param>
        /// <returns>a result set for the provided query</returns>
        public dynamic[] GetResults(string command, IController saveController, bool autoSave, params IDbDataParameter[] parameters)
        {
            return proxy.GetResults(sessionId, command, parameters);
        }

        /// <summary>
        /// Gets a list of dynamic as result for a query
        /// </summary>
        /// <param name="command">the query to execute on the database</param>
        /// <param name="saveController">the controller object used to provide instant-update capabilities on the fetched items</param>
        /// <param name="parameters">the parameters for the query</param>
        /// <returns>a result set for the provided query</returns>
        public DynamicResult[] GetNativeResults(string command, IController saveController, params IDbDataParameter[] parameters)
        {
            return proxy.GetResults(sessionId, command, parameters);
        }

        /// <summary>
        /// Gets a list of dynamic as result for a query
        /// </summary>
        /// <param name="command">the query to execute on the database</param>
        /// <param name="saveController">the controller object used to provide instant-update capabilities on the fetched items</param>
        /// <param name="parameters">the parameters for the query</param>
        /// <param param name="autoSave">indicates whether items must be automatically saved</param>
        /// <returns>a result set for the provided query</returns>
        public DynamicResult[] GetNativeResults(string command, IController saveController, bool autoSave,
                                                IDbDataParameter[] parameters)
        {
            return proxy.GetResults(sessionId, command, parameters);
        }

        /// <summary>
        /// Gets the results for a query to a specific datatype
        /// </summary>
        /// <typeparam name="T">the Type in which to convert the original results</typeparam>
        /// <param name="command">the command that serves the requested data</param>
        /// <param name="parameters">the parameters for the query</param>
        /// <returns>an array containing items of the requested type</returns>
        public IEnumerable<T> GetResults<T>(string command, params IDbDataParameter[] parameters) where T : new()
        {
            return proxy.GetResults(sessionId, command, parameters).GetModelResult<T,T>();
        }

        /// <summary>
        /// Gets the results for a query to a specific datatype
        /// </summary>
        /// <typeparam name="T">the Type in which to convert the original results</typeparam>
        /// <typeparam name="TM">the Meta-Type from which to take the Mapping for the results</typeparam>
        /// <param name="command">the command that serves the requested data</param>
        /// <param name="parameters">the parameters for the query</param>
        /// <returns>an array containing items of the requested type</returns>
        public IEnumerable<T> GetResults<T, TM>(string command, params IDbDataParameter[] parameters) where T : new()
        {
            return proxy.GetResults(sessionId, command, parameters).GetModelResult<T, TM>();
        }

        /// <summary>
        /// Gets the results for a query to a specific datatype
        /// </summary>
        /// <param name="command">the command that serves the requested data</param>
        /// <param name="targetType">the Type in which to convert the original results</param>
        /// <param name="parameters">the parameters for the query</param>
        /// <returns>an array containing items of the requested type</returns>
        public IEnumerable<object> GetResults(string command, Type targetType, params IDbDataParameter[] parameters)
        {
            return proxy.GetResults(sessionId, command, parameters).GetModelResult(targetType, targetType);
        }

        /// <summary>
        /// Gets the results for a query to a specific datatype
        /// </summary>
        /// <param name="command">the command that serves the requested data</param>
        /// <param name="targetType">the Type in which to convert the original results</param>
        /// <param name="metaType">the type that contains meta-information that can be used to assign data to members on the target-type</param>
        /// <param name="parameters">the parameters for the query</param>
        /// <returns>an array containing items of the requested type</returns>
        public IEnumerable<object> GetResults(string command, Type targetType, Type metaType, params IDbDataParameter[] parameters)
        {
            return proxy.GetResults(sessionId, command, parameters).GetModelResult(targetType, metaType);
        }

        /// <summary>
        /// Gets the results for a query to a specific datatype
        /// </summary>
        /// <param name="command">the command that serves the requested data</param>
        /// <param name="parameters">the parameters for the query</param>
        /// <returns>an array containing items of the ExpandoObject type</returns>
        public IEnumerable<ExpandoObject> GetExpandoResults(string command, bool extendWithUppercase, params IDbDataParameter[] parameters)
        {
            return proxy.GetResults(sessionId, command, parameters).GetExpandoResults(extendWithUppercase);
        }

        /// <summary>
        /// Gets the results for a query to a specific datatype
        /// </summary>
        /// <param name="command">the command that serves the requested data</param>
        /// <param name="parameters">the parameters for the query</param>
        /// <returns>an array containing items of the ExpandoObject type</returns>
        public IEnumerable<IDictionary<string,object>> GetDictionaryResults(string command, bool caseInsensitive, params IDbDataParameter[] parameters)
        {
            return proxy.GetResults(sessionId, command, parameters).GetDictionaryResults(caseInsensitive);
        }

        /// <summary>
        /// Gets the results as DataTable for a query
        /// </summary>
        /// <param name="command">the command that serves the requested data</param>
        /// <param name="parameters">the parameters for the query</param>
        /// <returns>a DataTable with the requested data</returns>
        public DataTable GetResults(string command, params IDbDataParameter[] parameters)
        {
            return proxy.GetResults(sessionId, command, parameters).ToDataTable();
        }

        /// <summary>
        /// Begins a transaction which can be used to rollback manipulations or to commit them when a processingblock ends
        /// </summary>
        /// <returns>an ITransaction object used to control the transaction</returns>
        public ITransaction AcquireTransaction()
        {
            long objectId = proxy.AcquireTransaction(sessionId);
            return new RemoteTransaction(proxy, objectId);
        }

        /// <summary>
        /// Gets a parameter
        /// </summary>
        /// <param name="name">the name of the parameter</param>
        /// <param name="value">the value of the parameter</param>
        /// <param name="typeName">the typename that is used to add table-values to a parameter</param>
        /// <returns>a specialized parameter for the currently underlaying database type</returns>
        public IDbDataParameter GetParameter(string name, object value, string typeName = null)
        {
            IDbDataParameter retVal = proxy.CreateParameter(sessionId, name, value, typeName);
            ((RemoteDataParameter) retVal).ClientInit(proxy);
            return retVal;
        }

        public Type GetType(string typeName)
        {
            throw new NotImplementedException();
        }

        public T GetLastIndex<T>(string tableName)
        {
            throw new NotImplementedException();
        }

        public void InsertRecord(DynamicResult value, string tableName, Dictionary<string, string> hardcodedValues)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            proxy.ReleaseConnection(sessionId);
        }

        public event EventHandler Disposed;
    }
}
