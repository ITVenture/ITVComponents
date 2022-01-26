using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Dynamitey.DynamicObjects;
using ITVComponents.EFRepo.Extensions;
using ITVComponents.EFRepo.Helpers;
using ITVComponents.Formatting.PluginSystemExtensions.Configuration;
using ITVComponents.Logging;
using ITVComponents.Scripting.CScript.Core.Native;
using ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Microsoft.VisualBasic;
using Newtonsoft.Json.Serialization;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Extensions
{
    public static class ContextExtensions
    {
        private const string RosFkConfig = "ROSFK";
        private const string RosDiagConfig = "ROSDIAG";

        static ContextExtensions()
        {
            NativeScriptHelper.AddReference(RosFkConfig, "--ROSLYN--");
            NativeScriptHelper.AddReference(RosFkConfig, "ITVComponents.WebCoreToolkit.EntityFramework");
            NativeScriptHelper.AddUsing(RosFkConfig, "ITVComponents.WebCoreToolkit.EntityFramework.Models");
            NativeScriptHelper.AddUsing(RosFkConfig, "ITVComponents.WebCoreToolkit.EntityFramework.Helpers");
            NativeScriptHelper.AddUsing(RosFkConfig, "ITVComponents.Helpers");
            NativeScriptHelper.RunLinqQuery(RosFkConfig, new[] { "Fubar" }, "Fubar", "return null;", new Dictionary<string, object>());
            NativeScriptHelper.AddReference(RosDiagConfig, "--ROSLYN--");
            NativeScriptHelper.AddReference(RosDiagConfig, "ITVComponents.WebCoreToolkit.EntityFramework");
            NativeScriptHelper.AddReference(RosDiagConfig, "ITVComponents.Decisions.Entities");
            NativeScriptHelper.AddUsing(RosDiagConfig, "ITVComponents.WebCoreToolkit.EntityFramework.Models");
            NativeScriptHelper.AddUsing(RosDiagConfig, "ITVComponents.Decisions");
            NativeScriptHelper.AddUsing(RosDiagConfig, "ITVComponents.Decisions.Entities.Results");
            NativeScriptHelper.RunLinqQuery(RosDiagConfig, new[] { "Fubar" }, "Fubar", "return null;", new Dictionary<string, object>());
        }

        /// <summary>
        /// Dummy-Method for Initializing the RosFK Linq-Context
        /// </summary>
        public static void Init()
        {
        }

        public static IEnumerable ReadForeignKey(this DbContext context, string tableName, IServiceProvider services, string id = null, Dictionary<string, object> postedFilter = null)
        {
            if (context is IForeignKeyProvider provider)
            {
                IEnumerable retVal;
                if (postedFilter == null && id == null)
                {
                    retVal = provider.GetForeignKeyFilterQuery(tableName);
                }
                else if (id != null)
                {
                    retVal = provider.GetForeignKeyResolveQuery(tableName, id);
                }
                else
                {
                    retVal = provider.GetForeignKeyFilterQuery(tableName, postedFilter);
                }

                if (retVal != null)
                {
                    return retVal;
                }
            }

            if (id == null)
            {
                var query = CreateRawQuery(context, tableName, postedFilter, services, out var filterDecl);
                var typeName = context.GetType().Name;
                query = $@"{typeName} db = Global.Db;
{filterDecl}
            return {query}";
                LogEnvironment.LogDebugEvent(query, LogSeverity.Report);
                //query = string.Format(query, $"t.{labelColumn}.Contains(filter)");
                return RunQuery(context, query, RosFkConfig, postedFilter);
            }
            else
            {
                var query = CreateRawResolveQuery(context, tableName, services);
                var typeName = context.GetType().Name;
                query = $@"{typeName} db = Global.Db;
            {query}";
                //query = string.Format(query, $"t.{labelColumn}.Contains(filter)");
                return RunQuery(context, query, RosFkConfig, new Dictionary<string, object> { { "Id", id } });
            }
        }

        public static IEnumerable RunDiagnosticsQuery(this DbContext context, DiagnosticsQueryDefinition query, IDictionary<string, string> arguments)
        {
            var queryText = CreateDiagQuery(context, query, arguments, out var args);
            return RunQuery(context, queryText, RosDiagConfig, args);
        }

        public static IEnumerable RunDiagnosticsQuery(this DbContext context, DiagnosticsQueryDefinition query, IDictionary<string, object> arguments)
        {
            var args = new Dictionary<string, object>(arguments);
            var queryText = CreateDiagQuery(context, query, args);
            return RunQuery(context, queryText, RosDiagConfig, args);
        }

        private static IEnumerable RunQuery(DbContext context, string query, string configName, IDictionary<string, object> data)
        {
            return (IEnumerable)NativeScriptHelper.RunLinqQuery(configName, context, "Db", query, data ?? new Dictionary<string, object>());
        }

        private static string CreateDiagQuery(DbContext context, DiagnosticsQueryDefinition query, IDictionary<string, object> arguments)
        {
            ConfigureLinqForContext(context, RosDiagConfig, out var contextType);
            StringBuilder fullQuery = new StringBuilder($@"{contextType.Name} db = Global.Db;
");
            DiagnoseQueryHelper.VerifyArguments(query, arguments, fullQuery);
            fullQuery.AppendLine($"{(query.AutoReturn ? "return " : "")}{query.QueryText}{(query.AutoReturn ? ";" : "")}");
            return fullQuery.ToString();
        }

        private static string CreateDiagQuery(DbContext context, DiagnosticsQueryDefinition query, IDictionary<string, string> arguments, out IDictionary<string, object> queryArguments)
        {
            ConfigureLinqForContext(context, RosDiagConfig, out var contextType);
            StringBuilder fullQuery = new StringBuilder($@"{contextType.Name} db = Global.Db;
");
            queryArguments = new Dictionary<string, object>();
            DiagnoseQueryHelper.BuildArguments(query, arguments, queryArguments, fullQuery);
            fullQuery.AppendLine($"{(query.AutoReturn ? "return " : "")}{query.QueryText}{(query.AutoReturn ? ";" : "")}");
            return fullQuery.ToString();
        }

        private static string CreateRawQuery(DbContext context, string tableName, IDictionary<string, object> postedFilter, IServiceProvider services, out string filterDecl)
        {
            filterDecl = null;
            var keyPropertyType = GetKeyType(context, tableName, services, out var keyColumn, out var tableType, out var isKeyless, out var accessible);
            if (tableType != null)
            {
                if (!accessible)
                {
                    throw new SecurityException($"Access denied for Table {tableName}!");
                }

                StringBuilder where = new StringBuilder();
                var selAttr = tableType.GetCustomAttributes(typeof(ForeignKeySelectionAttribute), true);
                if (selAttr.Length == 0)
                {
                    var firstStringCol = tableType.GetProperties().FirstOrDefault(n => n.PropertyType == typeof(string));
                    if (firstStringCol == null)
                    {
                        throw new InvalidOperationException("Unable to process entities that have no string-columns!");
                    }

                    where.Append(CreateWhereClause(tableType, postedFilter, out filterDecl));

                    //return $@"from t in db.{tableName} orderby t.{firstStringCol.Name} select new ForeignKeyData<{keyPropertyType}>{{{{Key=t.{keyColumn}, Label=t.{firstStringCol.Name}}}}};";
                    return $@"from t in db.{tableName} {where} orderby t.{firstStringCol.Name} select new ForeignKeyData<{keyPropertyType}>{{Key={(!isKeyless ? $"t.{keyColumn}" : "\"-\"")}, Label=t.{firstStringCol.Name}, FullRecord=t.ToDictionary(true)}};";
                }

                var att = (ForeignKeySelectionAttribute)selAttr[0];
                if (postedFilter != null && att.FilterKeys != null && att.FilterKeys.Length != 0)
                {
                    var filterDcl = new StringBuilder();
                    where.Clear();
                    for (var index = 0; index < att.FilterKeys.Length; index++)
                    {
                        var s = att.FilterKeys[index];
                        var dec = att.FilterDeclarations[index];
                        var flt = att.Filters[index];
                        if (postedFilter.ContainsKey(s) && postedFilter[s] != null)
                        {
                            filterDcl.Append($@"{dec}
");
                            where.Append($"{(where.Length != 0 ? " && " : "")}{flt}");
                        }
                    }

                    filterDecl = filterDcl.ToString();
                    if (where.Length != 0)
                    {
                        where.Insert(0, "where ");
                    }
                }
                else if (postedFilter != null)
                {
                    where.Clear();
                    where.Append(CreateWhereClause(tableType, postedFilter, out filterDecl));
                }

                return $"from t in db.{tableName} {where} {att.OrderByExpression} select {att.CompleteSelect};";
            }

            throw new InvalidOperationException("Table-Type was not found!");
        }

        private static string CreateWhereClause(Type tableType, IDictionary<string, object> postedFilter, out string filterDecl)
        {
            var retVal = new StringBuilder();
            var availableFilterValues = tableType.GetProperties().Where(n => n.PropertyType == typeof(string) || n.PropertyType.IsValueType).ToArray();
            var filterDl = new StringBuilder();
            if (postedFilter != null && availableFilterValues.Length != 0)
            {
                var generalWhere = new StringBuilder();
                var generalFilter = "";
                var specialWhere = new StringBuilder();
                if (postedFilter.ContainsKey("Filter") && postedFilter["Filter"] != null)
                {
                    generalFilter = ".Contains(Filter)";
                    filterDl.Append(@"string Filter = Global.Filter;
");
                }

                for (var index = 0; index < availableFilterValues.Length; index++)
                {
                    var prop = availableFilterValues[index];
                    if (postedFilter.ContainsKey(prop.Name))
                    {
                        var typ = NativeScriptHelper.GetFriendlyName(prop.PropertyType);
                        var cvTyp = "";
                        var attr = (NullSensitiveFkQueryExpressionAttribute)Attribute.GetCustomAttributes(prop, typeof(NullSensitiveFkQueryExpressionAttribute)).FirstOrDefault();
                        var defaultNullSensitiveQuery = attr?.GetQueryPart($"t.{prop.Name}", $"Var{index}");
                        if (prop.PropertyType != typeof(string))
                        {
                            if (!typ.EndsWith("?"))
                            {
                                typ += "?";
                            }

                            cvTyp = typ.Substring(0, typ.Length - 1);
                            filterDl.Append($@"{typ} Var{index} = ValueConvertHelper.TryChangeType<{cvTyp}>((string)Global.{prop.Name});
bool Var{index}NullExpected = Global.{prop.Name}==""##NULL##"";
");
                            var nsq = defaultNullSensitiveQuery ?? $"t.{prop.Name} == Var{index}";
                            specialWhere.Append($"{(specialWhere.Length != 0 ? " && " : "")}(Var{index} == null && !Var{index}NullExpected || ({nsq}))");
                        }
                        else
                        {
                            filterDl.Append($@"string Var{index} = (string)Global.{prop.Name};
bool Var{index}NullExpected = Global.{prop.Name}==""##NULL##"";
");
                            var nsq = defaultNullSensitiveQuery ?? $"t.{prop.Name}.Contains(Var{index})";
                            specialWhere.Append($"{(specialWhere.Length != 0 ? " && " : "")}(Var{index}NullExpected && t.{prop.Name} == null || !Var{index}NullExpected && ({nsq}))");
                        }
                    }
                    else if (!string.IsNullOrEmpty(generalFilter))
                    {
                        if (prop.PropertyType == typeof(string))
                        {
                            generalWhere.Append($"{(generalWhere.Length != 0 ? " || " : "")}t.{prop.Name}{generalFilter}");
                        }
                        else
                        {
                            var typ = NativeScriptHelper.GetFriendlyName(prop.PropertyType);
                            var cvTyp = "";
                            if (!typ.EndsWith("?"))
                            {
                                typ += "?";
                            }

                            cvTyp = typ.Substring(0, typ.Length - 1);
                            filterDl.Append($@"{typ} Var{index} = ValueConvertHelper.TryChangeType<{cvTyp}>(Filter);
");
                            generalWhere.Append($"{(generalWhere.Length != 0 ? " || " : "")}(Var{index} != null && t.{prop.Name} == Var{index})");
                        }
                    }
                }

                retVal.Append(generalWhere);
                if (retVal.Length != 0)
                {
                    retVal.Insert(0, "(");
                    retVal.Append(")");
                }

                if (specialWhere.Length != 0)
                {
                    if (retVal.Length != 0)
                    {
                        retVal.Append(" && ");
                    }
                    retVal.Append(specialWhere);
                }

                if (retVal.Length != 0)
                {
                    retVal.Insert(0, "where ");
                }
            }

            filterDecl = filterDl.ToString();
            return retVal.ToString();
        }

        private static string CreateRawResolveQuery(DbContext context, string tableName, IServiceProvider services)
        {
            var keyPropertyType = GetKeyType(context, tableName, services, out var keyColumn, out var tableType, out var isKeyless, out var accessible);
            if (isKeyless)
            {
                throw new InvalidOperationException("Not supported for Keyless entities!");
            }

            if (!accessible)
            {
                throw new SecurityException($"Access denied for Table {tableName}!");
            }

            if (tableType != null)
            {
                var selAttr = tableType.GetCustomAttributes(typeof(ForeignKeySelectionAttribute), true);
                if (selAttr.Length == 0)
                {
                    var firstStringCol = tableType.GetProperties().FirstOrDefault(n => n.PropertyType == typeof(string));
                    if (firstStringCol == null)
                    {
                        throw new InvalidOperationException("Unable to process entities that have no string-columns!");
                    }

                    //return $@"from t in db.{tableName} orderby t.{firstStringCol.Name} select new ForeignKeyData<{keyPropertyType}>{{{{Key=t.{keyColumn}, Label=t.{firstStringCol.Name}}}}};";
                    return $@"{keyPropertyType} Id = ValueConvertHelper.TryChangeType<{keyPropertyType}>((string)Global.Id)??default({keyPropertyType});
return from t in db.{tableName} where t.{keyColumn} == Id select new ForeignKeyData<{keyPropertyType}>{{Key=t.{keyColumn}, Label=t.{firstStringCol.Name}, FullRecord=t.ToDictionary(true)}};";
                }

                var att = (ForeignKeySelectionAttribute)selAttr[0];
                return $@"{keyPropertyType} Id = ValueConvertHelper.TryChangeType<{keyPropertyType}>((string)Global.Id)??default({keyPropertyType});
return from t in db.{tableName} where t.{keyColumn} == Id select {att.CompleteSelect};";
            }

            throw new InvalidOperationException("Table-Type was not found!");
        }

        private static string GetKeyType(DbContext context, string tableName, IServiceProvider services, out string keyName, out Type tableType, out bool isKeyless, out bool isAccessible)
        {
            ConfigureLinqForContext(context, RosFkConfig, out var contextType);
            var prop = contextType.GetProperty(tableName);
            tableType = null;
            isAccessible = true;
            if (prop != null)
            {
                ForeignKeySecurityAttribute accessAttr = null;
                if (Attribute.IsDefined(prop, typeof(ForeignKeySecurityAttribute)))
                {
                    accessAttr = (ForeignKeySecurityAttribute)Attribute.GetCustomAttribute(prop,typeof(ForeignKeySecurityAttribute));
                }

                isAccessible = accessAttr == null || services.VerifyUserPermissions(accessAttr.RequiredPermissions);
                Console.WriteLine($"Holdrio: IsAccessible = {isAccessible}");
                tableType = prop.PropertyType.GetGenericArguments()[0];
                isKeyless = Attribute.IsDefined(tableType, typeof(KeylessAttribute));
                if (!isKeyless)
                {
                    var keys = context.GetKeyProperties(tableType);
                    if (keys.Length != 1)
                    {
                        throw new InvalidOperationException(
                            "Unable to process entities that have a composite Primary-Key!");
                    }

                    var keyProperty = tableType.GetProperty(keys[0]);
                    keyName = keyProperty.Name;
                    return GetTypeForKey(keyProperty.PropertyType);
                }
            }

            isKeyless = true;
            keyName = "--";
            return "string";
        }

        private static void ConfigureLinqForContext(DbContext context, string configName, out Type contextType)
        {
            contextType = context.GetType();
            var nameSpace = contextType.Namespace;
            var assemblyName = contextType.Assembly.FullName;
            NativeScriptHelper.AddReference(configName, assemblyName);
            NativeScriptHelper.AddUsing(configName, nameSpace);
        }

        private static string GetTypeForKey(Type type)
        {
            if (type == typeof(int))
            {
                return "int";
            }

            if (type == typeof(long))
            {
                return "long";
            }

            if (type == typeof(Guid))
            {
                return "Guid";
            }

            if (type == typeof(string))
            {
                return "string";
            }

            throw new InvalidOperationException($"Unexpected Key-Type! ({type.FullName})");
        }
    }
}
