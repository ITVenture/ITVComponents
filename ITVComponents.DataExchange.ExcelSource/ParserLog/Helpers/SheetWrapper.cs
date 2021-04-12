using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.DataExchange.Import;
using NPOI.SS.UserModel;

namespace ITVComponents.DataExchange.ExcelSource.ParserLog.Helpers
{
    public class SheetWrapper
    {
        /// <summary>
        /// holds the wrapped sheet
        /// </summary>
        private ISheet wrappedSheet;

        /// <summary>
        /// Holds the index of the next line
        /// </summary>
        private int currentLine;

        /// <summary>
        /// the Header-Row, that is used to add new columns
        /// </summary>
        private IRow headerRow;

        /// <summary>
        /// all column names and their ordinal order
        /// </summary>
        private List<string> columns = new List<string>();

        /// <summary>
        /// Initializes a new instance of the SheetWrapper class
        /// </summary>
        /// <param name="target">the sheet instance that is wrapped</param>
        public SheetWrapper(ISheet target)
        {
            wrappedSheet = target;
        }

        /// <summary>
        /// Clears all existing data from the sheet
        /// </summary>
        public void ClearSheet()
        {
            List<IRow> rows = new List<IRow>();
            foreach (IRow o in wrappedSheet)
            {
                rows.Add(o);
            }

            rows.ForEach(wrappedSheet.RemoveRow);
            currentLine = 0;
            columns.Clear();
        }

        /// <summary>
        /// Initializes the sheet and reates a new header
        /// </summary>
        public void Init()
        {
            if (currentLine == 0)
            {
                headerRow = NextLine();
            }
        }

        /// <summary>
        /// Analyzes an existing sheet for its content
        /// </summary>
        public void Analyze()
        {
            headerRow = wrappedSheet.GetRow(0);
            columns.Clear();
            foreach (var cell in headerRow.Cells)
            {
                columns.Add(cell.StringCellValue);
            }

            PushColumn("Messages");
        }

        /// <summary>
        /// Adds a new row to this excel sheet
        /// </summary>
        /// <param name="record">the parser-event that must be added to this sheetWrapper</param>
        public void AddRow(ParserEventRecord record)
        {
            var source = record.SourceData as DynamicResult;
            CheckIndexColumns(source);
            var row = NextLine();
            (from t in columns.Select((s, i) => new {Index = i, Name = s})
                select new {Column = t.Index, Value = t.Name == "Message" ? record.Message : source?[t.Name]}).ForEach(
                u => SetValue(row.CreateCell(u.Column), $"{u.Value}"));
        }

        /// <summary>
        /// Adds a message to the original table from an import
        /// </summary>
        /// <param name="line">the line where to place the message</param>
        /// <param name="message">the message to add</param>
        public void AddMessage(int line, string message)
        {
            var row = wrappedSheet.GetRow(line);
            int id = columns.FindIndex(n => n.Equals("Messages", StringComparison.OrdinalIgnoreCase));
            var cell = row.GetCell(id, MissingCellPolicy.CREATE_NULL_AS_BLANK);
            string txt = cell.StringCellValue;
            if (!string.IsNullOrEmpty(txt))
            {
                message = $@"{txt}
{message}";
            }

            cell.SetCellValue(message);
        }

        /// <summary>
        /// SetValue wrapper to avoid the calling of the method with null-objects
        /// </summary>
        /// <param name="cell"></param>
        /// <param name="value"></param>
        private void SetValue(ICell cell, string value)
        {
            if (value != null)
            {
                cell.SetCellValue(value);
            }
        }

        /// <summary>
        /// Creates a new Line
        /// </summary>
        /// <returns>the row-instance for the new line</returns>
        private IRow NextLine()
        {
            return wrappedSheet.CreateRow(currentLine++);
        }

        /// <summary>
        /// Checks the index-columns with the ones of this sheet
        /// </summary>
        /// <param name="sourceData"></param>
        private void CheckIndexColumns(DynamicResult sourceData)
        {
            sourceData?.Keys.Where(n => !columns.Contains(n,StringComparer.OrdinalIgnoreCase)).ForEach(PushColumn);
            PushColumn("Message");
        }

        /// <summary>
        /// Pushes a new column into this sheet
        /// </summary>
        /// <param name="name">the name of the new column</param>
        private void PushColumn(string name)
        {
            if (!columns.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                columns.Add(name);
                headerRow.CreateCell(columns.Count - 1).SetCellValue(name);
            }
        }
    }
}
