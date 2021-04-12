using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess;
using ITVComponents.DataExchange.ExcelSource.ParserLog.Helpers;
using ITVComponents.DataExchange.Import;
using ITVComponents.DataExchange.Import.Diagnostics;
using ITVComponents.Decisions;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using NPOI.SS.UserModel;

namespace ITVComponents.DataExchange.ExcelSource.ParserLog
{
    public abstract class GenericExcelParserEventDumper:ImportDiagnosticDataDumper
    {
        /// <summary>
        /// the workbook instance that was loaded from a file
        /// </summary>
        private IWorkbook workBook;

        /// <summary>
        /// the name of the file
        /// </summary>
        private string fileName;

        /// <summary>
        /// indicates whether the workbook is expected to already exist
        /// </summary>
        private bool existingFile;

        /// <summary>
        /// the sheet into which the reports should be put
        /// </summary>
        private string reportSheet;

        /// <summary>
        /// the sheet into which to put the warnings
        /// </summary>
        private string warningSheet;

        /// <summary>
        /// the sheet into which to put the errors
        /// </summary>
        private string errorSheet;

        /// <summary>
        /// holds a list of sheet-wrappers that are used during the write-process
        /// </summary>
        private Dictionary<string, SheetWrapper> wrappers = new Dictionary<string, SheetWrapper>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the GenericExcelParserEventDumper class
        /// </summary>
        /// <param name="fileName">the name of the target file</param>
        /// <param name="existing">indicates whether the file is expected to already exist</param>
        /// <param name="reportSheet">the sheet into which to put the report messages</param>
        /// <param name="warningSheet">the sheet into which to put the warnings</param>
        /// <param name="errorSheet">the sheet into which to put the errors</param>
        protected GenericExcelParserEventDumper(string fileName, bool existing, string reportSheet, string warningSheet, string errorSheet)
        {
            this.fileName = fileName;
            existingFile = existing;
            this.reportSheet = reportSheet;
            this.warningSheet = warningSheet;
            this.errorSheet = errorSheet;
        }

        /// <summary>
        /// Initializes this dumper for writing events to a target
        /// </summary>
        public override void InitializeForEventDump()
        {
            workBook = OpenWorkbook(fileName, existingFile);
        }

        /// <summary>
        /// Finalizes the Event-Dump on the current target
        /// </summary>
        public override void FinalizeEventDump()
        {
            SaveWorkbook(fileName);
        }

        /// <summary>
        /// Dumps a single event the target of this dumper
        /// </summary>
        /// <param name="record">The Event-Record representing the Parser-Event that must be logged to this dumpers Target</param>
        protected override void DumpEventRecord(ParserEventRecord record)
        {
            if (record.Severity == ParserEventSeverity.Report && reportSheet != null)
            {
                GetSheet(reportSheet).AddRow(record);
            }
            else if (record.Severity == ParserEventSeverity.Warning && warningSheet != null)
            {
                GetSheet(warningSheet).AddRow(record);
            }
            else if (record.Severity == ParserEventSeverity.Error && errorSheet != null)
            {
                GetSheet(errorSheet).AddRow(record);
            }

            LogToOrigin(record);
        }

        /// <summary>
        /// Opens the Workbook file in the desired format
        /// </summary>
        /// <param name="fileName">the target-filename to open</param>
        /// <param name="existing">indicates whether the file is expected to already exist</param>
        /// <returns>a workbook instance representing the provided file</returns>
        protected abstract IWorkbook OpenWorkbook(string fileName, bool existing);

        /// <summary>
        /// Saves the current workbook to the given FileName
        /// </summary>
        /// <param name="fileName">the target filename to save the current workbook to</param>
        protected abstract void SaveWorkbook(string fileName);

        /// <summary>
        /// Gets the sheet with the requested name
        /// </summary>
        /// <param name="name">the expected name of the worksheet</param>
        /// <returns>a sheetwrapper that can be used to modify the data of this sheet</returns>
        private SheetWrapper GetSheet(string name, bool clear = true)
        {
            if (!wrappers.ContainsKey(name))
            {
                var tmp = new SheetWrapper(workBook.GetSheet(name) ?? workBook.CreateSheet(name));
                if (clear)
                {
                    tmp.ClearSheet();
                    tmp.Init();
                }
                else
                {
                    tmp.Analyze();
                }

                wrappers.Add(name, tmp);
            }

            return wrappers[name];
        }

        private void LogToOrigin(ParserEventRecord record)
        {
            DynamicResult dr = record.SourceData as DynamicResult;
            if (dr != null)
            {
                if (dr.ContainsKey("$origin"))
                {
                    string origin = dr["$origin"];
                    int id = origin.LastIndexOf("::", StringComparison.Ordinal);
                    if (id != -1)
                    {
                        string sheetName = origin.Substring(0, id);
                        int line = int.Parse(origin.Substring(id + 2));
                        var sheet = GetSheet(sheetName, false);
                        try
                        {
                            sheet.AddMessage(line, record.Message);
                        }
                        catch (Exception ex)
                        {
                            LogEnvironment.LogEvent(ex.OutlineException(), LogSeverity.Error);
                        }
                    }
                }
            }
        }
    }
}
