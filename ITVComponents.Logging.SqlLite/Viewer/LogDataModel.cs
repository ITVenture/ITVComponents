using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using ITVComponents.DataAccess.Models;

namespace ITVComponents.Logging.SqlLite.Viewer
{
    public class LogDataModel
    {
        public int Severiy { get; set; }

        [DbColumn("Severity", ValueResolveExpression = "'ITVComponents.Logging.SqlLite.Helpers.AwesomeHelper@@\"ITVComponents.Logging.SqlLite.dll\"'.IconForSeverity(value)")]
        public FontAwesome.Sharp.Icon SeverityImage { get; set; }

        [DbColumn("Severity", ValueResolveExpression = "'ITVComponents.Logging.SqlLite.Helpers.AwesomeHelper@@\"ITVComponents.Logging.SqlLite.dll\"'.BrushForSeverity(value)")]
        public Brush SeverityImageColor { get; set; }

        public DateTime EventTime { get; set; }

        public string EventContext { get; set; }

        public string EventText { get; set; }

        [DbColumn("EventText", ValueResolveExpression = @"(function(v){
    i = v.IndexOf(""\r\n"");
    if (i == -1){
        return v;
    }
    
    return v.Substring(0,i) + ""..."";
})(value)")]
        public string FirstEventLine { get; set; }
    }
}
