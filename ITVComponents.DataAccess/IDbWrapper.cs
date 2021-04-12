using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using ITVComponents.Plugins;

namespace ITVComponents.DataAccess
{
    public interface IDbWrapper : IPlugin
    {
        /// <summary>
        /// Executes a command with name-bound parameters
        /// </summary>
        /// <param name="command">the command to execute</param>
        /// <param name="parameters">the parameters to attach to the command</param>
        /// <returns>an integer indicating how many records were affected by the executed command</returns>
        int ExecuteCommand(string command, params IDbDataParameter[] parameters);

        /// <summary>
        /// Executes a complex command with name-bound parameters
        /// </summary>
        /// <param name="command">the command to execute</param>
        /// <param name="parameters">the parameters to attach to the command</param>
        /// <returns>an integer indicating how many records were affected by the executed command</returns>
        int ExecuteComplexCommand(string command, params IDbDataParameter[] parameters);

        /// <summary>
        /// Executes command with name-bound parameters that only returns a scalar
        /// </summary>
        /// <typeparam name="T">the type of the expected scalar</typeparam>
        /// <param name="command">the command to execute</param>
        /// <param name="parameters">the parameters to use for the command</param>
        /// <returns>the scalar value resultet by the execution of the command</returns>
        T ExecuteCommandScalar<T>(string command, params IDbDataParameter[] parameters);

        /// <summary>
        /// Calls a specific stored procedure with the provided parameters
        /// </summary>
        /// <typeparam name="T">the Type that is expected as return type</typeparam>
        /// <param name="procedureName">the name of the stored procedure</param>
        /// <param name="parameters">the parameters used to call the procedure</param>
        /// <returns>the return value in the expected Type</returns>
        T CallProcedure<T>(string procedureName, params IDbDataParameter[] parameters);

        /// <summary>
        /// Calls a specific stored procedure with the provided parameters and returns its result set
        /// </summary>
        /// <typeparam name="T">the Type that is expected as entity type</typeparam>
        /// <param name="procedureName">the name of the stored procedure</param>
        /// <param name="parameters">the parameters used to call the procedure</param>
        /// <returns>the return value in the expected Type</returns>
        IEnumerable<T> ReadProcedure<T>(string procedureName, params IDbDataParameter[] parameters) where T:new();

        /// <summary>
        /// Calls a specific stored procedure with the provided parameters and returns its result set
        /// </summary>
        /// <typeparam name="T">the Type that is expected as entity type</typeparam>
        /// <typeparam name="TM">the Meta-Type from which to take the Mapping for the results</typeparam>
        /// <param name="procedureName">the name of the stored procedure</param>
        /// <param name="parameters">the parameters used to call the procedure</param>
        /// <returns>the return value in the expected Type</returns>
        IEnumerable<T> ReadProcedure<T, TM>(string procedureName, params IDbDataParameter[] parameters) where T : new();
            
        /// <summary>
        /// Retreives the result of a procedure call in one or more resultsets
        /// </summary>
        /// <param name="procedureName">the procedure that is called</param>
        /// <param name="saveControllers">the save controller for the retreived resultsets</param>
        /// <param name="parameters">the parameters used to execute the command</param>
        /// <returns>a list of resultsets retreived from the called procedure</returns>
        List<DynamicResult[]> CallProcedure(string procedureName, IController[] saveControllers, params IDbDataParameter[] parameters);

        /// <summary>
        /// Gets a list of dynamic as result for a query
        /// </summary>
        /// <param name="command">the query to execute on the database</param>
        /// <param name="saveController">the controller object used to provide instant-update capabilities on the fetched items</param>
        /// <param name="parameters">the parameters for the query</param>
        /// <returns>a result set for the provided query</returns>
        dynamic[] GetResults(string command, IController saveController, params IDbDataParameter[] parameters);

        /// <summary>
        /// Gets a list of dynamic as result for a query
        /// </summary>
        /// <param name="command">the query to execute on the database</param>
        /// <param name="saveController">the controller object used to provide instant-update capabilities on the fetched items</param>
        /// <param name="parameters">the parameters for the query</param>
        /// <param param name="autoSave">indicates whether items must be automatically saved</param>
        /// <returns>a result set for the provided query</returns>
        dynamic[] GetResults(string command, IController saveController, bool autoSave, params IDbDataParameter[] parameters);

        /// <summary>
        /// Gets a list of dynamic as result for a query
        /// </summary>
        /// <param name="command">the query to execute on the database</param>
        /// <param name="saveController">the controller object used to provide instant-update capabilities on the fetched items</param>
        /// <param name="parameters">the parameters for the query</param>
        /// <returns>a result set for the provided query</returns>
        DynamicResult[] GetNativeResults(string command, IController saveController, params IDbDataParameter[] parameters);

        /// <summary>
        /// Gets a list of dynamic as result for a query
        /// </summary>
        /// <param name="command">the query to execute on the database</param>
        /// <param name="saveController">the controller object used to provide instant-update capabilities on the fetched items</param>
        /// <param name="parameters">the parameters for the query</param>
        /// <param param name="autoSave">indicates whether items must be automatically saved</param>
        /// <returns>a result set for the provided query</returns>
        DynamicResult [] GetNativeResults(string command, IController saveController, bool autoSave, IDbDataParameter[] parameters);

