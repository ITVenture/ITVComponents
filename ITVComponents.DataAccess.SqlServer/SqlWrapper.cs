﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.DataAccess.Models;
using ITVComponents.DataAccess.SqlServer.Resources;
using ITVComponents.Logging;
using ITVComponents.TypeConversion;

namespace ITVComponents.DataAccess.SqlServer
{
    /// <summary>
    /// Class exposing a Connection to a SQL Server
    /// </summary>
    public class SqlWrapper:IDbWrapper
    {
        /// <summary>
        /// the current transaction used to control whether modifications on the data in the database are written definitly or rolled back
        /// </summary>
        private IDbTransaction localTransaction;

        /// <summary>
        /// the connection used to communicate with the sql database
        /// </summary>
        private SqlConnection connection;

        /// <summary>
        /// the connection string for the underlaying connection
        /// </summary>
        private string connectionString;

        /// <summary>
        /// local object used to synchronized multithreaded access on this instance
        /// </summary>
        private object localLock = new object();

        /// <summary>
        /// the commandTimeout that will be applied to all command objects generated by this wrapper
        /// </summary>
        private int commandTimeout;

        /// <summary>
        /// Initializes a new instance of the SqlWrapper class
        /// </summary>
        /// <param name="connectionString">the connection string used to communicate with a database</param>
        /// <param name="commandTimeout">the command timeout that will be applied to all command objects generated by this wrapper</param>
        public SqlWrapper(string connectionString, int commandTimeout):this()
        {
            this.commandTimeout = commandTimeout>=0?commandTimeout:int.MaxValue;
            this.connectionString = connectionString;
            connection = new SqlConnection(connectionString);
            connection.Open();
        }

        /// <summary>
        /// Initializes a new instance of the SqlWrapper class
        /// </summary>
        /// <param name="connectionString">the connection string used to communicate with a database</param>
        public SqlWrapper(string connectionString) : this(connectionString, 0)
        {
        }

        /// <summary>
        /// Prevents a default instance of the SqlWrapperClass from being created
        /// </summary>
        private SqlWrapper()
        {
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            connection.Dispose();
            if (Disposed != null)
            {
                Disposed(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Informs a client object that this item has been disposed
        /// </summary>
        [field: NonSerialized]
        public event EventHandler Disposed;

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
            return ExecuteComplexCommand(command, parameters);
        }

        /// <summary>
        /// Calls a procedure on the server and returns the value of the first column in the first row that was retreived from the server
        /// </summary>
        /// <typeparam name="T">the type that is expected</typeparam>
        /// <param name="command">the name of the procedure</param>
        /// <param name="parameters">the parameters to pass to the procedure</param>
        /// <returns>the return value of the procedure</returns>
        public T CallProcedure<T>(string command, params IDbDataParameter[] parameters)
        {
            T retVal = default(T);
            using (IDbCommand cmd = GetCommand(command, parameters))
            {
                try
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    object ret = cmd.ExecuteScalar();
                    if (ret != null)
                    {
                        retVal = ret.Cast<T>();
                    }
                }
                finally
                {
                    cmd.Parameters.Clear();
                }
            }

            return retVal;
        }

        /// <summary>
        /// Calls a specific stored procedure with the provided parameters and returns its result set
        /// </summary>
        /// <typeparam name="T">the Type that is expected as entity type</typeparam>
        /// <param name="procedureName">the name of the stored procedure</param>
        /// <param name="parameters">the parameters used to call the procedure</param>
        /// <returns>the return value in the expected Type</returns>
        public IEnumerable<T> ReadProcedure<T>(string procedureName, params IDbDataParameter[] parameters) where T:new()
        {
            return ReadProcedure<T, T>(procedureName, parameters);
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
            using (IDbCommand cmd = GetCommand(procedureName, parameters))
            {
                try
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    IDataReader ret = cmd.ExecuteReader();
                    return ret.GetModelResult<T, TM>();
                }
                finally
                {
                    cmd.Parameters.Clear();
                }
            }

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
            List<DynamicResult[]> retVal = new List<DynamicResult[]>();
            List<DynamicResult> singleList = new List<DynamicResult>();
            bool hasMoreResults = true;
            using (IDbCommand cmd = GetCommand(procedureName, parameters))
            {
                try
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        while (hasMoreResults)
                        {
                            singleList.Clear();
                            while (reader.Read())
                            {
                                singleList.Add(new DynamicResult(reader));
                            }

                            retVal.Add(singleList.ToArray());
                            hasMoreResults = reader.NextResult();
                        }
                    }
                }
                finally
                {
                    cmd.Parameters.Clear();
                }
            }

