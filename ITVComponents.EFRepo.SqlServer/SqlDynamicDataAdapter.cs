using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.DataAccess.Models;
using ITVComponents.EFRepo.DynamicData;
using ITVComponents.EFRepo.Extensions;
using ITVComponents.EFRepo.Helpers;
using ITVComponents.Formatting;
using ITVComponents.Logging;
using ITVComponents.TypeConversion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ITVComponents.EFRepo.SqlServer
{
    public class SqlDynamicDataAdapter:DynamicDataAdapter
    {
        public SqlDynamicDataAdapter(DbContext parentContext) : base(parentContext)
        {
            SyntaxProvider = new SqlQuerySyntaxProvider();
        }

        public override List<IDictionary<string,object>> QueryDynamicTable(string tableName, DynamicTableFilter filter, ICollection<DynamicTableSort> sorts, out int totalCount, string tableAlias = null, DynamicQueryCallbackProvider queryCallbacks = null, int? hitsPerPage=null, int?page=null)
        {
            var desc = DescribeTable(tableName, true);
            using (Facade.UseConnection(out DbCommand cmd))
            {
                var rawQuery = filter!=null?BuildWhereClause(desc, filter, cmd, 0, tableAlias, queryCallbacks):null;
                var rawOrdering = (sorts != null && sorts.Count != 0) ? string.Join(", ", from t in sorts select $"{BrowseColumns(desc, t.ColumnName, tableAlias, queryCallbacks?.FQColumnQuery, out _)} {t.SortOrder}") : null;
                var rawCols = string.Join(", ", from t in desc select SyntaxProvider.FullQualifyColumn(tableAlias, t.ColumnName));
                var finalQuery = $@"select {rawCols}{(!string.IsNullOrEmpty(queryCallbacks?.CustomQuerySelection) ? $", {queryCallbacks?.CustomQuerySelection}" : "")}, [$$totalRecordCount$$] = count(*) over() from [{tableName}] {(!string.IsNullOrEmpty(tableAlias) ? $"[{tableAlias}]" : "")} {queryCallbacks?.CustomQueryTablePart} {(!string.IsNullOrEmpty(rawQuery)?$"where {rawQuery}":"")} {(!string.IsNullOrEmpty(rawOrdering)?$"order by {rawOrdering}":"")}
{(hitsPerPage != null && page != null?$@"offset {hitsPerPage*(page-1)} rows
fetch next {hitsPerPage} rows only":"")}";
                cmd.CommandText = finalQuery;
                var retVal = cmd.ExecuteReader().ToDictionaries(true).ToList();
                totalCount = 0;
                if (retVal.Count != 0)
                {
                    totalCount = Convert.ToInt32(retVal[0]["$$totalRecordCount$$"]);
                }
                
                return retVal;
            }
        }

        private string BuildWhereClause(ICollection<TableColumnDefinition> desc, DynamicTableFilter filter, DbCommand cmd, int paramIdSeed = 0, string tableAlias = null, DynamicQueryCallbackProvider queryCallbacks = null)
        {
            var paramId = paramIdSeed;
            return filter?.BuildQueryPart((string name, out TableColumnDefinition def) => BrowseColumns(desc, name, tableAlias, queryCallbacks?.FQColumnQuery, out def), o =>
            {
                var par = cmd.CreateParameter();
                cmd.Parameters.Add(par);
                par.ParameterName = $"@qpm{paramId++}";
                par.Value = o;
                return par.ParameterName;
            }, SyntaxProvider);
        }

        public override IDictionary<string,object> Update(string tableName, IDictionary<string,object> values)
        {
            var desc = DescribeTable(tableName, true);
            var pks = desc.Where(n => n.IsPrimaryKey).ToArray();
            DynamicTableFilter filter;
            Dictionary<string, object> paramHolder = new Dictionary<string, object>();
            if (pks.Length > 1)
            {
                var tmpAgg = new DynamicCompositeFilter(DynamicCompositeFilterType.And);
                foreach (var t in pks)
                {
                    tmpAgg.AddFilter(new DynamicTableColumnFilter(t.ColumnName, BinaryCompareFilterOperator.Equal, values[t.ColumnName]));
                }

                filter = tmpAgg;
            }
            else
            {
                filter = new DynamicTableColumnFilter(pks[0].ColumnName, BinaryCompareFilterOperator.Equal, values[pks[0].ColumnName]);
            }

            var sort = new DynamicTableSort {ColumnName = desc[0].ColumnName, SortOrder = SortOrder.Asc};
            var origin = QueryDynamicTable(tableName, filter, new[] {sort}, out _);
            if (origin.Count == 1)
            {
                var oriItem = origin[0];
                var l = new List<(string name, TableColumnDefinition col, object changedValue)>();
                foreach (var t in values.Keys)
                {
                    var tmp = BrowseColumns(desc, t, null, null, out var col);
                    if (tmp != null)
                    {
                        var nv = TypeConverter.TryConvert(values[t], col.Type.ManagedType);
                        var ov = oriItem[t];
                        var equal = nv == null && ov == null || nv != null && ov != null && nv.Equals(ov);
                        if (!equal)
                        {
                            l.Add(new (col.ColumnName, col, nv));
                        }
                    }
                }

                if (l.Count != 0)
                {
                    using (Facade.UseConnection(out DbCommand cmd))
                    {
                        var paramId = 0;
                        StringBuilder bld = new StringBuilder($"Update [{tableName}] set ");
                        bool first = true;
                        foreach (var item in l)
                        {
                            if (!first)
                            {
                                bld.Append(", ");
                            }
                            
                            
                            var param = CreateParameter(cmd, ref paramId, item.changedValue, paramHolder);
                            bld.Append($"{item.name} = {param.ParameterName}");
                            cmd.Parameters.Add(param);
                            first = false;
                        }

                        bld.Append($" where {BuildWhereClause(pks, filter, cmd, paramId)}");
                        cmd.CommandText = paramHolder.FormatText(bld.ToString(), ParameterFilter);
                        var ok = cmd.ExecuteNonQuery();
                        if (ok == 1)
                        {
                            return values;
                        }
                    }
                }
            }
            
            return null;
        }

        public override IDictionary<string, object> Create(string tableName, IDictionary<string, object> values)
        {
            var desc = DescribeTable(tableName, true);
            var hasIdentity = false;
            string identityCol = null;
            StringBuilder cols = new StringBuilder();
            StringBuilder args = new StringBuilder();
            bool first = true;
            int paramId = 0;
            Dictionary<string, object> paramHolder = new Dictionary<string, object>();
            using (Facade.UseConnection(out DbCommand cmd))
            {
                foreach (var t in values.Keys)
                {
                    var tmp = BrowseColumns(desc, t, null, null, out var col);
                    if (tmp != null && !col.IsIdentity)
                    {
                        var paramVal = TypeConverter.TryConvert(values[t], col.Type.ManagedType);
                        values[t] = paramVal;
                        if (paramVal != null)
                        {
                            if (!first)
                            {
                                cols.Append(", ");
                                args.Append(", ");
                            }

                            cols.Append(tmp);
                            var param = CreateParameter(cmd, ref paramId, paramVal, paramHolder);
                            args.Append(param.ParameterName);
                            cmd.Parameters.Add(param);
                            first = false;
                        }
                    }
                    else if (tmp != null && col.IsIdentity)
                    {
                        if (!hasIdentity)
                        {
                            hasIdentity = true;
                            identityCol = col.ColumnName;
                        }
                        else
                        {
                            throw new InvalidOperationException("Multiple Identity-Columns are not supported!");
                        }
                    }
                }

                cmd.CommandText = paramHolder.FormatText($@"insert into [{tableName}] ({cols}) values ({args});
{(hasIdentity?"select scope_identity()":"")}", ParameterFilter);
                if (hasIdentity)
                {
                    values[identityCol] = cmd.ExecuteScalar();
                }
                else
                {
                    cmd.ExecuteNonQuery();
                }
                
                return values;
            }
        }
        
        public override bool Delete(string tableName, IDictionary<string, object> record)
        {
            var desc = DescribeTable(tableName, true);
            var pks = desc.Where(n => n.IsPrimaryKey).ToArray();
            DynamicTableFilter filter;
            if (pks.Length > 1)
            {
                var tmpAgg = new DynamicCompositeFilter(DynamicCompositeFilterType.And);
                foreach (var t in pks)
                {
                    tmpAgg.AddFilter(new DynamicTableColumnFilter(t.ColumnName, BinaryCompareFilterOperator.Equal, record[t.ColumnName]));
                }

                filter = tmpAgg;
            }
            else
            {
                filter = new DynamicTableColumnFilter(pks[0].ColumnName, BinaryCompareFilterOperator.Equal, record[pks[0].ColumnName]);
            }
            
            using (Facade.UseConnection(out DbCommand cmd))
            {
                cmd.CommandText = $"delete from [{tableName}] where {BuildWhereClause(pks, filter, cmd)}";
                return cmd.ExecuteNonQuery() == 1;
            }
        }

        public override List<TableColumnDefinition> DescribeTable(string tableName, bool ignoreUnknownTypes)
        {
            var tmp = SqlQuery<TableColumnDefinition>(@"SELECT c.COLUMN_NAME ColumnName, c.DATA_TYPE DataType, c.CHARACTER_MAXIMUM_LENGTH DataLength, convert(bit,case c.IS_NULLABLE when 'Yes' then 1 else 0 end) Nullable,
c.ordinal_position Position,
convert(bit, case when ic.object_id is not null then 1 else 0 end) IsIdentity,
convert(bit, case when rc.column_name is not null then 1 else 0 end) IsForeignKey,
rc.table_name RefTable,
rc.column_name RefColumn,
convert(bit, case when k.column_name is not null then 1 else 0 end) HasIndex,
convert(bit, case when pk.type='PK' then 1 else 0 end) IsPrimaryKey,
convert(bit, case when pk.type='UQ' then 1 else 0 end) IsUniqueKey,
convert(bit, case when count(cc.referenced_column_id) > 0 then 1 else 0 end) HasReferences
FROM INFORMATION_SCHEMA.COLUMNS c
left outer join information_schema.KEY_COLUMN_USAGE k on k.COLUMN_NAME = c.column_name and k.table_name = c.TABLE_NAME
left outer join sys.identity_columns ic on object_Name(ic.object_id) = c.table_name and ic.name = c.COLUMN_NAME
left outer join sys.foreign_key_columns fc on object_name(fc.parent_object_id) = c.table_name and fc.parent_column_id = c.ORDINAL_POSITION
left outer join sys.key_constraints pk on object_name(pk.object_id) = k.CONSTRAINT_NAME and object_name(pk.parent_object_id) = c.table_name
left outer join information_schema.COLUMNS rc on rc.table_name = object_name(fc.referenced_object_id) and rc.ORDINAL_POSITION = fc.referenced_column_id
left outer join sys.foreign_key_columns cc on object_name(cc.referenced_object_id) = c.table_name and cc.referenced_column_id = c.ORDINAL_POSITION
WHERE c.TABLE_NAME = @p0 
group by c.COLUMN_NAME, c.DATA_TYPE, c.CHARACTER_MAXIMUM_LENGTH, convert(bit,case c.IS_NULLABLE when 'Yes' then 1 else 0 end) ,
c.ordinal_position,
convert(bit, case when ic.object_id is not null then 1 else 0 end),
convert(bit, case when rc.column_name is not null then 1 else 0 end),
rc.table_name,
rc.column_name,
convert(bit,case when k.column_name is not null then 1 else 0 end),
convert(bit, case when pk.type='PK' then 1 else 0 end),
pk.type
ORDER BY c.ORDINAL_POSITION", tableName);
            tmp.ForEach(n => n.Type = SyntaxProvider.GetAppropriateType(n, !ignoreUnknownTypes));
            if (ignoreUnknownTypes)
            {
                tmp = tmp.Where(n => n.Type != null).ToList();
            }
            
            return tmp;
        }

        public override void CopyTableData(string src, string dst, bool ignoreMissingColumn = false, bool whatIf = false)
        {
            var srcColumns = DescribeTable(src, false);
            var dstColumns = DescribeTable(dst, false);
            var diff = DynamicTableHelper.CompareDefinitions(srcColumns, dstColumns);
            List<string> columns = new List<string>();
            foreach (var item in diff)
            {
                if (item.Table2Def == null && !ignoreMissingColumn)
                {
                    if (!whatIf)
                    {
                        throw new InvalidOperationException("CopyJob with possible loss of data is not supported!");
                    }
                    else
                    {
                        LogEnvironment.LogEvent("CopyJob would fail due to data-loss!",LogSeverity.Error, "ITVComponents.EFRepo.SqlServer.SqlDynamicDataAdapter");
                    }
                }

                if (item.Table1Def!= null && item.Table2Def != null)
                {
                    columns.Add($"[{item.ColumnName}]");
                    if (!item.Table1Def.DataType.Equals(item.Table2Def.DataType, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!whatIf)
                        {
                            throw new InvalidOperationException("Changing the DataType is not supported!");
                        }
                        else
                        {
                            LogEnvironment.LogEvent("CopyJob would fail due to non-equal Data-Types between source and destination-table!", LogSeverity.Error, "ITVComponents.EFRepo.SqlServer.SqlDynamicDataAdapter");
                        }
                    }
                }
            }

            var joinedColumns = string.Join(", ", columns);
            var rawQuery = $"insert into [{dst}] ({joinedColumns}) select {joinedColumns} from [{src}]";
            if (!whatIf)
            {
                using (Facade.UseConnection(out DbCommand cmd))
                {
                    cmd.CommandText = rawQuery;
                    cmd.ExecuteNonQuery();
                }
            }
            else
            {
                LogEnvironment.LogEvent($"CopyJob would execute the following SQL-Command: {{{rawQuery}}}", LogSeverity.Error, "ITVComponents.EFRepo.SqlServer.SqlDynamicDataAdapter");
            }
        }

        public override void AlterOrCreateTable(string tableName, TableColumnDefinition[] columns, bool forceDeleteColumn = false, bool useTransaction = true, bool whatIf = false)
        {
            var lenFx = new Func<TableColumnDefinition, string>(def =>
            {
                var ln = def.DataLength ?? -1;
                return $"{(def.Type.LengthRequired?$"({(ln != -1?ln.ToString():"max")})":"")}";
            });
            var pos = 1;
            foreach (var col in columns)
            {
                if (col.Type == null)
                {
                    col.Type = SyntaxProvider.GetAppropriateType(col, true);
                }

                if (col.Position == 0)
                {
                    col.Position = pos++;
                }
            }
            
            bool transactionCreated = false;
            if (useTransaction && (Facade.CurrentTransaction == null))
            {
                if (!whatIf)
                {
                    Facade.BeginTransaction();
                }
                else
                {
                    LogEnvironment.LogEvent("Table-Modification would start a new transaction!", LogSeverity.Report, "ITVComponents.EFRepo.SqlServer.SqlDynamicDataAdapter");
                }
                
                transactionCreated = true;
            }

            try
            {
                List<string> dropIndices = new List<string>();
                List<string> addIndices = new List<string>();
                List<string> constraintChanges = new List<string>();
                var cmd = "";
                if (TableExists(tableName))
                {
                    var orig = DescribeTable(tableName, false);
                    var differ = DynamicTableHelper.CompareDefinitions(orig, columns);
                    List<string> addCols = new List<string>();
                    List<string> dropCols = new List<string>();
                    List<string> alterCols = new List<string>();
                    foreach (var item in differ)
                    {
                        if (item.Table2Def == null && !forceDeleteColumn)
                        {
                            if (!whatIf)
                            {
                                throw new InvalidOperationException("Column removal must be explicitly forced!");
                            }
                            else
                            {
                                LogEnvironment.LogEvent("Table-Modification would fail due to a column-removal with forceDeleteColumn=false!", LogSeverity.Error, "ITVComponents.EFRepo.SqlServer.SqlDynamicDataAdapter");
                            }
                        }
                        
                        if (item.Table2Def == null)
                        {
                            dropCols.Add($"[{item.ColumnName}]");
                        }

                        if (item.Table1Def!= null && item.Table2Def != null)
                        {
                            if (!item.Table1Def.DataType.Equals(item.Table2Def.DataType, StringComparison.OrdinalIgnoreCase))
                            {
                                if (!whatIf)
                                {
                                    throw new InvalidOperationException("Changing the DataType is not supported!");
                                }
                                else
                                {
                                    LogEnvironment.LogEvent("Table-Modification would fail, because DataTypes must not be altered!", LogSeverity.Error, "ITVComponents.EFRepo.SqlServer.SqlDynamicDataAdapter");
                                }
                            }
                            
                            if (item.Table1Def.DataLength != item.Table2Def.DataLength || item.Table1Def.Nullable != item.Table2Def.Nullable)
                            {
                                alterCols.Add($"[{item.ColumnName}] {item.Table2Def.DataType}{lenFx(item.Table2Def)} {(item.Table2Def.Nullable ? "" : "NOT ")}NULL");
                            }

                            if ((item.Table1Def.HasIndex != item.Table2Def.HasIndex || item.Table1Def.IsUniqueKey != item.Table2Def.IsUniqueKey) && item.Table1Def.IsForeignKey == item.Table2Def.IsForeignKey && !item.Table2Def.IsForeignKey)
                            {
                                if (item.Table1Def.IsPrimaryKey != item.Table2Def.IsPrimaryKey)
                                {
                                    if (!whatIf)
                                    {
                                        throw new InvalidOperationException("Primary keys must not be altered!");
                                    }
                                    else
                                    {
                                        LogEnvironment.LogEvent("Table-Modification would fail, because Primary-Key Columns may not be altered!", LogSeverity.Error, "ITVComponents.EFRepo.SqlServer.SqlDynamicDataAdapter");
                                    }
                                }

                                if (!item.Table1Def.IsPrimaryKey)
                                {
                                    if (item.Table1Def.IsUniqueKey)
                                    {
                                        dropIndices.Add($"UQ_{tableName}_{item.ColumnName}");
                                    }
                                    else if (item.Table1Def.HasIndex)
                                    {
                                        dropIndices.Add($"CX_{tableName}_{item.ColumnName}");
                                    }

                                    if (item.Table2Def.IsUniqueKey)
                                    {
                                        addIndices.Add($"Create UNIQUE INDEX [UQ_{tableName}_{item.ColumnName}] on [{tableName}] ([{item.ColumnName}] ASC)");
                                    }
                                    else if (item.Table2Def.HasIndex)
                                    {
                                        addIndices.Add($"Create NONCLUSTERED INDEX [CX_{tableName}_{item.ColumnName}] on [{tableName}] ({item.ColumnName})");
                                    }
                                }
                            }

                            if (item.Table2Def.IsForeignKey != item.Table1Def.IsForeignKey)
                            {
                                if (item.Table1Def.IsForeignKey)
                                {
                                    constraintChanges.Add($"Alter Table [{tableName}] drop constraint [FK_{tableName}_{item.Table1Def.Position}_{item.Table1Def.RefTable}_{item.Table1Def.RefColumn}]");
                                }
                                else
                                {
                                    constraintChanges.Add($@"Alter Table [{tableName}] add constraint [FK_{tableName}_{item.Table2Def.Position}_{item.Table2Def.RefTable}_{item.Table2Def.RefColumn}] Foreign Key ({item.Table2Def.ColumnName})
references [{item.Table2Def.RefTable}] ([{item.Table2Def.RefColumn}])");
                                }
                            }
                        }
                        else if (item.Table2Def != null)
                        {
                            addCols.Add($"[{item.ColumnName}] {item.Table2Def.DataType}{lenFx(item.Table2Def)} {(item.Table2Def.Nullable?"":"NOT ")}NULL");
                            if (!item.Table2Def.IsPrimaryKey && !item.Table2Def.IsForeignKey && (item.Table2Def.HasIndex || item.Table2Def.IsUniqueKey))
                            {
                                if (item.Table2Def.IsUniqueKey)
                                {
                                    addIndices.Add($"Create UNIQUE INDEX [UQ_{tableName}_{item.ColumnName}] on [{tableName}] ([{item.ColumnName}] ASC)");
                                }
                                else if (item.Table2Def.HasIndex)
                                {
                                    addIndices.Add($"Create NONCLUSTERED INDEX [CX_{tableName}_{item.ColumnName}] on [{tableName}] ({item.ColumnName})");
                                }
                            }

                            if (item.Table2Def.IsForeignKey)
                            {
                                constraintChanges.Add($@"Alter Table [{tableName}] add constraint [FK_{tableName}_{item.Table2Def.Position}_{item.Table2Def.RefTable}_{item.Table2Def.RefColumn}] Foreign Key ({item.Table2Def.ColumnName})
references [{item.Table2Def.RefTable}] ([{item.Table2Def.RefColumn}])");
                            }
                        }
                    }

                    if (dropCols.Count != 0)
                    {
                        cmd = $"alter table [{tableName}] drop column {string.Join(", ", dropCols)}";
                        if (!whatIf)
                        {
                            Facade.ExecuteSqlRaw(cmd);
                        }
                        else
                        {
                            LogEnvironment.LogEvent($"Table-Modification would execute the following SQL-Command: {{{cmd}}}", LogSeverity.Report, "ITVComponents.EFRepo.SqlServer.SqlDynamicDataAdapter");
                        }
                    }

                    if (alterCols.Count != 0)
                    {
                        foreach (var ac in alterCols)
                        {
                            cmd = $"alter table [{tableName}] alter column {ac}";
                            if (!whatIf)
                            {
                                Facade.ExecuteSqlRaw(cmd);
                            }
                            else
                            {
                                LogEnvironment.LogEvent($"Table-Modification would execute the following SQL-Command: {{{cmd}}}", LogSeverity.Report, "ITVComponents.EFRepo.SqlServer.SqlDynamicDataAdapter");
                            }
                        }
                    }

                    if (addCols.Count != 0)
                    {
                        cmd = $"alter table [{tableName}] add {string.Join(", ", addCols)}";
                        if (!whatIf)
                        {
                            Facade.ExecuteSqlRaw(cmd);
                        }
                        else
                        {
                            LogEnvironment.LogEvent($"Table-Modification would execute the following SQL-Command: {{{cmd}}}", LogSeverity.Report, "ITVComponents.EFRepo.SqlServer.SqlDynamicDataAdapter");
                        }
                    }
                }
                else
                {
                    cmd = $"CREATE TABLE [{tableName}] ({string.Join(", ", from t in columns select $"[{t.ColumnName}] {t.DataType}{lenFx(t)} {(t.Nullable?"":"NOT")} NULL {(t.IsPrimaryKey?"IDENTITY(1,1) PRIMARY KEY":"")}")})";
                    if (!whatIf)
                    {
                        Facade.ExecuteSqlRaw(cmd);
                    }
                    else
                    {
                        LogEnvironment.LogEvent($"Table-Modification would execute the following SQL-Command: {{{cmd}}}", LogSeverity.Report, "ITVComponents.EFRepo.SqlServer.SqlDynamicDataAdapter");
                    }
                    
                    foreach(var item in columns)
                    {
                        if (!item.IsPrimaryKey && (item.HasIndex || item.IsUniqueKey))
                        {
                            if (item.IsUniqueKey)
                            {
                                addIndices.Add($"Create UNIQUE INDEX [UQ_{tableName}_{item.ColumnName}] on [{tableName}] ([{item.ColumnName}] ASC)");
                            }
                            else if (item.HasIndex)
                            {
                                addIndices.Add($"Create NONCLUSTERED INDEX [CX_{tableName}_{item.ColumnName}] on [{tableName}] ({item.ColumnName})");
                            }
                        }
                        
                        if (item.IsForeignKey)
                        {
                            constraintChanges.Add($@"Alter Table [{tableName}] add constraint [FK_{tableName}_{item.Position}_{item.RefTable}_{item.RefColumn}] Foreign Key ({item.ColumnName})
references [{item.RefTable}] ([{item.RefColumn}])");
                        }
                    }

                }
                
                if (dropIndices.Count != 0)
                {
                    foreach(var di in dropIndices)
                    {
                        cmd = $"Drop INDEX IF EXISTS [{di}] on [{tableName}]";
                        if (!whatIf)
                        {
                            Facade.ExecuteSqlRaw(cmd);
                        }
                        else
                        {
                            LogEnvironment.LogEvent($"Table-Modification would execute the following SQL-Command: {{{cmd}}}", LogSeverity.Report, "ITVComponents.EFRepo.SqlServer.SqlDynamicDataAdapter");
                        }
                    }
                }
                    
                if (addIndices.Count != 0)
                {
                    foreach(var ai in addIndices)
                    {
                        cmd = ai;
                        if (!whatIf)
                        {
                            Facade.ExecuteSqlRaw(cmd);
                        }
                        else
                        {
                            LogEnvironment.LogEvent($"Table-Modification would execute the following SQL-Command: {{{cmd}}}", LogSeverity.Report, "ITVComponents.EFRepo.SqlServer.SqlDynamicDataAdapter");
                        }
                    }
                }

                if (constraintChanges.Count != 0)
                {
                    foreach (var cc in constraintChanges)
                    {
                        cmd = cc;
                        if (!whatIf)
                        {
                            Facade.ExecuteSqlRaw(cmd);
                        }
                        else
                        {
                            LogEnvironment.LogEvent($"Table-Modification would execute the following SQL-Command: {{{cmd}}}", LogSeverity.Report, "ITVComponents.EFRepo.SqlServer.SqlDynamicDataAdapter");
                        }
                    }
                }

                if (transactionCreated)
                {
                    if (!whatIf)
                    {
                        Facade.CommitTransaction();
                    }
                    else
                    {
                        LogEnvironment.LogEvent($"Table-Modification would commit the started transaction.", LogSeverity.Report, "ITVComponents.EFRepo.SqlServer.SqlDynamicDataAdapter");
                    }

                }
            }
            catch
            {
                if (transactionCreated)
                {
                    if (!whatIf)
                    {
                        Facade.RollbackTransaction();
                    }
                    else
                    {
                        LogEnvironment.LogEvent($"Table-Modification would rollback the started transaction.", LogSeverity.Report, "ITVComponents.EFRepo.SqlServer.SqlDynamicDataAdapter");
                    }
                }

                throw;
            }
        }
        
        public override List<T> SqlQuery<T>(string query, params object[] arguments) //where T:new()
        {
            var id = 0;
            using (Facade.UseConnection(out DbCommand cmd))
            {
                Dictionary<string, object> paramHolder = new Dictionary<string, object>();
                cmd.Parameters.AddRange((from t in arguments select CreateParameter(cmd, ref id, t, paramHolder)).ToArray());
                cmd.CommandText = paramHolder.FormatText(query, ParameterFilter);
                return cmd.ExecuteReader().GetModelResult<T, T>().ToList();
            }
        }
        
        public override List<object> SqlQuery(string query, Type targetType, params object[] arguments) //where T:new()
        {
            var id = 0;
            using (Facade.UseConnection(out DbCommand cmd))
            {
                Dictionary<string, object> paramHolder = new Dictionary<string, object>();
                cmd.Parameters.AddRange((from t in arguments select CreateParameter(cmd, ref id, t,paramHolder)).ToArray());
                cmd.CommandText = paramHolder.FormatText(query, ParameterFilter);
                return cmd.ExecuteReader().GetModelResult(targetType, targetType).ToList();
            }
        }

        public override IEnumerable<ExpandoObject> SqlQuery(string query, IDictionary<string, object> arguments)
        {
            var id = 0;
            using (Facade.UseConnection(out DbCommand cmd))
            {

                cmd.CommandText = query;
                cmd.Parameters.AddRange((from t in arguments select CreateParameter(cmd, t.Key, t.Value)).ToArray());
                return cmd.ExecuteReader().ToExpandoObjects(false).ToList();
            }
        }

        public override bool TableExists(string tableName)
        {
            return SqlQuery<(string object_Id, string name)>("select object_id, name from sys.objects where object_id = object_ID(@p0) and type = N'U'", tableName).Count != 0;
        }

        private DbParameter CreateParameter(DbCommand cmd, ref int id, object value, Dictionary<string,object> parameterMapper)
        {
            string pn = $"p{id++}";
            string fpn = $"@{pn}";
            parameterMapper.Add(pn, fpn);
            return CreateParameter(cmd, fpn, value);
        }

        private bool ParameterFilter(string argument, out string newArgument)
        {
            if (!argument.StartsWith("->"))
            {
                newArgument = null;
                return false;
            }

            newArgument = argument.Substring(2);
            return true;
        }

        private DbParameter CreateParameter(DbCommand cmd, string name, object value)
        {
            var retVal = cmd.CreateParameter();
            retVal.ParameterName = name;
            retVal.Value = value;
            return retVal;
        }
        
        private string BrowseColumns(ICollection<TableColumnDefinition> desc, string columnName, string tableAlias, TableColumnResolveCallback getCustomColumnDef, out TableColumnDefinition def)
        {
            def = desc.FirstOrDefault(n => n.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase));
            if (def!= null)
            {
                return SyntaxProvider.FullQualifyColumn(tableAlias, def.ColumnName); // $"{(tableAlias != null ? $"[{tableAlias}]." : "")}[{col.ColumnName}]";
            }

            return getCustomColumnDef?.Invoke(columnName, out def);
        }
    }
}
