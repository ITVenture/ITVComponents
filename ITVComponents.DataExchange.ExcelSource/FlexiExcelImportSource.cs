using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace ITVComponents.DataExchange.ExcelSource
{
    public class FlexiExcelImportSource:GenericKvExcelImportSource
    {
        private readonly FlexOpeningPrecedence precedence;

        /// <summary>
        /// Initializes a new instance of the FlexiExcelImportSource class
        /// </summary>
        /// <param name="fileName">the file-name to open</param>
        /// <param name="precedence">the preferred format</param>
        public FlexiExcelImportSource(string fileName, FlexOpeningPrecedence precedence = FlexOpeningPrecedence.XFormat) : base(fileName)
        {
            this.precedence = precedence;
        }

        /// <summary>
        /// Opens a Workbook in the current format
        /// </summary>
        /// <param name="sourceFile">the sourcefile that is being imported</param>
        /// <returns></returns>
        protected override IWorkbook OpenWorkbook(string sourceFile)
        {
            Func<Stream, IWorkbook> open1 = s => new XSSFWorkbook(s);
            Func<Stream, IWorkbook> open2 = s => new HSSFWorkbook(s);
            if (precedence == FlexOpeningPrecedence.LegacyFormat)
            {
                var tmp = open1;
                open1 = open2;
                open2 = tmp;
            }

            using (var stream = File.Open(sourceFile, FileMode.Open, FileAccess.Read))
            {
                try
                {
                    return open1(stream);
                }
                catch
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    return open2(stream);
                }
            }
        }
    }
}
