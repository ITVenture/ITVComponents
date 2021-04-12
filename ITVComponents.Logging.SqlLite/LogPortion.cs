using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Logging.SqlLite
{
    internal class LogPortion
    {
        public DateTime EventTime { get; set; }

        public int Severity { get; set; }

        public string EventText { get; set; }

        public string Context { get; set; }
    }
}
