using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataExchange.Import;
using ITVComponents.DataExchange.KeyValueTableImport;
using ITVComponents.Decisions;
using ITVComponents.ExtendedFormatting;
using NPOI.SS.UserModel;

namespace ITVComponents.DataExchange.ExcelSource
{
    public abstract class GenericKvExcelImportSource:KeyValueSourceBase
    {
        /// <summary>
        /// the sourcefile for which to get the structured data
        /// </summary>
        private string sourceFile;

        /// <summary>
        /// Initializes a new instance of the GenericExcelImportSource class
        /// </summary>
        /// <param name="fileName"></param>
        protected GenericKvExcelImportSource(string fileName)
        {
            sourceFile = fileName;
        }

        public IDecider<string> ImportWorkSheetDecider { get; } = new SimpleDecider<string>(false);
        #region Overrides of ImportSourceBase<IBasicKeyValueProvider,KeyValueAcceptanceCallbackParameter>

        /// <summary>
        /// Reads consumption-compilant portions of Data from the underlaying data-source
        /// </summary>
        /// <returns>an IEnumerable of the base-set</returns>
        protected override IEnumerable<IBasicKeyValueProvider> ReadData()
        {
            IWorkbook book = OpenWorkbook(sourceFile);
            bool foundSheet = false;
            for (int i = 0; i < book.NumberOfSheets; i++)
            {
                string msg;
                ISheet sheet = book.GetSheetAt(i);
                if ((ImportWorkSheetDecider.Decide(sheet.SheetName, DecisionMethod.Simple, out msg) & (DecisionResult.Acceptable | DecisionResult.Success)) != DecisionResult.None)
                {
                    foundSheet = true;
                    yield return
                        new DictionaryWrapper(new Dictionary<string, object>
                        {
                            {
                                "SheetName",
                                sheet.SheetName
                            }
                        });
                    foreach (IRow row in sheet)
                    {
                        if (row != null)
                        {
                            Dictionary<string, object> vals = new Dictionary<string, object>();
                            foreach (ICell cell in row.Cells)
                            {
                                XLNumber num = cell.ColumnIndex;
                                object value = GetCellValue(cell, cell.CellType);
                                if (value != null)
                                {
                                    vals.Add(num.ToString(), value);
                                }
                            }

                            string origin = $"{sheet.SheetName}::{row.RowNum}";
                            vals.Add("$origin", origin);
                            yield return new DictionaryWrapper(vals);
                        }
                    }
                }
            }

            if (!foundSheet)
            {
                LogParserEvent(null, $@"No sheet has met the configured Constraints.
Settings for sheet-search: {ImportWorkSheetDecider}", ParserEventSeverity.Warning, null);
            }
        }

        /// <summary>
        /// Opens a Workbook in the current format
        /// </summary>
        /// <param name="sourceFile">the sourcefile that is being imported</param>
        /// <returns></returns>
        protected abstract IWorkbook OpenWorkbook(string sourceFile);

        /// <summary>
        /// Evaluates the value of the given cell
        /// </summary>
        /// <param name="cell">the current cell that is being read</param>
        /// <param name="type">the provided celltype for the given cell</param>
        /// <returns>the appropriate value of the given cell</returns>
        private object GetCellValue(ICell cell, CellType type)
        {
            object value = null;
            switch (type)
            {
                case CellType.Blank:
                    {
                        value = null;
                        break;
                    }
                case CellType.Boolean:
                    {
                        value = cell.BooleanCellValue;
                        break;
                    }
                case CellType.Numeric:
                    {
                        if (DateUtil.IsCellDateFormatted(cell))
                        {
                            value = cell.DateCellValue;
                        }
                        else
                        {
                            value = cell.NumericCellValue;
                        }

                        break;
                    }
                case CellType.String:
                    {
                        value = cell.RichStringCellValue.String;
                        if (string.IsNullOrEmpty((string) value))
                        {
                            value = null;
                        }

                        break;
                    }
                case CellType.Formula:
                {
                    if (cell.CachedFormulaResultType != CellType.Formula)
                    {
                        value = GetCellValue(cell, cell.CachedFormulaResultType);
                    }
                    else
                    {
                        value = "[RECURSIVE_FUNCTION_ERROR]";
                    }
                    break;
                    }
                case CellType.Error:
                case CellType.Unknown:
                    {
                        value = "[ERROR]";
                        break;
                    }
            }

            return value;
        }

        #endregion
    }
}
