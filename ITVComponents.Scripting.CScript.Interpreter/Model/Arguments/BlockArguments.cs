using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Interpreter.Model.Arguments
{
    internal class BlockArguments : IExecutorArgument
    {
        public const string Statement = "Statement";
        public const string SourceElement = "SourceElement";
        public BlockType BlockType { get; set; } = BlockType.StatementList;
    }

    internal enum BlockType
    {
        StatementList,
        CodeBlock,
        TryBlock,
        CatchBlock,
        FinallyBlock,
        LoopBlock,
        SourceElementList
    }
}
