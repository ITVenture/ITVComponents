using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.DataExchange.KeyValueImport.Config
{
    [Serializable]
    public class CsvImportConfiguration
    {
        public string Name { get; set; }

        public bool ValuesWrapped { get; set; } = true;

        public string ValueStartCharacter { get; set; } = "\"";

        public string ValueEndCharacter { get; set; } = "\"";

        public string ValueWrapperEscapes { get; set; } = @"\\\""";

        public string CsvSeparator { get; set; } = ",";

        public string EscapeStrategyName { get; set; } = "Default";

        public string Encoding { get; set; } = "Utf-8";

        public List<TypeConversion> TypeConversions { get; set; } = new List<TypeConversion>
        {
            new TypeConversion
            {
                RawValuePattern = @"^\d+\.\d+$",
                ParseExpression="'System.Decimal'.Parse(RawText)"
            },
            new TypeConversion
            {
                RawValuePattern = @"^\d+$",
                ParseExpression="'System.Int64'.Parse(RawText)"
            },
            new TypeConversion
            {
                RawValuePattern = @"^\d{2}.\d{2}\.\d{4}$",
                ParseExpression = "'System.DateTime'.ParseExact(RawText,\"dd.MM.yyyy\",'System.Globalization.CultureInfo'.InvariantCulture)"
            },
            new TypeConversion
            {
                RawValuePattern = @"^\d{2}.\d{2}\.\d{4}\s*\d{2}:\d{2}:\d{2}$",
                ParseExpression = "'System.DateTime'.ParseExact(RawText,\"dd.MM.yyyy HH:mm:ss\",'System.Globalization.CultureInfo'.InvariantCulture)"
            }
        };
        
    }

    public class TypeConversion
    {
        public string RawValuePattern { get; set; }

        public string ParseExpression { get; set; }
    }
}
