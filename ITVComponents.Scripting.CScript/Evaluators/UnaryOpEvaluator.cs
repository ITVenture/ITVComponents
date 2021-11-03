using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using ITVComponents.Scripting.CScript.Evaluators.FlowControl;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Operating;

namespace ITVComponents.Scripting.CScript.Evaluators
{
    public class UnaryOpEvaluator:EvaluatorBase
    {
        private readonly OpPosition position;
        private readonly string op;
        private IAssignableEvaluator assigner;

        public UnaryOpEvaluator(EvaluatorBase baseValue, OpPosition position, string op, ParserRuleContext context) : base(null,null,new[]{baseValue},context,null,null)
        {
            this.position = position;
            this.op = op;
            assigner = baseValue as IAssignableEvaluator;
        }

        public override ResultType ExpectedResult { get; internal set; }
        public override AccessMode AccessMode { get; internal set; }
        public override bool PutValueOnStack { get; } = true;
        protected override object Evaluate(object[] arguments, EvaluationContext context)
        {
            var baseVal = arguments[0];
            if (baseVal is object[] ag && (op == "++" || op == "--"))
            {
                if (assigner == null)
                {
                    throw new ScriptException("Assignable Evaluator expected!");
                }
                var ori = ag[0];
                var xcremented = op == "++" ? OperationsHelper.Increment(ori) : OperationsHelper.Decrement(ori);
                var writerAction = (ActiveCodeAccessDescriptor)ag[1];
                assigner.Assign(xcremented, writerAction);
                return position==OpPosition.Pre?xcremented:ori;
            }

            if (baseVal is object[])
            {
                throw new InvalidOperationException("Read-Only evaluator expected");
            }

            if (op == "++" || op == "--")
            {
                throw new InvalidOperationException("Assignment instructions expected!");
            }

            if (position != OpPosition.Post)
            {
                throw new InvalidOperationException("Prepending Unary operator expected!");
            }

            switch (op)
            {
                case "-":
                    return OperationsHelper.UnaryMinus(baseVal);
                case "~":
                    return OperationsHelper.Negate(baseVal);
                case "!":
                    return !(baseVal is true);
            }

            throw new ScriptException("Unexpected operator!");
        }
    }

    public enum OpPosition
    {
        Pre,
        Post
    }
}
