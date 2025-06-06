using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Cookies
{
    public class DefaultCookieOptions
    {
        public int LengthToBufferThreashold { get; set; } = 1024;
        public int BufferValidityDays { get; set; } = 1;
    }
}
