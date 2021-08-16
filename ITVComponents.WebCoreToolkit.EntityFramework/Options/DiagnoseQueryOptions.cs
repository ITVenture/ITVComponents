using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Helpers;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Options
{
    public class DiagnoseQueryOptions
    {
        public Dictionary<string, DiagnoseQueryHelper.DiagQueryItem> KnownQueries { get; set; }
        public string Area { get; set; }
        public IDictionary<string, object> Arguments { get; set; }
    }
}
