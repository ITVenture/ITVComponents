using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using ITVComponents.Logging;
using ITVComponents.Scripting.CScript.Evaluators.FlowControl;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Scripting.CScript.Operating;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ITVComponents.Scripting.CScript.Evaluators
{
    public class LiteralEvaluator:EvaluatorBase
    {
        private object value;



        public LiteralEvaluator(string literalValue, LiteralType type, SequenceEvaluator typeChildren, string assemblyPath, ParserRuleContext context) : base(null,null,typeChildren==null?null:new[]{typeChildren},context,null,null)
        {
            switch (type)
            {
                case LiteralType.Boolean:
                    value = literalValue.Equals("true", StringComparison.OrdinalIgnoreCase);
                    break;
                case LiteralType.String:

                    value = StringHelper.Parse(literalValue);
                    break;
                case LiteralType.Decimal:
                    value = OperationsHelper.ParseDecimalValue(literalValue);
                    break;
                case LiteralType.OctalInt:
                    value = Convert.ToInt32(literalValue, 8);
                    break;
                case LiteralType.HexalInt:
                    value = Convert.ToInt32(literalValue, 16);
                    break;
                case LiteralType.Type:
                    if (!string.IsNullOrEmpty(assemblyPath))
                    {
                        var src = NamedAssemblyResolve.LoadAssembly(assemblyPath);
                        value = src.GetType(literalValue);
                    }
                    else
                    {
                        value = Type.GetType(literalValue);
                    }

                    break;
            }
        }

        public LiteralEvaluator(string literalValue, LiteralType type, ParserRuleContext context) : this(literalValue,
            type, null, null, context)
        {
        }

        public override AccessMode AccessMode
        {
            get
            {
                return AccessMode.Read;
            }
            internal set
            {
                if ((value & AccessMode.Write) == AccessMode.Write)
                {
                    throw new InvalidOperationException("This is a read-only evaluator!");
                }
            }
        }

        public override ResultType ExpectedResult
        {
            get
            {
                return ResultType.Literal;
            }
            internal set
            {
                if (value != ResultType.Literal)
                {
                    throw new InvalidOperationException("This is a literal-only evaluator!");
                }
            }
        }

        public override bool PutValueOnStack { get; } = true;
        protected override object Evaluate(object[] arguments, EvaluationContext context)
        {
            if (value is string s)
            {
                if (s.ToUpper() == "@@TYPESAFETY OFF")
                {
                    context.TypeSafety = false;
                }
                else if (s.ToUpper() == "@@TYPESAFETY ON")
                {
                    context.TypeSafety = true;
                }
                else if (s.ToUpper() == "@@LAZYINVOKATION ON")
                {
                    context.LazyEvaluation = true;
                }
                else if (s.ToUpper() == "@@LAZYINVOKATION OFF")
                {
                    context.LazyEvaluation = false;
                }
                else if (s.ToUpper() == "@@LAZYINVOKATIONSTATICBIND ON")
                {
                    LogEnvironment.LogDebugEvent("Ignored obsolete Lazyness instruction", LogSeverity.Warning);
                    //context. = true;
                }
                else if (s.ToUpper() == "@@LAZYINVOKATIONSTATICBIND OFF")
                {
                    LogEnvironment.LogDebugEvent("Ignored obsolete Lazyness instruction", LogSeverity.Warning);
                    //bypassCompatibilityOnLazyInvokation = false;
                }
            }

            if (value is Type t && arguments.Length == 1 && arguments[0] is object[] p)
            {
                var typArg = p.Cast<Type>().ToArray();
                return t.MakeGenericType(typArg);
            }

            return value;
        }
    }

    public enum LiteralType
    {
        Boolean,
        String,
        Decimal,
        OctalInt,
        HexalInt,
        Type
    }
}
