using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Decisions.DefaultConstraints;
using ITVComponents.Decisions.Entities.Results;
using ITVComponents.EFRepo.Extensions;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;

namespace ITVComponents.Decisions.Entities
{
    public class EntityConstraint<T>:ScriptedConstraint<T> where T : class
    {
        private DbContext database;

        public string Schema { get; private set; }

        public string TableName { get; private set; }

        /// <summary>
        /// Initializes a new instance of the EntityConstraint class
        /// </summary>
        /// <param name="database">the EntityFramework Context for this constraint</param>
        /// <param name="expression">the expression that is used to evaluate the given expression</param>
        /// <param name="mode">the expressionmode of this expression</param>
        /// <param name="schema">the entitySchema</param>
        /// <param name="tableName">the Tablename of the given entity</param>
        public EntityConstraint(DbContext database ,string expression, ConstraintExpressionMode mode, string schema, string tableName) : base(expression, mode)
        {
            Schema = schema;
            TableName = tableName;
            this.database = database;
        }

        /// <summary>
        /// Initializes a new instance of the EntityConstraint class
        /// </summary>
        /// <param name="database">the EntityFramework Context for this constraint</param>
        /// <param name="expression">the expression that is used to evaluate the given expression</param>
        /// <param name="mode">the expressionmode of this expression</param>
        /// <param name="schema">the entitySchema</param>
        /// <param name="tableName">the Tablename of the given entity</param>
        /// <param name="constraintDescription">Describes what this constraint will examine</param>
        public EntityConstraint(DbContext database, string expression, ConstraintExpressionMode mode, string schema, string tableName, string constraintDescription) : base(expression, mode, constraintDescription)
        {
            Schema = schema;
            TableName = tableName;
            this.database = database;
        }

        /// <summary>
        /// Initializes a new instance of the EntityConstraint class
        /// </summary>
        /// <param name="database">the EntityFramework Context for this constraint</param>
        /// <param name="expression">the expression that is used to evaluate the given expression</param>
        /// <param name="mode">the expressionmode of this expression</param>
        /// <param name="schema">the entitySchema</param>
        /// <param name="tableName">the Tablename of the given entity</param>
        /// <param name="additionalVariables">additional variables that are provided to ensure the ability to verify a constraint</param>
        public EntityConstraint(DbContext database, string expression, ConstraintExpressionMode mode, string schema, string tableName, IDictionary<string, object> additionalVariables) : base(expression, mode, additionalVariables)
        {
            Schema = schema;
            TableName = tableName;
            this.database = database;
        }

        /// <summary>
        /// Initializes a new instance of the EntityConstraint class
        /// </summary>
        /// <param name="database">the EntityFramework Context for this constraint</param>
        /// <param name="expression">the expression that is used to evaluate the given expression</param>
        /// <param name="mode">the expressionmode of this expression</param>
        /// <param name="schema">the entitySchema</param>
        /// <param name="tableName">the Tablename of the given entity</param>
        /// <param name="additionalVariables">additional variables that are provided to ensure the ability to verify a constraint</param>
        /// <param name="constraintDescription">Describes what this constraint will examine</param>
        public EntityConstraint(DbContext database, string expression, ConstraintExpressionMode mode, string schema, string tableName, IDictionary<string, object> additionalVariables, string constraintDescription) : base(expression, mode, additionalVariables, constraintDescription)
        {
            Schema = schema;
            TableName = tableName;
            this.database = database;
        }

        #region Overrides of ScriptedConstraint<T>

        /// <summary>
        /// Prepares this variablescope. Override this method if you want to provide additional information of functions
        /// </summary>
        /// <param name="scope">the variablescope that is passed to the scripting engine when running a decision</param>
        protected override void PrepareVariables(Scope scope)
        {
            base.PrepareVariables(scope);
            scope["DescribeProcedure"] = new Func<string, T, ParameterDescriptor[]>(DescribeProcedure);
            scope["SimpleDecideProc"] = new Func<string, T, bool>(AutoProc);
            scope["FullDecideProc"] = new Func<string, T, SimpleResult>(AutoProcEx);
            scope["Exec"] = new Func<string, ParameterDescriptor[], SimpleResult>(RunProcedure);
            scope["SimpleDecideProcT"] = new Func<string, T, DecisionResult>(AutoProcT);
            scope["FullDecideProcT"] = new Func<string, T, SimpleTriStateResult>(AutoProcExT);
            scope["ExecT"] = new Func<string, ParameterDescriptor[], SimpleTriStateResult>(RunProcedureT);
            scope["table"] = TableName;
            scope["schema"] = Schema;
        }

