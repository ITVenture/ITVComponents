using System;
using System.IO;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace ITVComponents.DataExchange.ExcelSource.DictionaryImpl
{
    public class XFormatExcelImportSource:GenericExcelImportSource
    {
        public XFormatExcelImportSource(string fileName) : this(fileName, false)
        {
        }

        public XFormatExcelImportSource(string fileName, bool readNullData) : base(fileName, readNullData)
        {
        }

        #region Overrides of GenericExcelImportSource

        /// <summary>
        /// Opens a Workbook in the current format
        /// </summary>
        /// <param name="sourceFile">the sourcefile that is being imported</param>
        /// <returns></returns>
        protected override IWorkbook OpenWorkbook(string sourceFile)
        {
            using (var stream = File.Open(sourceFile, FileMode.Open, FileAccess.Read))
            {
                return new XSSFWorkbook(stream);
            }
        }

        #endregion
    }
}
