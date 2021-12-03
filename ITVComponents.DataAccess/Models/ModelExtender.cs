using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Text;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Helpers;

namespace ITVComponents.DataAccess.Models
{
    /// <summary>
    /// Provides extension Methods for IDataReaders that enable a user to convert dataReaders directly to instances of a specific model
    /// </summary>
    public static class ModelExtender
    {
        /// <summary>
        /// Gets the model for this DataReader's currents result 
        /// </summary>
        /// <typeparam name="T">the targetType into which to convert the reader's value</typeparam>
        /// <param name="reader">the reader having a current result open</param>
        /// <param name="rules">a set of rules that applies to the current type</param>
        /// <param name="fieldNames">a set of names provided by the current reader</param>
        /// <returns>an instance representing the value of the current result of the reader</returns>
        internal static T GetModel<T>(this IDataReader reader, MapRule[] rules, string[] fieldNames) where T : new()
        {
            T retVal = new T();
            for (int i = 0; i < rules.Length; i++)
            {
                MapRule r = rules[i];
                if (r != null && fieldNames.Any(n =>  n.Equals(r.FieldName,StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        object val = reader[r.FieldName];
                        if (!r.UseExpression)
                        {
                            r[retVal] = val;
                        }
                        else
                        {
                            r[retVal] = ProcessResolveExpression(r.ValueResolveExpression, val);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogEnvironment.LogEvent($"Error during model bind! {ex.OutlineException()}",LogSeverity.Warning);
                    }
                }
                else if (r != null)
                {
                    rules[i] = null;
                    LogEnvironment.LogDebugEvent($"No Value found for MapRule {r.FieldName} ({r.UseExpression};{r.ValueResolveExpression}", LogSeverity.Report);
                }
            }

            return retVal;
        }

        /// <summary>
        /// Gets the model for this DataReader's currents result 
        /// </summary>
        /// <param name="reader">the reader having a current result open</param>
        /// <param name="targetType">the targetType into which to convert the reader's value</param>
        /// <param name="rules">a set of rules that applies to the current type</param>
        /// <param name="fieldNames">a set of names provided by the current reader</param>
        /// <returns>an instance representing the value of the current result of the reader</returns>
        internal static object GetModel(this IDataReader reader, Type targetType, MapRule[] rules, string[] fieldNames)
        {
            object retVal = targetType.GetConstructor(Type.EmptyTypes).Invoke(null);
            for (int i = 0; i < rules.Length; i++)
            {
                MapRule r = rules[i];
                if (r != null && fieldNames.Any(n => n.Equals(r.FieldName,StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        object val = reader[r.FieldName];
                        if (!r.UseExpression)
                        {
                            r[retVal] = val;
                        }
                        else
                        {
                            r[retVal] = ProcessResolveExpression(r.ValueResolveExpression, val);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogEnvironment.LogEvent($"Error during model bind! {ex.OutlineException()}", LogSeverity.Warning);
                    }
                }
                else if (r != null)
                {
                    LogEnvironment.LogDebugEvent($"No Value found for MapRule {r.FieldName} ({r.UseExpression};{r.ValueResolveExpression}", LogSeverity.Report);
                }
            }

            return retVal;
        }

        /// <summary>
        /// Returns an enuerable with entites representing all values of this reader
        /// </summary>
        /// <typeparam name="T">the target type in which to convert all items</typeparam>
        /// <typeparam name="TM">the Mapping Type from which to use the mapping for the results</typeparam>
        /// <param name="reader">the reader from which to fetch all results</param>
        /// <returns>an enuerable representing all items</returns>
        public static IEnumerable<T> GetModelResult<T, TM>(this IDataReader reader) where T : new()
        {
            MapRule[] rules = DbColumnAttribute.GetRules(typeof(TM)).Clone() as MapRule[];
            string[] fieldNames = new string[reader.FieldCount];
            for (int i = 0; i < fieldNames.Length; i++)
            {
                fieldNames[i] = reader.GetName(i).ToLower();
            }

            try
            {
                while (reader.Read())
                {
                    yield return reader.GetModel<T>(rules, fieldNames);
                }
            }
            finally
            {
                reader.Dispose();
            }
        }

        /// <summary>
        /// Returns an enuerable with entites representing all values of this reader
        /// </summary>
        /// <typeparam name="T">the target type in which to convert all items</typeparam>
        /// <typeparam name="TM">the Mapping Type from which to use the mapping for the results</typeparam>
        /// <param name="data">the fetched data from which to convert all results</param>
        /// <returns>an enuerable representing all items</returns>
        public static IEnumerable<T> GetModelResult<T, TM>(this DynamicResult[] data) where T : new()
        {
            foreach (DynamicResult item in data)
            {
                yield return item.GetModelResult<T, TM>();
            }
        }

        /// <summary>
        /// Gets the modelResult from a specific dynamicResult object
        /// </summary>
        /// <typeparam name="T">the target model in which to convert the dynamicResult</typeparam>
        /// <typeparam name="TM">the Meta-Type used for the mapping</typeparam>
        /// <param name="item">the DynamicResult that contains the original data</param>
        /// <returns>an instance of the desired type</returns>
        public static T GetModelResult<T, TM>(this DynamicResult item) where T : new()
        {
            T retVal = new T();
            item.ToModel<T, TM>(retVal);
            return retVal;
        }

        /// <summary>
        /// Maps the data of the dynamicResult instance into the T - Instance
        /// </summary>
        /// <typeparam name="T">the type into which to convert the data</typeparam>
        /// <typeparam name="TM">the meta-type used to identify mapped columns</typeparam>
        /// <param name="item">the item that contains the original data</param>
        /// <param name="target">the target instance into which the data is mapped</param>
        public static void ToModel<T, TM>(this DynamicResult item, T target)
        {
            string[] fieldNames = item.GetDynamicMemberNames().ToArray();
            MapRule[] rules = DbColumnAttribute.GetRules(typeof(TM)).Clone() as MapRule[];
            for (int i = 0; i < rules.Length; i++)
            {
                MapRule r = rules[i];
                if (r != null && fieldNames.Any(n=>n.Equals(r.FieldName,StringComparison.OrdinalIgnoreCase)))//Array.IndexOf(fieldNames, r.FieldName.ToLower()) != -1))))
                {
                    try
                    {
                        object val = item[r.FieldName];
                        if (!r.UseExpression)
                        {
                            r[target] = val;
                        }
                        else
                        {
                            r[target] = ProcessResolveExpression(r.ValueResolveExpression, val);
                        }
                    }
                    catch(Exception ex)
                    {
                        LogEnvironment.LogEvent(string.Format("Failed to set value\r\n{0}", ex.OutlineException()),LogSeverity.Warning, "DataAccess");
                    }
                }
                else if (r != null)
                {
                    LogEnvironment.LogDebugEvent($"No Value found for MapRule {r.FieldName} ({r.UseExpression};{r.ValueResolveExpression}", LogSeverity.Report);
                }
            }
        }

        public static ExpandoObject ToExpando(this DynamicResult item, bool extendWithUppercase)
        {
            ExpandoObject eob = new ExpandoObject();
            IDictionary<string,object> eoc = eob;
            CopyToDictionary(item, eoc, extendWithUppercase);

            return eob;
        }

        public static IDictionary<string, object> ToDictionary(this DynamicResult item, bool caseInsensitive)
        {
            var retVal =
                new Dictionary<string, object>(caseInsensitive
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.CurrentCulture);
            CopyToDictionary(item, retVal, false);
            return retVal;
        }

        /// <summary>
        /// Maps the data of the dynamicResult instance into the T - Instance
        /// </summary>
        /// <param name="item">the item that contains the original data</param>
        /// <param name="target">the target instance into which the data is mapped</param>
        /// <param name="modelType">the meta-type used to identify mapped columns</param>
        public static void ToModel(this DynamicResult item, object target, Type modelType)
        {
            string[] fieldNames = item.GetDynamicMemberNames().ToArray();
            MapRule[] rules = DbColumnAttribute.GetRules(modelType).Clone() as MapRule[];
            for (int i = 0; i < rules.Length; i++)
            {
                MapRule r = rules[i];
                if (r != null && fieldNames.Any(n => n.Equals(r.FieldName, StringComparison.OrdinalIgnoreCase)))//Array.IndexOf(fieldNames, r.FieldName.ToLower()) != -1))))
                {
                    try
                    {
                        object val = item[r.FieldName];
                        if (!r.UseExpression)
                        {
                            r[target] = val;
                        }
                        else
                        {
                            r[target] = ProcessResolveExpression(r.ValueResolveExpression, val);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogEnvironment.LogEvent(string.Format("Failed to set value\r\n{0}", ex.OutlineException()), LogSeverity.Warning, "DataAccess");
                    }
                }
                else if (r != null)
                {
                    LogEnvironment.LogDebugEvent($"No Value found for MapRule {r.FieldName} ({r.UseExpression};{r.ValueResolveExpression}", LogSeverity.Report);
                }
            }
        }

        /// <summary>
        /// Returns an enuerable with entites representing all values of this reader
        /// </summary>
        /// <param name="reader">the reader from which to fetch all results</param>
        /// <param name="targetType">the targetType into which to convert the reader's value</param>
        /// <param name="metaType">the type delcaring meta - information that is capable to assign data on instances of the targetType</param>
        /// <returns>an enuerable representing all items</returns>
        public static IEnumerable<object> GetModelResult(this IDataReader reader, Type targetType, Type metaType)
        {
            MapRule[] rules = DbColumnAttribute.GetRules(metaType).Clone() as MapRule[];
            string[] fieldNames = new string[reader.FieldCount];
            for (int i = 0; i < fieldNames.Length; i++)
            {
                fieldNames[i] = reader.GetName(i).ToLower();
            }

            try
            {
                while (reader.Read())
                {
                    yield return reader.GetModel(targetType, rules, fieldNames);
                }
            }
            finally
            {
                reader.Dispose();
            }
        }

        public static IEnumerable<ExpandoObject> GetExpandoResults(this DynamicResult[] data, bool extendWithUppercase)
        {
            foreach (DynamicResult result in data)
            {
                var retVal = result.ToExpando(extendWithUppercase);
                yield return retVal;
            }
        }

        public static IEnumerable<IDictionary<string, object>> GetDictionaryResults(this DynamicResult[] data,
            bool caseInsensitive)
        {
            foreach (DynamicResult result in data)
            {
                var retVal = result.ToDictionary(caseInsensitive);
                yield return retVal;
            }
        }

        /// <summary>
        /// Returns an enuerable with entites representing all values of this reader
        /// </summary>
        /// <param name="data">the data that was fetched from the underlaying datasource</param>
        /// <param name="targetType">the targetType into which to convert the reader's value</param>
        /// <param name="metaType">the type delcaring meta - information that is capable to assign data on instances of the targetType</param>
        /// <returns>an enuerable representing all items</returns>
        public static IEnumerable<object> GetModelResult(this DynamicResult[] data, Type targetType, Type metaType)
        {
            MapRule[] rules = DbColumnAttribute.GetRules(metaType).Clone() as MapRule[];
            foreach (DynamicResult result in data)
            {
                object retVal = targetType.GetConstructor(Type.EmptyTypes).Invoke(null);
                result.ToModel(retVal, metaType);
                yield return retVal;
            }
        }

        /// <summary>
        /// Resolves an expression-resolved ColumnMapping
        /// </summary>
        /// <param name="expression">the expression that was provided for the mapping</param>
        /// <param name="value">the value of the provided columnname</param>
        /// <returns>the result of the Expression</returns>
        private static object ProcessResolveExpression(string expression, object value)
        {
            Dictionary<string, object> variables = new Dictionary<string, object> {{"value", value}};
            var retVal = ExpressionParser.Parse(expression, variables, a => { DefaultCallbacks.PrepareDefaultCallbacks(a.Scope, a.ReplSession); });
            return retVal;
        }

        private static void CopyToDictionary(DynamicResult item, IDictionary<string, object> target, bool extendWithUppercase)
        {
            string[] fieldNames = item.GetDynamicMemberNames().ToArray();
            foreach (var fieldName in fieldNames)
            {
                target.Add(fieldName, item[fieldName]);
                if (extendWithUppercase && fieldName.ToUpper() != fieldName)
                {
                    target.Add(fieldName.ToUpper(), item[fieldName]);
                }
            }
        }
    }
}
