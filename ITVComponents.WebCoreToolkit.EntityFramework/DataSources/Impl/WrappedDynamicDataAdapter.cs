using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.DynamicData;
using ITVComponents.EFRepo.Expressions.Models;
using ITVComponents.Logging;
using ITVComponents.TypeConversion;
using ITVComponents.WebCoreToolkit.EntityFramework.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Options.ForeignKeys;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace ITVComponents.WebCoreToolkit.EntityFramework.DataSources.Impl
{
    internal class WrappedDynamicDataAdapter:IWrappedDataSource
    {
        private readonly DynamicDataAdapter src;

        public WrappedDynamicDataAdapter(DynamicDataAdapter src)
        {
            this.src = src;
        }
        
        public IEnumerable RunDiagnosticsQuery(DiagnosticsQueryDefinition query, HttpContext httpContext, IDictionary<string, string> queryArguments)
        {
            var arguments = DiagnoseQueryHelper.BuildArguments(query, queryArguments, out var argumentsValid);
            if (argumentsValid)
            {
                return src.SqlQuery(query.QueryText, arguments);
            }

            throw new InvalidOperationException(
                $"Invalid arguments were passed for {httpContext.Request.Path}. (Diagnostics-QueryName: {query.DiagnosticsQueryName})");
        }

        public IEnumerable RunDiagnosticsQuery(DiagnosticsQueryDefinition query, HttpContext httpContext, IDictionary<string, object> arguments)
        {
            var arg = DiagnoseQueryHelper.VerifyArguments(query, arguments, out var argumentsValid);
            if (argumentsValid)
            {
                return src.SqlQuery(query.QueryText, arg);
            }

            throw new InvalidOperationException(
                $"Invalid arguments were passed for {httpContext.Request.Path}. (Diagnostics-QueryName: {query.DiagnosticsQueryName})");
        }

        public ForeignKeyOptions CustomFkSettings { get; } = null;

        public IEnumerable ReadForeignKey(string tableName, string id = null, Dictionary<string, object> postedFilter = null)
        {
            var desc = src.DescribeTable(tableName, true, out _);
            var idColumnCount = desc.Count(n => n.IsPrimaryKey);
            if (idColumnCount != 1)
            {
                throw new InvalidOperationException("No Primary-Key was found!");
            }

            var stringColumnCount = desc.Count(n => n.Type.ManagedType == typeof(string) && !n.IsPrimaryKey);
            if (stringColumnCount == 0)
            {
                throw new InvalidOperationException("Unable to select a proper foreignkey.");
            }

            var idColumn = desc.First(n => n.IsPrimaryKey);
            var stringCol = desc.First(n => n.Type.ManagedType == typeof(string) && !n.IsPrimaryKey);
            var t = typeof(ForeignKeyData<>).MakeGenericType(idColumn.Type.ManagedType);
            var keyAlias = src.SyntaxProvider.FormatColumnName("Key");
            var labelAlias = src.SyntaxProvider.FormatColumnName("Label");
            if (id != null)
            {
                return src.SqlQuery($"Select {src.SyntaxProvider.FormatColumnName(idColumn.ColumnName)} {keyAlias}, {src.SyntaxProvider.FormatColumnName(stringCol.ColumnName)} {labelAlias} from {src.SyntaxProvider.FormatTableName(tableName)} where {src.SyntaxProvider.FormatColumnName(idColumn.ColumnName)} = [->p0]", t, id);
            }

            if (postedFilter.ContainsKey("parsedfilter") && postedFilter["parsedfilter"] is FilterBase fimo)
            {
                var values = new List<object>();
                var tmpAddition = (from tf in postedFilter
                    join d in desc on tf.Key.ToLower() equals d.ColumnName.ToLower()
                    select new CompareFilter
                    {
                        Value = TypeConverter.TryConvert(tf.Value, d.Type.ManagedType), Operator = CompareOperator.Equal,
                        PropertyName = d.ColumnName
                    }).ToArray();

                LogEnvironment.LogDebugEvent($"tmpAddition has {tmpAddition.Length} entries.", LogSeverity.Report);
                var ca = fimo as CompositeFilter ;
                if (tmpAddition.Length != 0 && ca is not { Operator: BoolOperator.And })
                {
                    fimo = ca = new CompositeFilter
                    {
                        Children = [fimo],
                        Operator = BoolOperator.And
                    };
                }
                else if (tmpAddition.Length == 0)
                {
                    ca = null;
                }

                if (ca != null && tmpAddition.Length != 0)
                {
                    LogEnvironment.LogDebugEvent($"ca has {ca.Children.Length} child-entries before adding tmpAddition.", LogSeverity.Report);
                    ca.AddFilters(tmpAddition);
                    LogEnvironment.LogDebugEvent($"ca has now {ca.Children.Length} child-entries.", LogSeverity.Report);
                }

                var whereClause = src.SyntaxProvider.TranslateExpressionFilter(fimo, n =>
                {
                    if (n == "Label")
                    {
                        return (from u in desc
                            where u.Type.ManagedType == typeof(string)
                            select u).ToArray();
                    }

                    return (from u in desc where u.ColumnName.Equals(n, StringComparison.OrdinalIgnoreCase) select u)
                        .ToArray();
                }, (v) =>
                {
                    var retVal = $"[->p{values.Count}]";
                    values.Add(v);
                    return retVal;
                });

                var finalQuery =
                    $"Select {src.SyntaxProvider.FormatColumnName(idColumn.ColumnName)} {keyAlias}, {src.SyntaxProvider.FormatColumnName(stringCol.ColumnName)} {labelAlias} from {src.SyntaxProvider.FormatTableName(tableName)} {(!string.IsNullOrEmpty(whereClause) ? $"where {whereClause}" : "")}";
                LogEnvironment.LogDebugEvent($"executing the following Query for ForeignKey: {finalQuery}", LogSeverity.Report);
                return src.SqlQuery(finalQuery,
                    t, values.ToArray());
                //var filter = fimo.BuildSqlFilter(desc, src.SyntaxProvider, values);
                /*return src.SqlQuery(
                    $"Select {src.SyntaxProvider.FormatColumnName(idColumn.ColumnName)} {keyAlias}, {src.SyntaxProvider.FormatColumnName(stringCol.ColumnName)} {labelAlias} from {src.SyntaxProvider.FormatTableName(tableName)} {(string.IsNullOrEmpty(filter) ? "" : $"where {filter}")}",
                    t, values.ToArray());*/
            }
            else
            {
                var filterValue = (postedFilter?.ContainsKey("Filter") ?? false) ? postedFilter["Filter"] : null;
                if (filterValue != null)
                {
                    List<object> values = new List<object>();
                    List<string> andFilters = new List<string>();
                    List<string> orFilters = new List<string>();
                    string linkOp;
                    foreach (var col in desc)
                    {
                        var target = orFilters;
                        linkOp = " AND ";
                        var pnNull = "is not null";
                        var cv = filterValue;
                        if (postedFilter.ContainsKey(col.ColumnName))
                        {
                            cv = postedFilter[col.ColumnName];
                            linkOp = " OR ";
                            pnNull = "is null";
                            target = andFilters;
                        }

                        var pn = $"[->p{values.Count}]";
                        var op = "=";
                        if (col.Type.ManagedType == typeof(string))
                        {
                            values.Add($"%{cv}%");
                            op = "like";
                        }
                        else
                        {
                            values.Add(TypeConverter.TryConvert(cv, col.Type.ManagedType) ?? DBNull.Value);
                        }

                        target.Add(
                            $"({pn} {pnNull}{linkOp}{src.SyntaxProvider.FormatColumnName(col.ColumnName)} {op} {pn})");
                    }

                    List<string> final = new List<string>();
                    if (andFilters.Count != 0)
                    {
                        final.Add($"({string.Join(" AND ", andFilters)})");
                    }

                    if (orFilters.Count != 0)
                    {
                        final.Add($"({string.Join(" OR ", orFilters)})");
                    }

                    return src.SqlQuery(
                        $"Select {src.SyntaxProvider.FormatColumnName(idColumn.ColumnName)} {keyAlias}, {src.SyntaxProvider.FormatColumnName(stringCol.ColumnName)} {labelAlias} from {src.SyntaxProvider.FormatTableName(tableName)} {(final.Count != 0 ? $"where {string.Join(" AND ", final)}" : "")}",
                        t, values.ToArray());
                }
            }

            return src.SqlQuery($"Select {src.SyntaxProvider.FormatColumnName(idColumn.ColumnName)} {keyAlias}, {src.SyntaxProvider.FormatColumnName(stringCol.ColumnName)} {labelAlias} from {src.SyntaxProvider.FormatTableName(tableName)}", t);
        }
    }
}
