using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.DynamicData;
using ITVComponents.TypeConversion;
using ITVComponents.WebCoreToolkit.EntityFramework.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.DataSources.Impl
{
    internal class WrappedDynamicDataAdapter:IWrappedDataSource
    {
        private readonly DynamicDataAdapter src;

        public WrappedDynamicDataAdapter(DynamicDataAdapter src)
        {
            this.src = src;
        }
        
        public IEnumerable RunDiagnosticsQuery(DiagnosticsQuery query, IDictionary<string, string> queryArguments)
        {
            var arguments = DiagnoseQueryHelper.BuildArguments(query, queryArguments);
            return src.SqlQuery(query.QueryText, arguments);
        }

        public IEnumerable RunDiagnosticsQuery(DiagnosticsQuery query, IDictionary<string, object> arguments)
        {
            var arg = DiagnoseQueryHelper.VerifyArguments(query, arguments);
            return src.SqlQuery(query.QueryText, arg);
        }

        public IEnumerable ReadForeignKey(string tableName, string id = null, Dictionary<string, object> postedFilter = null)
        {
            var desc = src.DescribeTable(tableName, true);
            var idColumnCount = desc.Count(n => n.IsPrimaryKey);
            if (idColumnCount != 1)
            {
                throw new InvalidOperationException("No Primary-Key was found!");
            }

            var stringColumnCount = desc.Count(n => n.Type.ManagedType == typeof(string));
            if (stringColumnCount == 0)
            {
                throw new InvalidOperationException("Unable to select a proper foreignkey.");
            }

            var idColumn = desc.First(n => n.IsPrimaryKey);
            var stringCol = desc.First(n => n.Type.ManagedType == typeof(string));
            var t = typeof(ForeignKeyData<>).MakeGenericType(idColumn.Type.ManagedType);
            var keyAlias = src.SyntaxProvider.FormatObjectName("Key");
            var labelAlias = src.SyntaxProvider.FormatObjectName("Label");
            if (id != null)
            {
                return src.SqlQuery($"Select {src.SyntaxProvider.FormatObjectName(idColumn.ColumnName)} {keyAlias}, {src.SyntaxProvider.FormatObjectName(stringCol.ColumnName)} {labelAlias} from {src.SyntaxProvider.FormatObjectName(tableName)} where {src.SyntaxProvider.FormatObjectName(idColumn.ColumnName)} = [->p0]", t, id);
            }

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
                        values.Add(TypeConverter.TryConvert(cv, col.Type.ManagedType)??DBNull.Value);
                    }
                    
                    target.Add($"({pn} {pnNull}{linkOp}{src.SyntaxProvider.FormatObjectName(col.ColumnName)} {op} {pn})");
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

                return src.SqlQuery($"Select {src.SyntaxProvider.FormatObjectName(idColumn.ColumnName)} {keyAlias}, {src.SyntaxProvider.FormatObjectName(stringCol.ColumnName)} {labelAlias} from {src.SyntaxProvider.FormatObjectName(tableName)} {(final.Count != 0 ? $"where {string.Join(" AND ", final)}" : "")}", t, values.ToArray());
            }
            
            return src.SqlQuery($"Select {src.SyntaxProvider.FormatObjectName(idColumn.ColumnName)} {keyAlias}, {src.SyntaxProvider.FormatObjectName(stringCol.ColumnName)} {labelAlias} from {src.SyntaxProvider.FormatObjectName(tableName)}", t);
        }
    }
}
