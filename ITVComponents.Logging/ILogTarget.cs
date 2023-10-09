using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.Logging
{
    public interface ILogTarget
    {
        void LogEvent(string eventText, int severity, string context);
    }
}
