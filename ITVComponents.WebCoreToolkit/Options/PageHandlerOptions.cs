using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Options
{
    public class PageHandlerOptions
    {
        public string PageModelTypeName { get; set; }

        public string HandlerInterfaceTypeName { get; set; }

        public string HandlerImplementationTypeName { get; set; }

        public bool UpdateExisting { get; set; }
    }
}
