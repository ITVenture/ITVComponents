using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Invokation
{
    public class ConsoleOutputInfo
    {
        public ConsoleOutputInfo(string output, Process runningProcess)
        {
            Output = output;
            RunningProcess = runningProcess;
        }

        public string Output { get; }

        public Process RunningProcess { get; }
    }
}
