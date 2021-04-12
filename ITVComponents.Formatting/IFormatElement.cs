using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Formatting
{
    internal interface IFormatElement
    {
        int Start { get; set; }

        int Length { get; set; }

        string Content { get; set; }
    }
}
