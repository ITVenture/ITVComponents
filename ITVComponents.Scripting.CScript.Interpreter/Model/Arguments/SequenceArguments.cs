using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Interpreter.Model.Arguments
{
    internal class SequenceArguments : IExecutorArgument
    {
        public const string SequenceItem = "SequenceItem";
        public bool VoidWhenEmpty { get; set; }
        public SequenceType SequenceType { get; set; } = SequenceType.ExpressionSequence;
    }

    internal enum SequenceType
    {
        ExpressionSequence,
        Array
    }
}
