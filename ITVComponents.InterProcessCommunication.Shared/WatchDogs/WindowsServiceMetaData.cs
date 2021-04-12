using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.Shared.WatchDogs
{
    public class WindowsServiceMetaData
    {
        public string ServiceName { get; set; }

        public string DisplayName { get; set; }

        public string MachineName { get; set; }

        public bool RegularShutdown { get; set; }
    }
}
