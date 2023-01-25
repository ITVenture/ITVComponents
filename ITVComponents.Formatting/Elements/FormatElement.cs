using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Formatting.Elements
{
    class FormatElement : IFormatElement
    {
        #region Implementation of IFormatElement

        public int Start { get; set; }
        public int Length { get; set; }
        public StringBuilder Content { get; } = new StringBuilder();
        public StringBuilder FormatHint { get; } = new StringBuilder();
        public StringBuilder FormatLength { get; } = new StringBuilder();
        public CodeType CodeType { get; set; } = CodeType.Expression;
        public bool IsRecursive { get; set; }
        public StringBuilder RecursionDepthString { get; } = new StringBuilder();
        public int RecursionDepth { get; set; } = 1;

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