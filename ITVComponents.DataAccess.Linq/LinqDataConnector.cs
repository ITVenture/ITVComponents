using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Text;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.DataAccess.Models;
using ITVComponents.Scripting.CScript.Core.Native;

namespace ITVComponents.DataAccess.Linq
{
    public class LinqDataConnector:IDbWrapper
    {
        /// <summary>
        /// the dataContext of this wrapper
        /// </summary>
        private IDataContext context;

        /// <summary>
        /// Initializes a new instance of the LinqDataConnector class
        /// </summary>
        /// <param name="appDomainName">the appdomain into which to compile and run the linq queries for this connection</param>
        /// <param name="context">the datacontext of this connection</param>
        public LinqDataConnector(IDataContext context)
            : this()
        {
            this.context = context;
        }

        /// <summary>
        /// Prevents a default instance of the LinqDataConnector class from being created
        /// </summary>
        private LinqDataConnector()
        {
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// Executes a complex command with name-bound parameters
        /// </summary>
        /// <param name="command">the command to execute</param>
        /// <param name="parameters">the parameters to attach to the command</param>
        /// <returns>an integer indicating how many records were affected by the executed command</returns>
        public int ExecuteComplexCommand(string command, params IDbDataParameter[] parameters)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
           return GetNativeResults(command, saveController, false, parameters);
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
            return GetNativeResults(command, saveController, autoSave, parameters);
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
            return GetNativeResults(command, saveController, false, parameters);
        }

        /// <summary>
        /// Gets a list of dynamic as result for a query
        /// </summary>
        /// <param name="command">the query to execute on the database</param>
        /// <param name="saveController">the controller object used to provide instant-update capabilities on the fetched items</param>
        /// <param name="parameters">the parameters for the query</param>
        /// <param param name="autoSave">indicates whether items must be automatically saved</param>
        /// <returns>a result set for the provided query</returns>
        public DynamicResult[] GetNativeResults(string command,
            IController saveController,
            bool autoSave,
            IDbDataParameter[] parameters)
        {
            var tables = context.Tables;
            var variables = parameters.Cast<LinqParameter>().ToArray();
            var cmd = LinqCommandCreator.CreateCommand(command);
            ScriptData data = new ScriptData();
            IDictionary<string, object> dic = new ExpandoObject();
            foreach (var tab in context.Tables)
            {
                dic[tab.Key] = tab.Value;
            }

            foreach (var param in parameters)
            {
                dic[param.ParameterName] = param.Value;
            }

            data.Global = dic;
            var retVal = (AsyncHelpers.RunSync(() => cmd.RunAsync(data)).ReturnValue as IEnumerable<DynamicResult>).ToArray();
            retVal.ForEach(n =>
            {
                n.Controller = saveController;
                n.AutoSave = autoSave;
            });
            return retVal;
        }

        /// <summary>
        /// Inserts a record into the specified table with the specified values
        /// </summary>
        /// <param name="value">the result containing the items inserted into the table</param>
        /// <param name="tableName">the tablename in which to insert the data</param>
        /// <param name="hardcodedValues">values that are hardcoded</param>
        public void InsertRecord(DynamicResult value, string tableName, Dictionary<string, string> hardcodedValues)
        {
            throw new NotImplementedException();
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
            return GetResults<T, T>(command, parameters);
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
            var tmp = GetNativeResults(command, null, parameters);
            return tmp.GetModelResult<T, TM>();
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
            return GetResults(command, targetType, targetType, parameters);
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
            var tmp = GetNativeResults(command, null, parameters);
            return tmp.GetModelResult(targetType, metaType);
        }

        public IEnumerable<ExpandoObject> GetExpandoResults(string command, bool extendWithUppercase, params IDbDataParameter[] parameters)
        {
            var tmp = GetNativeResults(command, null, parameters);
            return tmp.GetExpandoResults(extendWithUppercase);
        }

        public IEnumerable<IDictionary<string, object>> GetDictionaryResults(string command, bool caseInsensitive,
            params IDbDataParameter[] parameters)
        {
            var tmp = GetNativeResults(command, null, parameters);
            return tmp.GetDictionaryResults(caseInsensitive);
        }

        /// <summary>
        /// Gets the results as DataTable for a query
        /// </summary>
        /// <param name="command">the command that serves the requested data</param>
        /// <param name="parameters">the parameters for the query</param>
        /// <returns>a DataTable with the requested data</returns>
        public DataTable GetResults(string command, params IDbDataParameter[] parameters)
        {
            var tmp = GetNativeResults(command, null, parameters);
            return tmp.ToDataTable();
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
            return new LinqParameter() {ParameterName = string.Format("@{0}", name), Value = value};
        }

        /// <summary>
        /// Gets the Type representation of a SQL - Typename
        /// </summary>
        /// <param name="typeName">the name of the sql type</param>
        /// <returns>the type representation of the demanded type</returns>
        public Type GetType(string typeName)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the last index written to the database
        /// </summary>
        /// <typeparam name="T">the type of the index-value</typeparam>
        /// <param name="tableName">the table in which a record was stored</param>
        /// <returns>the indexvalue assigned by the database</returns>
        public T GetLastIndex<T>(string tableName)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Begins a transaction which can be used to rollback manipulations or to commit them when a processingblock ends
        /// </summary>
        /// <returns>an ITransaction object used to control the transaction</returns>
        public ITransaction AcquireTransaction()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
