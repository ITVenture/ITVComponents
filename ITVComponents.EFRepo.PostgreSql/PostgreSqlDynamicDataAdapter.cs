using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.DataAccess.Models;
using ITVComponents.EFRepo.DynamicData;
using ITVComponents.EFRepo.Extensions;
using ITVComponents.EFRepo.Helpers;
using ITVComponents.Formatting;
using ITVComponents.Logging;
using ITVComponents.TypeConversion;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.EFRepo.PostgreSql
{
    public class PostgreSqlDynamicDataAdapter : DynamicDataAdapter
    {
        public PostgreSqlDynamicDataAdapter(DbContext parentContext) : this(parentContext, new PostgreSqlQuerySyntaxProvider())
        {
        }

        public PostgreSqlDynamicDataAdapter(DbContext parentContext, IQuerySyntaxProvider syntaxProvider):base(parentContext)
        {
            SyntaxProvider = syntaxProvider;
        }

        public override List<IDictionary<string, object>> QueryDynamicTable(string tableName, DynamicTableFilter filter, ICollection<DynamicTableSort> sorts, out int totalCount, string tableAlias = null, DynamicQueryCallbackProvider queryCallbacks = null, int? hitsPerPage = null, int? page = null)
        {
            tableName = SyntaxProvider.ResolveTableName(tableName);
            var desc = DescribeTable(tableName, true, out _, false);
            using (Facade.UseConnection(out DbCommand cmd))
            {
                var colFx = new Func<List<TableColumnDefinition>, string, string, TableColumnResolveCallback, string>((cols, colName, alias, cb) =>
                {
                    var retVal = BrowseColumns(cols, colName, alias, cb, out var tmp);
                    if (tmp is AliasQualifiedColumn ali)
                    {
                        retVal = ali.FullQualifiedName;
                    }

                    return retVal;
                });
                var rawQuery = filter != null ? BuildWhereClause(desc, filter, cmd, 0, tableAlias, queryCallbacks) : null;
                var rawOrdering = (sorts != null && sorts.Count != 0) ? string.Join(", ", from t in sorts select $"{colFx(desc, t.ColumnName, tableAlias, queryCallbacks?.FQColumnQuery)} {t.SortOrder}") : null;
                var rawCols = string.Join(", ", from t in desc select SyntaxProvider.FullQualifyColumn(tableAlias, t.ColumnName, false));
                var finalQuery = $@"select {rawCols}{(!string.IsNullOrEmpty(queryCallbacks?.CustomQuerySelection) ? $", {queryCallbacks?.CustomQuerySelection}" : "")}, count(*) over() as ""$$totalRecordCount$$"" from {SyntaxProvider.FormatTableName(tableName, false)} {(!string.IsNullOrEmpty(tableAlias) ? $"{SyntaxProvider.FormatColumnName(tableAlias)}" : "")} {queryCallbacks?.CustomQueryTablePart} {(!string.IsNullOrEmpty(rawQuery) ? $"where {rawQuery}" : "")} {(!string.IsNullOrEmpty(rawOrdering) ? $"order by {rawOrdering}" : "")}
{(hitsPerPage != null && page != null ? $@"offset {hitsPerPage * (page - 1)} rows
Limit {hitsPerPage}" : "")}";
                LogEnvironment.LogDebugEvent(null, finalQuery, (int)LogSeverity.Report, "ITVComponents.EFRepo.SqlServer.PostgreSqlDynamicDataAdapter");
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

        public override IDictionary<string, object> GetEntity()
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        public override IDictionary<string, object> Update(string tableName, IDictionary<string, object> values)
        {
            tableName = SyntaxProvider.ResolveTableName(tableName);
            var desc = DescribeTable(tableName, true, out _, false);
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

            var sort = new DynamicTableSort { ColumnName = desc[0].ColumnName, SortOrder = SortOrder.Asc };
            var origin = QueryDynamicTable(tableName, filter, new[] { sort }, out _);
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
                            l.Add(new(col.ColumnName, col, nv));
                        }
                    }
                }

                if (l.Count != 0)
                {
                    using (Facade.UseConnection(out DbCommand cmd))
                    {
                        var paramId = 0;
                        StringBuilder bld = new StringBuilder($"Update {SyntaxProvider.FormatTableName(tableName, false)} set ");
                        bool first = true;
                        foreach (var item in l)
                        {
                            if (!first)
                            {
                                bld.Append(", ");
                            }


                            var param = CreateParameter(cmd, ref paramId, item.changedValue, paramHolder);
                            bld.Append($"{SyntaxProvider.FormatColumnName(item.name)} = {param.ParameterName}");
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
            tableName = SyntaxProvider.ResolveTableName(tableName);
            var desc = DescribeTable(tableName, true, out _, false);
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

                cmd.CommandText = paramHolder.FormatText($@"insert into {SyntaxProvider.FormatTableName(tableName, false)} ({cols}) values ({args})
{(hasIdentity ? $"returning {SyntaxProvider.FormatColumnName(identityCol)}" : "")}", ParameterFilter);
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
            tableName = SyntaxProvider.ResolveTableName(tableName);
            var desc = DescribeTable(tableName, true, out _, false);
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
                cmd.CommandText = $"delete from {SyntaxProvider.FormatTableName(tableName)} where {BuildWhereClause(pks, filter, cmd)}";
                return cmd.ExecuteNonQuery() == 1;
            }
        }

        public override List<TableColumnDefinition> DescribeTable(string tableName, bool ignoreUnknownTypes,
            out bool definitionEditable)
        {
            return DescribeTable(tableName, ignoreUnknownTypes, out definitionEditable, true);
        }

        public override void CopyTableData(string src, string dst, bool ignoreMissingColumn = false, bool whatIf = false)
        {
            src= SyntaxProvider.ResolveTableName(src);
            dst= SyntaxProvider.ResolveTableName(dst);
            var srcColumns = DescribeTable(src, false, out _, false);
            var dstColumns = DescribeTable(dst, false, out _, false);
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
                        LogEnvironment.LogEvent("CopyJob would fail due to data-loss!", LogSeverity.Error, "ITVComponents.EFRepo.SqlServer.PostgreSqlDynamicDataAdapter");
                    }
                }

                if (item.Table1Def != null && item.Table2Def != null)
                {
                    columns.Add($"{SyntaxProvider.FormatColumnName(item.ColumnName)}");
                    if (!item.Table1Def.DataType.Equals(item.Table2Def.DataType, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!whatIf)
                        {
                            throw new InvalidOperationException("Changing the DataType is not supported!");
                        }
                        else
                        {
                            LogEnvironment.LogEvent("CopyJob would fail due to non-equal Data-Types between source and destination-table!", LogSeverity.Error, "ITVComponents.EFRepo.SqlServer.PostgreSqlDynamicDataAdapter");
                        }
                    }
                }
            }

            var joinedColumns = string.Join(", ", columns);
            var rawQuery = $"insert into {SyntaxProvider.FormatTableName(dst, false)} ({joinedColumns}) select {joinedColumns} from {SyntaxProvider.FormatTableName(src, false)}";
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
                LogEnvironment.LogEvent($"CopyJob would execute the following SQL-Command: {{{rawQuery}}}", LogSeverity.Error, "ITVComponents.EFRepo.SqlServer.PostgreSqlDynamicDataAdapter");
            }
        }

        public override void 
            AlterOrCreateTable(string tableName, TableColumnDefinition[] columns, bool forceDeleteColumn = false, bool useTransaction = true, bool whatIf = false)
        {
            var lenFx = new Func<TableColumnDefinition, string>(def =>
            {
                var ln = def.DataLength ?? -1;
                return $"{(def.Type.LengthRequired ? (ln != -1 ? $"({(ln.ToString())})" : "") : "")}";
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
                    LogEnvironment.LogEvent("Table-Modification would start a new transaction!", LogSeverity.Report, "ITVComponents.EFRepo.SqlServer.PostgreSqlDynamicDataAdapter");
                }

                transactionCreated = true;
            }

            try
            {
                List<string> dropIndices = new List<string>();
                List<string> addIndices = new List<string>();
                List<string> constraintChanges = new List<string>();
                var cmd = "";
                tableName = SyntaxProvider.ResolveTableName(tableName);
                if (TableExists(tableName, false))
                {
                    var orig = DescribeTable(tableName, false, out var definitionEditable, false);
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
                                LogEnvironment.LogEvent("Table-Modification would fail due to a column-removal with forceDeleteColumn=false!", LogSeverity.Error, "ITVComponents.EFRepo.SqlServer.PostgreSqlDynamicDataAdapter");
                            }
                        }

                        if (item.Table2Def == null && !definitionEditable)
                        {
                            if (!whatIf)
                            {
                                throw new InvalidOperationException("Column removal is not supported on this table, because the index-configuration is not supported!");
                            }
                            else
                            {
                                LogEnvironment.LogEvent("Table-Modification would fail due to a column-removal on a table with an unsupported index-configuration!", LogSeverity.Error, "ITVComponents.EFRepo.SqlServer.PostgreSqlDynamicDataAdapter");
                            }
                        }

                        if (item.Table2Def == null)
                        {
                            dropCols.Add($"{SyntaxProvider.FormatColumnName(item.ColumnName)}");
                        }

                        if (item.Table1Def != null && item.Table2Def != null)
                        {
                            if (!item.Table1Def.DataType.Equals(item.Table2Def.DataType, StringComparison.OrdinalIgnoreCase))
                            {
                                if (!whatIf)
                                {
                                    throw new InvalidOperationException("Changing the DataType is not supported!");
                                }
                                else
                                {
                                    LogEnvironment.LogEvent("Table-Modification would fail, because DataTypes must not be altered!", LogSeverity.Error, "ITVComponents.EFRepo.SqlServer.PostgreSqlDynamicDataAdapter");
                                }
                            }

                            if (item.Table1Def.DataLength != item.Table2Def.DataLength || item.Table1Def.Nullable != item.Table2Def.Nullable)
                            {
                                alterCols.Add($"{SyntaxProvider.FormatColumnName(item.ColumnName)} {item.Table2Def.DataType}{lenFx(item.Table2Def)} {(item.Table2Def.Nullable ? "" : "NOT ")}NULL");
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
                                        LogEnvironment.LogEvent("Table-Modification would fail, because Primary-Key Columns may not be altered!", LogSeverity.Error, "ITVComponents.EFRepo.SqlServer.PostgreSqlDynamicDataAdapter");
                                    }
                                }

                                if (!definitionEditable)
                                {
                                    if (!whatIf)
                                    {
                                        throw new InvalidOperationException("Index-Changes not supported on this table. Use Management studio to alter table.");
                                    }
                                    else
                                    {
                                        LogEnvironment.LogEvent("Table-Modification would fail, because index-configuration of the table is not supported!", LogSeverity.Error, "ITVComponents.EFRepo.SqlServer.PostgreSqlDynamicDataAdapter");
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
                                        addIndices.Add($"Create UNIQUE INDEX {SyntaxProvider.FormatIndexName($"UQ_{tableName}_{item.ColumnName}")} on {SyntaxProvider.FormatTableName(tableName, false)} ({SyntaxProvider.FormatColumnName(item.ColumnName)} ASC)");
                                    }
                                    else if (item.Table2Def.HasIndex)
                                    {
                                        addIndices.Add($"Create INDEX {SyntaxProvider.FormatIndexName($"CX_{tableName}_{item.ColumnName}")} on {SyntaxProvider.FormatTableName(tableName, false)} ({SyntaxProvider.FormatColumnName(item.ColumnName)})");
                                    }
                                }
                            }

                            if (item.Table2Def.IsForeignKey != item.Table1Def.IsForeignKey)
                            {
                                if (!definitionEditable)
                                {
                                    if (!whatIf)
                                    {
                                        throw new InvalidOperationException("Constraint-Changes not supported on this table. Use Management studio to alter table.");
                                    }
                                    else
                                    {
                                        LogEnvironment.LogEvent("Table-Modification would fail, because index-configuration of the table is not supported!", LogSeverity.Error, "ITVComponents.EFRepo.SqlServer.PostgreSqlDynamicDataAdapter");
                                    }
                                }

                                if (item.Table1Def.IsForeignKey)
                                {
                                    constraintChanges.Add($"Alter Table {SyntaxProvider.FormatTableName(tableName, false)} drop constraint {SyntaxProvider.FormatConstraintName($"FK_{tableName}_{item.Table1Def.Position}_{item.Table1Def.RefTable}_{item.Table1Def.RefColumn}")}");
                                }
                                else
                                {
                                    constraintChanges.Add($@"Alter Table {SyntaxProvider.FormatTableName(tableName, false)} add constraint {SyntaxProvider.FormatConstraintName($"FK_{tableName}_{item.Table1Def.Position}_{item.Table1Def.RefTable}_{item.Table1Def.RefColumn}")} Foreign Key ({SyntaxProvider.FormatColumnName(item.Table2Def.ColumnName)})
references {SyntaxProvider.FormatTableName(item.Table2Def.RefTable, false)} ({SyntaxProvider.FormatColumnName(item.Table2Def.RefColumn)})");
                                }
                            }
                        }
                        else if (item.Table2Def != null)
                        {
                            addCols.Add($"{SyntaxProvider.FormatColumnName(item.ColumnName)} {item.Table2Def.DataType}{lenFx(item.Table2Def)} {(item.Table2Def.Nullable ? "" : "NOT ")}NULL");
                            if (!item.Table2Def.IsPrimaryKey && !item.Table2Def.IsForeignKey && (item.Table2Def.HasIndex || item.Table2Def.IsUniqueKey))
                            {
                                if (item.Table2Def.IsUniqueKey)
                                {
                                    addIndices.Add($"Create UNIQUE INDEX {SyntaxProvider.FormatIndexName($"UQ_{tableName}_{item.ColumnName}")} on {SyntaxProvider.FormatTableName(tableName, false)} ({SyntaxProvider.FormatColumnName(item.ColumnName)} ASC)");
                                }
                                else if (item.Table2Def.HasIndex)
                                {
                                    addIndices.Add($"Create INDEX {SyntaxProvider.FormatIndexName($"CX_{tableName}_{item.ColumnName}")} on {SyntaxProvider.FormatTableName(tableName, false)} ({SyntaxProvider.FormatColumnName(item.ColumnName)})");
                                }
                            }

                            if (item.Table2Def.IsForeignKey)
                            {
                                constraintChanges.Add($@"Alter Table {SyntaxProvider.FormatTableName(tableName, false)} add constraint {SyntaxProvider.FormatConstraintName($"FK_{tableName}_{item.Table2Def.Position}_{item.Table2Def.RefTable}_{item.Table2Def.RefColumn}")} Foreign Key ({SyntaxProvider.FormatColumnName(item.Table2Def.ColumnName)})
references {SyntaxProvider.FormatTableName(item.Table2Def.RefTable, false)} ({SyntaxProvider.FormatColumnName(item.Table2Def.RefColumn)})");
                            }
                        }
                    }

                    if (dropCols.Count != 0)
                    {
                        cmd = $"alter table {SyntaxProvider.FormatTableName(tableName, false)} drop column {string.Join(", ", dropCols)}";
                        if (!whatIf)
                        {
                            Facade.ExecuteSqlRaw(cmd);
                        }
                        else
                        {
                            LogEnvironment.LogEvent($"Table-Modification would execute the following SQL-Command: {{{cmd}}}", LogSeverity.Report, "ITVComponents.EFRepo.SqlServer.PostgreSqlDynamicDataAdapter");
                        }
                    }

                    if (alterCols.Count != 0)
                    {
                        foreach (var ac in alterCols)
                        {
                            cmd = $"alter table {SyntaxProvider.FormatTableName(tableName, false)} alter column {ac}";
                            if (!whatIf)
                            {
                                Facade.ExecuteSqlRaw(cmd);
                            }
                            else
                            {
                                LogEnvironment.LogEvent($"Table-Modification would execute the following SQL-Command: {{{cmd}}}", LogSeverity.Report, "ITVComponents.EFRepo.SqlServer.PostgreSqlDynamicDataAdapter");
                            }
                        }
                    }

                    if (addCols.Count != 0)
                    {
                        cmd = $"alter table {SyntaxProvider.FormatTableName(tableName, false)} add {string.Join(", ", addCols)}";
                        if (!whatIf)
                        {
                            Facade.ExecuteSqlRaw(cmd);
                        }
                        else
                        {
                            LogEnvironment.LogEvent($"Table-Modification would execute the following SQL-Command: {{{cmd}}}", LogSeverity.Report, "ITVComponents.EFRepo.SqlServer.PostgreSqlDynamicDataAdapter");
                        }
                    }
                }
                else
                {
                    cmd = $"CREATE TABLE {SyntaxProvider.FormatTableName(tableName, false)} ({string.Join(", ", from t in columns select $"{SyntaxProvider.FormatColumnName(t.ColumnName)} {t.DataType}{lenFx(t)} {(t.Nullable ? "" : "NOT")} NULL {(t.IsPrimaryKey && t.IsIdentity ? "GENERATED BY DEFAULT AS IDENTITY" : "")} {(t.IsPrimaryKey ? " PRIMARY KEY" : "")}")})";
                    if (!whatIf)
                    {
                        Facade.ExecuteSqlRaw(cmd);
                    }
                    else
                    {
                        LogEnvironment.LogEvent($"Table-Modification would execute the following SQL-Command: {{{cmd}}}", LogSeverity.Report, "ITVComponents.EFRepo.SqlServer.PostgreSqlDynamicDataAdapter");
                    }

                    foreach (var item in columns)
                    {
                        if (!item.IsPrimaryKey && (item.HasIndex || item.IsUniqueKey))
                        {
                            if (item.IsUniqueKey)
                            {
                                addIndices.Add($"Create UNIQUE INDEX {SyntaxProvider.FormatIndexName($"UQ_{tableName}_{item.ColumnName}")} on {SyntaxProvider.FormatTableName(tableName, false)} ({SyntaxProvider.FormatColumnName(item.ColumnName)} ASC)");
                            }
                            else if (item.HasIndex)
                            {
                                addIndices.Add($"Create INDEX {SyntaxProvider.FormatIndexName($"CX_{tableName}_{item.ColumnName}")} on {SyntaxProvider.FormatTableName(tableName, false)} ({SyntaxProvider.FormatColumnName(item.ColumnName)})");
                            }
                        }

                        if (item.IsForeignKey)
                        {
                            constraintChanges.Add($@"Alter Table {SyntaxProvider.FormatTableName(tableName, false)} add constraint {SyntaxProvider.FormatConstraintName($"FK_{tableName}_{item.Position}_{item.RefTable}_{item.RefColumn}")} Foreign Key ({SyntaxProvider.FormatColumnName(item.ColumnName)})
references {SyntaxProvider.FormatTableName(item.RefTable, false)} ({SyntaxProvider.FormatColumnName(item.RefColumn)})");
                        }
                    }

                }

                if (dropIndices.Count != 0)
                {
                    foreach (var di in dropIndices)
                    {
                        cmd = $"Drop INDEX IF EXISTS {SyntaxProvider.FormatIndexName(di)}";
                        if (!whatIf)
                        {
                            Facade.ExecuteSqlRaw(cmd);
                        }
                        else
                        {
                            LogEnvironment.LogEvent($"Table-Modification would execute the following SQL-Command: {{{cmd}}}", LogSeverity.Report, "ITVComponents.EFRepo.SqlServer.PostgreSqlDynamicDataAdapter");
                        }
                    }
                }

                if (addIndices.Count != 0)
                {
                    foreach (var ai in addIndices)
                    {
                        cmd = ai;
                        if (!whatIf)
                        {
                            Facade.ExecuteSqlRaw(cmd);
                        }
                        else
                        {
                            LogEnvironment.LogEvent($"Table-Modification would execute the following SQL-Command: {{{cmd}}}", LogSeverity.Report, "ITVComponents.EFRepo.SqlServer.PostgreSqlDynamicDataAdapter");
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
                            LogEnvironment.LogEvent($"Table-Modification would execute the following SQL-Command: {{{cmd}}}", LogSeverity.Report, "ITVComponents.EFRepo.SqlServer.PostgreSqlDynamicDataAdapter");
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
                        LogEnvironment.LogEvent($"Table-Modification would commit the started transaction.", LogSeverity.Report, "ITVComponents.EFRepo.SqlServer.PostgreSqlDynamicDataAdapter");
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
                        LogEnvironment.LogEvent($"Table-Modification would rollback the started transaction.", LogSeverity.Report, "ITVComponents.EFRepo.SqlServer.PostgreSqlDynamicDataAdapter");
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
                cmd.Parameters.AddRange((from t in arguments select CreateParameter(cmd, ref id, t, paramHolder)).ToArray());
                cmd.CommandText = paramHolder.FormatText(query, ParameterFilter);
                return cmd.ExecuteReader().GetModelResult(targetType, targetType).ToList();
            }
        }

        public override IEnumerable<IDictionary<string, object>> SqlQuery(string query, IDictionary<string, object> arguments)
        {
            var id = 0;
            using (Facade.UseConnection(out DbCommand cmd))
            {

                cmd.CommandText = query;
                cmd.Parameters.AddRange((from t in arguments select CreateParameter(cmd, t.Key, t.Value)).ToArray());
                return cmd.ExecuteReader().ToDictionaries(true).ToList();
            }
        }

        public override bool TableExists(string tableName)
        {
            return TableExists(tableName, true);
        }

        protected virtual bool TableExists(string tableName, bool resolveTable)
        {
            tableName = resolveTable ? SyntaxProvider.ResolveTableName(tableName) : tableName;
            return SqlQuery<(string tabletype,string name)>("select table_type tableType, table_name name from information_schema.tables where table_type = 'BASE TABLE' and table_name = @p0", tableName).Count != 0;
        }

        protected virtual List<TableColumnDefinition> DescribeTable(string tableName, bool ignoreUnknownTypes, out bool definitionEditable, bool resolveTableName)
        {
            tableName = resolveTableName ? SyntaxProvider.ResolveTableName(tableName) : tableName;
            var tmp = SqlQuery<TableColumnDefinition>(@"select distinct * from
(SELECT
   c.column_name ""ColumnName"", 
   c.data_type ""DataType"",
   c.character_maximum_length ""DataLength"",
   case c.IS_NULLABLE when 'YES' then true else false end ""Nullable"",
   c.ordinal_position ""Position"",
   case c.is_identity when 'YES' then true else false end ""IsIdentity"",
   case when ccu.column_name is not null then true else false end ""IsForeignKey"",
   ccu.table_name ""RefTable"",
   ccu.column_name ""RefColumn"",
   case when pc.constraint_type is not null then true else false end ""IsPrimaryKey"",
   case when k.column_name is not null or ia.attname is not null then true else false end ""HasIndex"",
   case when iix.indisunique and pc.constraint_type is null then true else false end ""IsUnique"",
   case when count(pcu.column_name) > 0 then true else false end ""HasReferences""
FROM 
   INFORMATION_SCHEMA.COLUMNS c
left outer join information_schema.KEY_COLUMN_USAGE k on k.COLUMN_NAME = c.column_name and k.table_name = c.TABLE_NAME
left outer join information_schema.table_constraints AS tc 
      ON tc.constraint_name = k.constraint_name
      AND tc.table_schema = k.table_schema
	  AND tc.constraint_type = 'FOREIGN KEY'
left outer join information_schema.table_constraints AS pc 
      ON pc.constraint_name = k.constraint_name
      AND pc.table_schema = k.table_schema
	  AND pc.constraint_type = 'PRIMARY KEY'
left outer JOIN information_schema.constraint_column_usage AS ccu
      ON ccu.constraint_name = tc.constraint_name
      AND ccu.table_schema = tc.table_schema
left outer join (pg_class it
   inner join pg_index iix on it.oid = iix.indrelid
   inner join pg_class ii on ii.oid = iix.indexrelid
   inner join pg_attribute ia on ia.attrelid = it.oid and ia.attnum = ANY(iix.indkey)
   inner join pg_namespace AS ins ON it.relnamespace = ins.oid) on it.relname = c.table_name
   																and ia.attname = c.column_name
																and ins.nspname = c.table_schema
  left outer join information_schema.constraint_column_usage pcu on pcu.table_name = c.table_name 
  																	and pcu.table_schema = c.table_schema
																	and pcu.column_name = k.column_name
																	and pc.constraint_type is not null
	  WHERE 
   c.table_name = @p0
group by c.column_name
,   c.data_type
,   c.character_maximum_length
,   c.IS_NULLABLE
,   c.ordinal_position
,   c.is_identity
,   ccu.column_name
,   ccu.table_name
,   ccu.column_name
,   pc.constraint_type
,   k.column_name 
,   ia.attname
,   iix.indisunique 
,   pc.constraint_type
 ) c
order by c.""Position""", tableName);
            var tmp2 = (from t in tmp group t by new { t.ColumnName, t.DataLength, t.DataType, t.Nullable, t.Position, t.RefTable, t.RefColumn, t.HasReferences })
                .ToList();
            tmp.Clear();
            definitionEditable = true;
            foreach (var t in tmp2)
            {
                tmp.Add(new TableColumnDefinition
                {
                    ColumnName = t.Key.ColumnName,
                    DataLength = t.Key.DataLength,
                    DataType = t.Key.DataType,
                    Nullable = t.Key.Nullable,
                    Position = t.Key.Position,
                    RefTable = t.Key.RefTable,
                    RefColumn = t.Key.RefColumn,
                    HasReferences = t.Key.HasReferences,
                    HasIndex = t.All(n => n.HasIndex),
                    IsForeignKey = t.All(n => n.IsForeignKey),
                    IsIdentity = t.All(n => n.IsIdentity),
                    IsPrimaryKey = t.All(n => n.IsPrimaryKey),
                    IsUniqueKey = t.All(n => n.IsUniqueKey),
                    Type = SyntaxProvider.GetAppropriateType(t.First(), !ignoreUnknownTypes)
                });
                definitionEditable &= t.Count() == 1;
            }
            if (ignoreUnknownTypes)
            {
                tmp = tmp.Where(n => n.Type != null).ToList();
            }

            return tmp;
        }

        private IDbDataParameter CreateParameter(DbCommand cmd, ref int id, object value, Dictionary<string, object> parameterMapper)
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

        private IDbDataParameter CreateParameter(DbCommand cmd, string name, object value)
        {
            if (value is not IDbDataParameter par)
            {
                var retVal = cmd.CreateParameter();
                retVal.ParameterName = name;
                retVal.Value = value;
                return retVal;
            }
            
            return par;
        }

        private string BrowseColumns(ICollection<TableColumnDefinition> desc, string columnName, string tableAlias, TableColumnResolveCallback getCustomColumnDef, out TableColumnDefinition def)
        {
            def = desc.FirstOrDefault(n => n.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase));
            if (def != null)
            {
                return SyntaxProvider.FullQualifyColumn(tableAlias, def.ColumnName, false); // $"{(tableAlias != null ? $"[{tableAlias}]." : "")}[{col.ColumnName}]";
            }

            return getCustomColumnDef?.Invoke(columnName, out def);
        }
    }
}
