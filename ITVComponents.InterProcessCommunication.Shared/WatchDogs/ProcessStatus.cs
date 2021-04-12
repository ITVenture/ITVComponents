using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.Shared.WatchDogs
{
    public class ProcessStatus
    {
        private object additionalMetaData;

        public string ProcessName { get;internal set; }

        public bool RebootRequired{get; internal set; }

        public void MetaData<T>(T metaData)
        {
            additionalMetaData = metaData;
        }

        public T MetaData<T>()
        {
            return (T) additionalMetaData;
        }
    }
}