        /// <summary>
        /// Evaluates the provided storedprocedure and returns its result
        /// </summary>
        /// <param name="procedureName">the name of the stored procedure</param>
        /// <param name="entity">the entity for which to evaluate the parameters</param>
        /// <returns>a value indicating whether the stored procedure evaluated the given entity properly</returns>
        private bool AutoProc(string procedureName, T entity)
        {
            SimpleResult retVal = AutoProcEx(procedureName, entity);
            LastMessage = retVal.Message;
            return retVal.Ok;
        }

        /// <summary>
        /// Evaluates the provided storedprocedure and returns its result as TriState DecisionResult
        /// </summary>
        /// <param name="procedureName">the name of the stored procedure</param>
        /// <param name="entity">the entity for which to evaluate the parameters</param>
        /// <returns>a value indicating whether the stored procedure evaluated the given entity properly</returns>
        private DecisionResult AutoProcT(string procedureName, T entity)
        {
            SimpleTriStateResult retVal = AutoProcExT(procedureName, entity);
            LastMessage = retVal.Message;
            return retVal.Result;
        }

        /// <summary>
        /// Evaluates the StoredProcedure and returns the complete result of it
        /// </summary>
        /// <param name="procedureName">the storedprocedure to call</param>
        /// <param name="entity">the entity for which to evaluate this constraint</param>
        /// <returns>the complete value of the stored procedure</returns>
        private SimpleResult AutoProcEx(string procedureName, T entity)
        {
            ParameterDescriptor[] parameters = DescribeProcedure(procedureName, entity, true);
            SimpleResult retVal = RunProcedure(procedureName, parameters);
            return retVal;
        }

        /// <summary>
        /// Evaluates the StoredProcedure and returns the complete TriState result of it
        /// </summary>
        /// <param name="procedureName">the storedprocedure to call</param>
        /// <param name="entity">the entity for which to evaluate this constraint</param>
        /// <returns>the complete value of the stored procedure</returns>
        private SimpleTriStateResult AutoProcExT(string procedureName, T entity)
        {
            ParameterDescriptor[] parameters = DescribeProcedure(procedureName, entity, true);
            SimpleTriStateResult retVal = RunProcedureT(procedureName, parameters);
            return retVal;
        }

        /// <summary>
        /// Runs a Procedure with the given name and parameters
        /// </summary>
        /// <param name="procedureName">the procedurename without schema</param>
        /// <param name="parameters">the parameters to pass to the procedure</param>
        /// <returns>a value containing the simple result of the procedure</returns>
        private SimpleResult RunProcedure(string procedureName, ParameterDescriptor[] parameters)
        {
            SqlParameter[] args = (from t in parameters
                where !t.IsOptional || t.ParameterValue != null
                select
                    new SqlParameter
                    {
                        ParameterName = t.ParameterName,
                        Direction = !t.IsRef ? ParameterDirection.Input : ParameterDirection.InputOutput,
                        Value = t.ParameterValue ?? DBNull.Value
                    }).ToArray();

            using (database.Database.UseConnection(out DbCommand cmd))
            {
                cmd.CommandText = $"[{Schema}].[{procedureName}]";
                cmd.Parameters.AddRange(args);
                cmd.CommandType = CommandType.StoredProcedure;
                SimpleResult retVal = new SimpleResult {Ok = false};
                bool doClose = false;
                using (var reader = cmd.ExecuteReader())
                {
                    bool first = true;
                    bool messageRead = false;
                    while (reader.Read())
                    {
                        if (first)
                        {
                            retVal.Ok = reader.GetBoolean(0);
                            if (reader.FieldCount >= 2)
                            {
                                if (!reader.IsDBNull(1))
                                {
                                    retVal.Message = reader.GetString(1);
                                }

                                messageRead = true;
                            }

                            first = false;
                        }
                    }

                    (from t in parameters
                        join a in args on t.ParameterName equals a.ParameterName
                        where t.IsRef
                        select new {Param = t, Arg = a}).ForEach(n => n.Param.ParameterValue = n.Arg.Value);
                    if (!messageRead)
                    {
                        ParameterDescriptor desc =
                            parameters.FirstOrDefault(
                                n =>
                                    n.IsRef &&
                                    n.ParameterName.Equals("@message", StringComparison.OrdinalIgnoreCase));
                        if (desc != null)
                        {
                            retVal.Message = desc.ParameterValue as string;
                        }
                    }
                }

                return retVal;
            }
        }

