using System;
using System.IO;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;

namespace ITVComponents.DataExchange.ExcelSource.DictionaryImpl
{
    public class LegacyExcelImportSource:GenericExcelImportSource
    {
        public LegacyExcelImportSource(string fileName) : this(fileName, false)
        {
        }

        public LegacyExcelImportSource(string fileName, bool readNullData) : base(fileName, readNullData)
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
                return new HSSFWorkbook(stream);
            }
        }

        #endregion
    }
}
