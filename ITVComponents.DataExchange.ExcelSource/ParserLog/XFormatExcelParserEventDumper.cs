using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace ITVComponents.DataExchange.ExcelSource.ParserLog
{
    public class XFormatExcelParserEventDumper:GenericExcelParserEventDumper
    {
        /// <summary>
        /// holds the current instance of the workbook
        /// </summary>
        private XSSFWorkbook currentWb;

        /// <summary>
        /// Initializes a new instance of the XFormatExcelParserEventDumper class
        /// </summary>
        /// <param name="fileName">the name of the target file</param>
        /// <param name="existing">indicates whether the file is expected to already exist</param>
        /// <param name="reportSheet">the sheet into which to put the report messages</param>
        /// <param name="warningSheet">the sheet into which to put the warnings</param>
        /// <param name="errorSheet">the sheet into which to put the errors</param>
        public XFormatExcelParserEventDumper(string fileName, bool existing, string reportSheet, string warningSheet, string errorSheet) : base(fileName, existing, reportSheet, warningSheet, errorSheet)
        {
        }

        #region Overrides of GenericExcelParserEventDumper

        /// <summary>
        /// Opens the Workbook file in the desired format
        /// </summary>
        /// <param name="fileName">the target-filename to open</param>
        /// <param name="existing">indicates whether the file is expected to already exist</param>
        /// <returns>a workbook instance representing the provided file</returns>
        protected override IWorkbook OpenWorkbook(string fileName, bool existing)
        {
            if (existing && !File.Exists(fileName))
            {
                throw new InvalidOperationException("The requested file does not exist");
            }

            if (existing || File.Exists(fileName))
            {
                using (var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                {
                    return currentWb = new XSSFWorkbook(stream);
                }
            }
            else
            {
                return currentWb = new XSSFWorkbook();
            }
        }

        /// <summary>
        /// Saves the current workbook to the given FileName
        /// </summary>
        /// <param name="fileName">the target filename to save the current workbook to</param>
        protected override void SaveWorkbook(string fileName)
        {
            using (var stream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            {
                currentWb.Write(stream);
            }
        }

        #endregion
    }
}
