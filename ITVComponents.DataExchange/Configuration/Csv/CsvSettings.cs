using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.DataExchange.Configuration.Csv
{
    public class CsvSettings
    {
        public string CsvSeparator { get; set; } = ",";

        public string ValueStartCharacter { get; set; }= "\"";

        public string ValueEndCharacter { get; set; }= "\"";

        public string Encoding { get; set; } = "UTF-8";

        public bool TableHeader { get; set; } = false;

        public List<CsvEscapeSetting> EscapeSettings { get;set; } = new List<CsvEscapeSetting>
        {
            new CsvEscapeSetting
            {
                MatchRegex = @"(?<badChar>[""\\])",
                RegexReplaceExpression = @"\${badChar}"
            },
            new CsvEscapeSetting
            {
                MatchRegex = @"\r",
                RegexReplaceExpression=@"\\r"
            },
            new CsvEscapeSetting
            {
                MatchRegex = @"\n",
                RegexReplaceExpression=@"\\n"
            }
        };

        public List<CsvColumnFormat> Formattings { get; set; } = new List<CsvColumnFormat>();

    }

    public class CsvEscapeSetting
    {
        public string MatchRegex { get; set; }

        public string RegexReplaceExpression { get; set; }
    }

    public class CsvColumnFormat
    {
        public string ColumnName { get; set; }

        public string Format { get; set; }
    }
}
