using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ITVComponents.DataExchange.Configuration
{
    public class DumpFormatFileCollection : List<DumpFormatFile>
    {
        public DumpFormatFile this[string name]
        {
            get { return this.FirstOrDefault(n => n.ConfigName == name); }
        }
    }
}
