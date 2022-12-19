using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.DynamicData.Models
{
    public class FilterModel
    {
        public FilterModel[] Children { get; set; }

        public string GroupOp { get; set; }

        public string BinaryOp { get; set; }

        public object Value { get; set; }
        public string ColumnName { get; set; }
    }
}
