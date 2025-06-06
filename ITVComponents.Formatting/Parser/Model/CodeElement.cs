using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ICustomFormatter = ITVComponents.Formatting.CustomFormat.ICustomFormatter;

namespace ITVComponents.Formatting.Parser.Model
{
    public class CodeElement
    {
        public bool IsBlock { get; set; }

        public string Code { get; set; }

        public int RecursionDepth { get; set; }

        public ICustomFormatter CustomFormatter { get; set; }
    }
}
