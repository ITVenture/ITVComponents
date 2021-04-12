using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataExchange.KeyValueTableImport;
using ITVComponents.ExtendedFormatting;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;

namespace ITVComponents.DataExchange.ExcelSource
{
    public class LegacyExcelImportSource:GenericKvExcelImportSource
    {
        public LegacyExcelImportSource(string fileName) : base(fileName)
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