        /// <summary>
        /// Runs a Procedure with the given name and parameters and returns a SimpleTriStateResult
        /// </summary>
        /// <param name="procedureName">the procedurename without schema</param>
        /// <param name="parameters">the parameters to pass to the procedure</param>
        /// <returns>a value containing the simple result of the procedure</returns>
        private SimpleTriStateResult RunProcedureT(string procedureName, ParameterDescriptor[] parameters)
        {
            SqlParameter[] args = (from t in parameters
                where !t.IsOptional || t.ParameterValue != null
                select
                    new SqlParameter
                    {
                        ParameterName = t.ParameterName,
                        Direction = !t.IsRef ? ParameterDirection.Input : ParameterDirection.InputOutput,
                        Value = t.ParameterValue ?? DBNull.Value
                    }).ToArray();

            using (database.Database.UseConnection(out DbCommand cmd))
            {
                cmd.CommandText = $"[{Schema}].[{procedureName}]";
                cmd.Parameters.AddRange(args);
                cmd.CommandType = CommandType.StoredProcedure;
                SimpleTriStateResult retVal = new SimpleTriStateResult {Result = DecisionResult.None};
                bool doClose = false;

                using (var reader = cmd.ExecuteReader())
                {
                    bool first = true;
                    bool messageRead = false;
                    while (reader.Read())
                    {
                        if (first)
                        {
                            retVal.Result = (DecisionResult) reader.GetInt32(0);
                            if (reader.FieldCount >= 2)
                            {
                                if (!reader.IsDBNull(1))
                                {
                                    retVal.Message = reader.GetString(1);
                                }

                                messageRead = true;
                            }

                            first = false;
                        }
                    }

                    (from t in parameters
                        join a in args on t.ParameterName equals a.ParameterName
                        where t.IsRef
                        select new {Param = t, Arg = a}).ForEach(n => n.Param.ParameterValue = n.Arg.Value);
                    if (!messageRead)
                    {
                        ParameterDescriptor desc =
                            parameters.FirstOrDefault(
                                n =>
                                    n.IsRef &&
                                    n.ParameterName.Equals("@message", StringComparison.OrdinalIgnoreCase));
                        if (desc != null)
                        {
                            retVal.Message = desc.ParameterValue as string;
                        }
                    }
                }

                return retVal;
            }
        }

        /// <summary>
        /// Describes the parameters of a procedure
        /// </summary>
        /// <param name="procedureName">the name of the requested procedure</param>
        /// <param name="entity">the entity for which pre-evaluate the parameters of the procedure</param>
        /// <returns>a list of parameters that can be passed to the storedprocedure</returns>
        private ParameterDescriptor[] DescribeProcedure(string procedureName, T entity)
        {
            return DescribeProcedure(procedureName, entity, false);
        }

        /// <summary>
        /// Describes the provided procedure for the given Entity object
        /// </summary>
        /// <param name="procedureName">the name of the procedure to call</param>
        /// <param name="entity">the Entity-object</param>
        /// <param name="throwOnError">indicates whether to throw an error if the parameter is not part of the provided entity</param>
        /// <returns>an array of parameter-descriptors for the parameters of the provided procedure</returns>
        private ParameterDescriptor[] DescribeProcedure(string procedureName, T entity, bool throwOnError)
        {
            ParameterDescriptor[] retVal =
                database.Database.SqlQuery<ParameterDescriptor>(
                    @"select a.name as ParameterName, a.is_output as IsRef, a.has_default_value as IsOptional from sys.schemas s inner join  sys.procedures p on p.schema_id = s.schema_id
inner join sys.parameters a on a.object_id = p.object_id
where s.name = @p0 and p.name = @p1
order by a.parameter_id",new SqlParameter("@p0",Schema), new SqlParameter("@p1",procedureName)
                    ).ToArray();
            Type t = typeof(T);
            foreach (ParameterDescriptor param in retVal)
            {
                PropertyInfo inf = t.GetProperty(param.ParameterName.Substring(1));
                if (inf == null && !param.IsOptional && !param.IsRef && throwOnError)
                {
                    throw new ArgumentException($"The demanded Parameter {param.ParameterName} is not part of the provided Entity");
                }

                if (inf != null)
                {
                    object value = inf.GetValue(entity);
                    param.ParameterValue = value;
                }
            }

            return retVal;
        }

        #endregion

        public class ParameterDescriptor
        {
            public string ParameterName { get; set; }

            [NotMapped]
            public object ParameterValue { get; set; }

            public bool IsRef { get; set; }

            public bool IsOptional { get; set; }
        }
    }
}
