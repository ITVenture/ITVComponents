using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.ScriptingTree
{
    public class ScriptTree
    {
        public ScriptTree(TreeNodeType nodeType)
        {
            NodeType = nodeType;
        }

        public TreeNodeType NodeType { get; } = TreeNodeType.Empty;

        public TreeUsage Usage { get; set; } = TreeUsage.Unspecified;

        public TreeOperator Operator { get; set; } = TreeOperator.Unspecified;

        public List<ScriptTree> Children { get; } = new();
        public string Name { get; set; }

        public static readonly ScriptTree Empty = new(TreeNodeType.Empty);
    }

    public enum TreeOperator
    {
        Unspecified,
        Plus,
        Minus,
        Multiply,
        Divide,
        Modulus,
        ShiftLeft,
        ShiftRight,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual
    }

    public enum TreeUsage
    {
        Unspecified,
        Condition,
        LoopBody,
        ForLoopStart,
        ForLoopAction,
        EachLoopVar,
        EachLoopEnum,
        CaseLabel,
        Exception,
        Then,
        Else,
        CaseBlock,
        CaseDefault,
        ReThrow,
        TryBlock,
        CatchBlock,
        FinallyBlock,
        ElementList,
        TypeArgumentList,
        ArgumentList,
        MethodCall,
        AssignmentTarget,
        AssignmentSource,
        LeftOperand,
        RightOperand 
    }

    public enum TreeNodeType
    {
        Empty,
        Block,
        IfStatement,
        DoLoop,
        WhileLoop,
        ForLoop,
        EachLoop,
        Continue,
        Break,
        Return,
        Switch,
        Throw,
        Try,
        ArrayLiteral,
        TypePath,
        TernaryExpression,
        AndAlso,
        OrElse,
        PreIncrement,
        Not,
        PreDecrease,
        UnaryMinus,
        NullableMemberAccess,
        PostDecrease,
        Sequence,
        Assignment,
        Comparison,
        BitXor,
        MathOperation
    }
}
