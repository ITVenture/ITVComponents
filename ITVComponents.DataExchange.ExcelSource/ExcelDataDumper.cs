using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ITVComponents.DataAccess;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.DataExchange.Configuration;
using ITVComponents.DataExchange.Interfaces;
using ITVComponents.Logging;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.Scripting.CScript.Helpers;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace ITVComponents.DataExchange.ExcelSource
{
    public class ExcelDataDumper:IDataDumper
    {
        private bool legacyFormat;

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Initializes a new instance of the ExcelDataDumper class
        /// </summary>
        public ExcelDataDumper() : this(false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the ExcelDataDumper class
        /// </summary>
        /// <param name="legacyFormat">indicates whether to use the legacy excel format for dumping</param>
        public ExcelDataDumper(bool legacyFormat)
        {
            this.legacyFormat = legacyFormat;
        }

        /// <summary>
        /// Dumps collected data into the given file
        /// </summary>
        /// <param name="fileName">the name of the target filename for this dump-run</param>
        /// <param name="data">the data that must be dumped</param>
        /// <param name="configuration">the dumper-configuiration</param>
        /// <returns>a value indicating whether there was any data available for dumping</returns>
        public bool DumpData(string fileName, DynamicResult[] data, DumpConfiguration configuration)
        {
            using (FileStream fst = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite))
            {
                return DumpData(fst, data, configuration);
            }
        }

        /// <summary>
        /// Dumps collected data into the given stream
        /// </summary>
        /// <param name="outputStream">the output-stream that will receive the dumped data</param>
        /// <param name="data">the data that must be dumped</param>
        /// <param name="configuration">the dumper-configuiration</param>
        /// <returns>a value indicating whether there was any data available for dumping</returns>
        public bool DumpData(Stream outputStream, DynamicResult[] data, DumpConfiguration configuration)
        {
            IWorkbook wb = OpenWorkbook();
            try
            {
                if (configuration == null)
                {
                    PerformSimpleDump(wb, data);
                }
                else
                {
                    PerformComplexDump(wb, data, configuration);
                }

                return true;
            }
            finally
            {
                wb.Write(outputStream);
            }
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            OnDisposed();
        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Opens a workbook in the appropriate format
        /// </summary>
        /// <returns></returns>
        private IWorkbook OpenWorkbook()
        {
            if (legacyFormat)
                return new HSSFWorkbook();

            return new XSSFWorkbook();
        }

        /// <summary>
        /// Performs a simple Dump operation with the given data
        /// </summary>
        /// <param name="wb">the workbook that needs to be dumped</param>
        /// <param name="data">the data that is being exported</param>
        private void PerformSimpleDump(IWorkbook wb, DynamicResult[] data)
        {
            ISheet shiit = wb.CreateSheet("DumpData");
            bool first = true;
            string[] keys = null;
            int rowId = 0;
            foreach (var item in data)
            {
                IRow row;
                if (first)
                {
                    keys = item.Keys;
                    row = shiit.CreateRow(rowId++);
                    DumpRow(row, keys, (DynamicResult)null);
                    first = false;
                }
                row = shiit.CreateRow(rowId++);
                DumpRow(row, keys, item);
            }

            if (keys != null)
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    shiit.AutoSizeColumn(i);
                }
            }
        }

        /// <summary>
        /// Performs a complex Dump operation with the given data
        /// </summary>
        /// <param name="wb">the workbook that needs to be dumped</param>
        /// <param name="data">the data that is being exported</param>
        /// <param name="configuration">the configuration describing the output process</param>
        private void PerformComplexDump(IWorkbook wb, DynamicResult[] data, DumpConfiguration configuration)
        {
            if (configuration.Source != ".")
            {
                DumpSection(wb, data, configuration, configuration.Source);
            }
            else
            {
                var fl = configuration.Files.First();
                var item = data.First();
                foreach (var child in item.Keys)
                {
                    var childConfig = fl.Children[child];
                    if (childConfig == null)
                    {
                        childConfig = fl.Children["Default"];
                    }

                    if (childConfig != null && item[child] is DynamicResult[] childRows)
                    {
                        DumpSection(wb, childRows, childConfig, child);
                    }
                }
            }
        }

        private void DumpSection(IWorkbook wb, DynamicResult[] data, DumpConfiguration configuration, string sheetName)
        {
            if (configuration.Files.Count != 0)
            {
                throw new ArgumentException("No child-sections allowed in a sheet");
            }

            var scope = new Scope(true);
            scope["$sheetName"] = sheetName;
            var first = true;
            string[] keys = null;
            int rowId = 0;
            ISheet shiit = wb.CreateSheet(sheetName.Replace("'","").Replace("\"","").Replace("/","").Replace("\\",""));
            using (var context = ExpressionParser.BeginRepl(scope, a => { DefaultCallbacks.PrepareDefaultCallbacks(a.Scope, a.ReplSession); }))
            {
                var badKeys = scope.Keys.ToList();
                badKeys.Add("$sheetTitle");
                foreach (var item in data)
                {
                    foreach (var key in item.Keys)
                    {
                        if (!(item[key] is DynamicResult[]))
                        {
                            scope[key] = item[key];
                        }
                    }

                    foreach (ConstConfiguration constant in configuration.Constants)
                    {
                        scope[constant.Name] = constant.ConstType == ConstType.SingleExpression
                            ? ExpressionParser.Parse(constant.ValueExpression,context)
                            : ExpressionParser.ParseBlock(constant.ValueExpression, context);
                    }

                    IRow row;
                    if (first)
                    {
                        keys = (from k in scope.Keys join b in badKeys on k equals b into bk
                            from g in bk.DefaultIfEmpty()
                            where g == null
                                  select k).ToArray();
                        if (scope["$sheetTitle"] is string s && !string.IsNullOrEmpty(s))
                        {
                            row = shiit.CreateRow(rowId++);
                            SetTitleRow(row, s, keys.Length);
                        }
                        row = shiit.CreateRow(rowId++);
                        DumpRow(row, keys, (IDictionary<string, object>) null);
                        first = false;
                    }

                    row = shiit.CreateRow(rowId++);
                    DumpRow(row, keys, scope);
                }

                if (keys != null)
                {
                    for (int i = 0; i < keys.Length; i++)
                    {
                        shiit.AutoSizeColumn(i);
                    }
                }
            }
        }

        private void SetTitleRow(IRow targetRow, string title, int columnsToMerge)
        {
            List<ICell> l = new List<ICell>();
            for (int i = 0; i < columnsToMerge; i++)
            {
                l.Add(targetRow.CreateCell(i));
            }

            var addr = new NPOI.SS.Util.CellRangeAddress(targetRow.RowNum, targetRow.RowNum, 0, columnsToMerge - 1);
            targetRow.Sheet.AddMergedRegion(addr);
            var first = l.First();
            first.SetCellType(CellType.String);
            first.SetCellValue(title);
        }

        /// <summary>
        /// Dumps data into the given row
        /// </summary>
        /// <param name="targetRow">the target row into which to dump the provided data</param>
        /// <param name="keys">the keys that are used for output</param>
        /// <param name="data">the source data. if null is provided, the keys are dumped into the row</param>
        private void DumpRow(IRow targetRow, string[] keys, DynamicResult data)
        {
            object[] res = (from t in keys select (object)data?[t] ?? t).ToArray();
            int i = 0;
            res.ForEach(r => SetCellValue(targetRow.CreateCell(i++), r)); //targetRow.CreateCell(i++).SetCellValue(r.ToString()));
        }

        /// <summary>
        /// Dumps data into the given row
        /// </summary>
        /// <param name="targetRow">the target row into which to dump the provided data</param>
        /// <param name="keys">the keys that are used for output</param>
        /// <param name="data">the source data. if null is provided, the keys are dumped into the row</param>
        private void DumpRow(IRow targetRow, string[] keys, IDictionary<string,object> data)
        {
            object[] res = (from t in keys.Where(n => !n.StartsWith("$")) select (data!=null)?data[t] : t).ToArray();
            int i = 0;
            res.ForEach(r => SetCellValue(targetRow.CreateCell(i++),r/*?.ToString()*/));
        }

        private void SetCellValue(ICell cell, object value)
        {
            if (value != null)
            {
                if (value is string s)
                {
                    cell.SetCellValue(s);
                }
                else if (value is double d)
                {
                    cell.SetCellValue(d);
                    cell.CellStyle.DataFormat = cell.Sheet.Workbook.CreateDataFormat().GetFormat("#,##0.00");
                }
                else if (value is float f)
                {
                    cell.SetCellValue(f);
                    cell.CellStyle.DataFormat = cell.Sheet.Workbook.CreateDataFormat().GetFormat("#,##0.00");
                }
                else if (value is decimal m)
                {
                    cell.SetCellValue((double)m);
                    cell.CellStyle.DataFormat = cell.Sheet.Workbook.CreateDataFormat().GetFormat("#,##0.00");
                }
                else if (value is bool b)
                {
                    cell.SetCellValue(b);
                }
                else if (value is long l)
                {
                    cell.SetCellValue(l);
                    cell.CellStyle.DataFormat = cell.Sheet.Workbook.CreateDataFormat().GetFormat("#,##0");
                }
                else if (value is int i)
                {
                    cell.SetCellValue(i);
                    cell.CellStyle.DataFormat = cell.Sheet.Workbook.CreateDataFormat().GetFormat("#,##0");
                }
                else if (value is short o)
                {
                    cell.SetCellValue(o);
                    cell.CellStyle.DataFormat = cell.Sheet.Workbook.CreateDataFormat().GetFormat("#,##0");
                }
                else if (value is DateTime dto)
                {
                    cell.SetCellValue($"{dto:dd.MM.yyyy}");
                    //cell.CellStyle.DataFormat = cell.Sheet.Workbook.CreateDataFormat().GetFormat("dd.MM.yyyy;@");

                }
                else
                {
                    cell.SetCellValue(value.ToString());
                }
            }
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
