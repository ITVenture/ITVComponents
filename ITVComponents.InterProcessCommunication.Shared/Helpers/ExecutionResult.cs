using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.Shared.Helpers
{
    public class ExecutionResult
    {
        public string ActionName { get; set; }

        public object Result { get; set; }

        public object[] Parameters { get; set; }
    }
}
