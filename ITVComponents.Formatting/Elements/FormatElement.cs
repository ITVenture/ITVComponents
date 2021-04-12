using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Formatting.Elements
{
    class FormatElement:IFormatElement
    {
        #region Implementation of IFormatElement

        public int Start { get; set; }
        public int Length { get; set; }
        public string Content { get; set; } = "";
        public string FormatHint { get; set; } = "";
        public string FormatLength { get; set; } = "";
        public CodeType CodeType { get; set; } = CodeType.Expression;
        #endregion
    }

    internal enum CodeType
    {
        Expression,
        Block,
        RecursiveExpression,
        RecursiveBlock
    }
}