        /// <summary>
        /// Inserts a record into the specified table with the specified values
        /// </summary>
        /// <param name="value">the result containing the items inserted into the table</param>
        /// <param name="tableName">the tablename in which to insert the data</param>
        /// <param name="hardcodedValues">values that are hardcoded</param>
        void InsertRecord(DynamicResult value, string tableName, Dictionary<string, string> hardcodedValues);

        /// <summary>
        /// Gets the results for a query to a specific datatype
        /// </summary>
        /// <typeparam name="T">the Type in which to convert the original results</typeparam>
        /// <param name="command">the command that serves the requested data</param>
        /// <param name="parameters">the parameters for the query</param>
        /// <returns>an array containing items of the requested type</returns>
        IEnumerable<T> GetResults<T>(string command, params IDbDataParameter[] parameters) where T : new();

        /// <summary>
        /// Gets the results for a query to a specific datatype
        /// </summary>
        /// <typeparam name="T">the Type in which to convert the original results</typeparam>
        /// <typeparam name="TM">the Meta-Type from which to take the Mapping for the results</typeparam>
        /// <param name="command">the command that serves the requested data</param>
        /// <param name="parameters">the parameters for the query</param>
        /// <returns>an array containing items of the requested type</returns>
        IEnumerable<T> GetResults<T,TM>(string command, params IDbDataParameter[] parameters) where T : new();

        /// <summary>
        /// Gets the results for a query to a specific datatype
        /// </summary>
        /// <param name="command">the command that serves the requested data</param>
        /// <param name="targetType">the Type in which to convert the original results</param>
        /// <param name="parameters">the parameters for the query</param>
        /// <returns>an array containing items of the requested type</returns>
        IEnumerable<object> GetResults(string command, Type targetType, params IDbDataParameter[] parameters);


        /// <summary>
        /// Gets the results for a query to a specific datatype
        /// </summary>
        /// <param name="command">the command that serves the requested data</param>
        /// <param name="extendWithUppercase">indicates whether to extend the expandoresult objects with the uppercase representation of each property</param>
        /// <param name="parameters">the parameters for the query</param>
        /// <returns>an array containing items of the ExpandoObject type</returns>
        IEnumerable<ExpandoObject> GetExpandoResults(string command, bool extendWithUppercase, params IDbDataParameter[] parameters);

        /// <summary>
        /// Gets the results for a query to a specific datatype
        /// </summary>
        /// <param name="command">the command that serves the requested data</param>
        /// <param name="caseInsensitive">indicates whether to create a case-insensitive dictionary</param>
        /// <param name="parameters">the parameters for the query</param>
        /// <returns>an array containing items of the ExpandoObject type</returns>
        IEnumerable<IDictionary<string, object>> GetDictionaryResults(string command, bool caseInsensitive,
            params IDbDataParameter[] parameters);
            /// <summary>
        /// Gets the results for a query to a specific datatype
        /// </summary>
        /// <param name="command">the command that serves the requested data</param>
        /// <param name="targetType">the Type in which to convert the original results</param>
        /// <param name="metaType">the type that contains meta-information that can be used to assign data to members on the target-type</param>
        /// <param name="parameters">the parameters for the query</param>
        /// <returns>an array containing items of the requested type</returns>
        IEnumerable<object> GetResults(string command, Type targetType, Type metaType, params IDbDataParameter[] parameters);

        /// <summary>
        /// Gets the results as DataTable for a query
        /// </summary>
        /// <param name="command">the command that serves the requested data</param>
        /// <param name="parameters">the parameters for the query</param>
        /// <returns>a DataTable with the requested data</returns>
        DataTable GetResults(string command, params IDbDataParameter[] parameters);

        /// <summary>
        /// Gets a parameter
        /// </summary>
        /// <param name="name">the name of the parameter</param>
        /// <param name="value">the value of the parameter</param>
        /// <param name="typeName">the typename that is used to add table-values to a parameter</param>
        /// <returns>a specialized parameter for the currently underlaying database type</returns>
        IDbDataParameter GetParameter(string name, object value, string typeName = null);

        /// <summary>
        /// Gets the Type representation of a SQL - Typename
        /// </summary>
        /// <param name="typeName">the name of the sql type</param>
        /// <returns>the type representation of the demanded type</returns>
        Type GetType(string typeName);

        /// <summary>
        /// Gets the last index written to the database
        /// </summary>
        /// <typeparam name="T">the type of the index-value</typeparam>
        /// <param name="tableName">the table in which a record was stored</param>
        /// <returns>the indexvalue assigned by the database</returns>
        T GetLastIndex<T>(string tableName);

        /// <summary>
        /// Begins a transaction which can be used to rollback manipulations or to commit them when a processingblock ends
        /// </summary>
        /// <returns>an ITransaction object used to control the transaction</returns>
        ITransaction AcquireTransaction();
    }
}