            return retVal;
        }

        /// <summary>
        /// Executes a complex command with name-bound parameters
        /// </summary>
        /// <param name="command">the command to execute</param>
        /// <param name="parameters">the parameters to attach to the command</param>
        /// <returns>an integer indicating how many records were affected by the executed command</returns>
        public int ExecuteComplexCommand(string command, params IDbDataParameter[] parameters)
        {
            using (IDbCommand cmd = GetCommand(command, parameters))
            {
                try
                {
                    return cmd.ExecuteNonQuery();
                }
                finally
                {
                    cmd.Parameters.Clear();
                }
            }
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
            using (IDbCommand cmd = GetCommand(command, parameters))
            {
                try
                {
                    object o = cmd.ExecuteScalar();
                    if (!(o is T))
                    {
                        o = TypeConverter.Convert(o, typeof (T));
                    }

                    return (T) o;
                }
                finally
                {
                    cmd.Parameters.Clear();
                }
            }
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
        /// <param name="autoSave">indicates whether items must be automatically saved</param>
        /// <returns>a result set for the provided query</returns>
        public DynamicResult[] GetNativeResults(string command, IController saveController, bool autoSave, IDbDataParameter[] parameters)
        {
            List<DynamicResult> retVal = new List<DynamicResult>();
            using (IDbCommand cmd = GetCommand(command, parameters))
            {
                try
                {
                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            retVal.Add(new DynamicResult(reader) { Controller = saveController, AutoSave = autoSave });
                        }
                    }
                }
                finally
                {
                    cmd.Parameters.Clear();
                }
            }

            return retVal.ToArray();
        }

        /// <summary>
        /// Inserts a record into the specified table with the specified values
        /// </summary>
        /// <param name="value">the result containing the items inserted into the table</param>
        /// <param name="tableName">the tablename in which to insert the data</param>
        /// <param name="hardcodedValues">values that are hardcoded</param>
        public void InsertRecord(DynamicResult value, string tableName, Dictionary<string, string> hardcodedValues)
        {
            List<string> fields = new List<string>(hardcodedValues.Keys);
            List<string> values = new List<string>((from t in fields select hardcodedValues[t]));
            IDbDataParameter[] parameters = (from t in value.GetDynamicMemberNames() select GetParameter(t, value[t]) as IDbDataParameter).ToArray();
            (from t in value.GetDynamicMemberNames() select t).ToList().ForEach(fields.Add);
            (from t in value.GetDynamicMemberNames() select "@" + t).ToList().ForEach(values.Add);
            string cmd = string.Format(SqlCommands.InsertWithGetIdentity, tableName, string.Join(",", fields), string.Join(",", values));
            int id = ExecuteCommandScalar<int>(cmd, parameters);
            if (value.Controller != null)
            {
                value.Controller.SetIndex(value, id);
            }
        }

        /// <summary>
        /// Gets the results for a query to a specific datatype
        /// </summary>
        /// <typeparam name="T">the Type in which to convert the original results</typeparam>
        /// <param name="command">the command that serves the requested data</param>
        /// <param name="parameters">the parameters for the query</param>
        /// <returns>an array containing items of the requested type</returns>
        public IEnumerable<T> GetResults<T>(string command, params IDbDataParameter[] parameters) where T:new()
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
        public IEnumerable<T> GetResults<T,TM>(string command, params IDbDataParameter[] parameters) where T : new()
        {
            using (IDbCommand cmd = GetCommand(command, parameters))
            {
                try
                {
                    return cmd.ExecuteReader().GetModelResult<T,TM>();
                }
                finally
                {
                    cmd.Parameters.Clear();
                }
            }
        }

        /// <summary>
        /// Gets the results for a query to a specific datatype
        /// </summary>
        /// <param name="command">the command that serves the requested data</param>
        /// <param name="parameters">the parameters for the query</param>
        /// <returns>an array containing items of the ExpandoObject type</returns>
        public IEnumerable<ExpandoObject> GetExpandoResults(string command, bool extendWithUppercase, params IDbDataParameter[] parameters)
        {
            using (IDbCommand cmd = GetCommand(command, parameters))
            {
                try
                {
                    return cmd.ExecuteReader().ToExpandoObjects(extendWithUppercase);
                }
                finally
                {
                    cmd.Parameters.Clear();
                }
            }
        }

        /// <summary>
        /// Gets the results for a query to a specific datatype
        /// </summary>
        /// <param name="command">the command that serves the requested data</param>
        /// <param name="caseInsensitive">indicates whether to create a case-insensitive dictionary</param>
        /// <param name="parameters">the parameters for the query</param>
        /// <returns>an array containing items of the ExpandoObject type</returns>
        public IEnumerable<IDictionary<string,object>> GetDictionaryResults(string command, bool caseInsensitive, params IDbDataParameter[] parameters)
        {
            using (IDbCommand cmd = GetCommand(command, parameters))
            {
                try
                {
                    return cmd.ExecuteReader().ToDictionaries(caseInsensitive);
                }
                finally
                {
                    cmd.Parameters.Clear();
                }
            }
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
            using (IDbCommand cmd = GetCommand(command, parameters))
            {
                try
                {
                    return cmd.ExecuteReader().GetModelResult(targetType, metaType);
                }
                finally
                {
                    cmd.Parameters.Clear();
                }
            }
        }

        /// <summary>
        /// Gets the results as DataTable for a query
        /// </summary>
        /// <param name="command">the command that serves the requested data</param>
        /// <param name="parameters">the parameters for the query</param>
        /// <returns>a DataTable with the requested data</returns>
        public DataTable GetResults(string command, params IDbDataParameter[] parameters)
        {
            using (IDbCommand cmd = GetCommand(command, parameters))
            {
                try
                {
                    IDbDataAdapter adapter = GetAdapter(cmd);
                    DataSet set = new DataSet();
                    adapter.Fill(set);
                    return set.Tables[0];
                }
                finally
                {
                    cmd.Parameters.Clear();
                }
            }
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
            if (!(value is IEnumerable) || (value is string))
            {
                return new SqlParameter(string.Format(SqlCommands.SqlParameterPrefix, name), value ?? DBNull.Value)
                {
                    IsNullable = value == null
                };
            }
            else
            {
                DataTable dtab;
                if (value is IEnumerable<DynamicResult> && typeName != null)
                {
                    IEnumerable<DynamicResult> val = (IEnumerable<DynamicResult>) value;
                    dtab = val.ToDataTable();
                }
                else if (value is IEnumerable<DynamicResult>)
                {

                    throw new ArgumentException("TypeName is required when passing structured data using DynamicResult!");
                }
                else
                {
                    IEnumerable val = (IEnumerable) value;
                    dtab = val.ToDataTable(out typeName);
                }

                SqlParameter retVal = new SqlParameter(string.Format(SqlCommands.SqlParameterPrefix, name), dtab);
                retVal.SqlDbType = SqlDbType.Structured;
                retVal.TypeName = typeName;
                return retVal;
            }
        }

        /// <summary>
        /// Gets the Type representation of a SQL - Typename
        /// </summary>
        /// <param name="typeName">the name of the sql type</param>
        /// <returns>the type representation of the demanded type</returns>
        public Type GetType(string typeName)
        {
            switch (typeName.ToLower())
            {
                case "varchar":
                case "nvarchar":
                case "text":
                case "char":
                case "nchar":
                case "ntext":
                    {
                        return typeof(string);
                    }
                case "bigint":
                    {
                        return typeof(long);
                    }
                case "bit":
                    {
                        return typeof(bool);
                    }
                case "date":
                case "datetime":
                case "datetime2":
                case "smalldatetime":
                    {
                        return typeof(DateTime);
                    }
                case "decimal":
                case "money":
                case "smallmoney":
                case "numeric":
                case "real":
                    {
                        return typeof(decimal);
                    }
                case "float":
                    {
                        return typeof(float);
                    }
                case "int":
                    {
                        return typeof(int);
                    }
                case "smallint":
                    {
                        return typeof(short);
                    }
                case "tinyInt":
                    {
                        return typeof(byte);
                    }
            }

            throw new ArgumentException(string.Format(Messages.UnknownTypeMessage, typeName), "typeName");
        }

        /// <summary>
        /// Gets the last index written to the database
        /// </summary>
        /// <typeparam name="T">the type of the index-value</typeparam>
        /// <param name="tableName">the table in which a record was stored</param>
        /// <returns>the indexvalue assigned by the database</returns>
        public T GetLastIndex<T>(string tableName)
        {
            return ExecuteCommandScalar<T>(SqlCommands.GetScopeIdentity);
        }

        /// <summary>
        /// Begins a transaction which can be used to rollback manipulations or to commit them when a processingblock ends
        /// </summary>
        /// <returns>an ITransaction object used to control the transaction</returns>
        public ITransaction AcquireTransaction()
        {
            if (localTransaction == null)
            {
                ITransaction retVal = new Transaction(localTransaction = connection.BeginTransaction(), localLock);
                retVal.Disposed += (o, e)=>{localTransaction = null;};
                return retVal;
            }

            throw new Exception(Messages.OpenTransactionFound);
        }

        /// <summary>
        /// Gets a command for the current connection object
        /// </summary>
        /// <param name="command">the command to be executed</param>
        /// <param name="parameters">the parameters to pass to the command</param>
        /// <returns>the created command</returns>
        private IDbCommand GetCommand(string command, params IDbDataParameter[] parameters)
        {
            if (!EnsureConnected())
            {
                throw new Exception("The connection was required to close and re-open. the open transaction has to be rolled back!");
            }

            while (connection.State == ConnectionState.Executing || connection.State == ConnectionState.Fetching)
            {
                Thread.Sleep(10);
            }

            IDbCommand retVal = new SqlCommand(command, connection);
            if (commandTimeout != 0)
            {
                retVal.CommandTimeout = commandTimeout;
            }
            if (localTransaction != null)
            {
                retVal.Transaction = localTransaction;
            }

            retVal.CommandText = command;
            retVal.CommandType = CommandType.Text;
            parameters.ToList().ForEach(n =>retVal.Parameters.Add(n));
            return retVal;
        }

        /// <summary>
        /// Ensures the connection to be open before calling any procedure
        /// </summary>
        /// <returns>a value indicating whether the connection can be used without expecting an error. This is the case when there is no open transaction</returns>
        private bool EnsureConnected()
        {
            bool retVal = true;
            if (connection.State == ConnectionState.Closed || connection.State == ConnectionState.Broken)
            {
                retVal = localTransaction == null;
                localTransaction = null;
                try
                {
                    if (connection.State != ConnectionState.Closed)
                    {
                        connection.Close();
                    }

                    connection.Open();
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent(ex.ToString(), LogSeverity.Error, "SqlServer");
                }

                if (connection.State != ConnectionState.Open)
                {
                    try
                    {
                        connection.Dispose();
                        connection = null;
                    }
                    catch (Exception ex)
                    {
                        LogEnvironment.LogEvent(ex.ToString(), LogSeverity.Error, "SqlServer");
                    }
                    finally
                    {
                        bool success = false;
                        while (!success)
                        {
                            try
                            {
                                connection = new SqlConnection(connectionString);
                                connection.Open();
                                success = true;
                            }
                            catch (Exception ex)
                            {
                                LogEnvironment.LogEvent(ex.ToString(), LogSeverity.Error, "SqlServer");
                            }
                            finally
                            {
                                if (connection != null && !success)
                                {
                                    try
                                    {
                                        connection.Dispose();
                                        connection = null;
                                    }
                                    catch (Exception ex)
                                    {
                                        LogEnvironment.LogEvent(ex.ToString(), LogSeverity.Error, "SqlServer");
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return retVal;
        }

        /// <summary>
        /// Gets a data adapter for the provided command
        /// </summary>
        /// <param name="command">the command for which to generate a data adapter</param>
        /// <returns>a data-adapter for the given command</returns>
        private IDbDataAdapter GetAdapter(IDbCommand command)
        {
            return new SqlDataAdapter((SqlCommand) command);
        }
    }
}
