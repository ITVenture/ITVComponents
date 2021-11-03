using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using ITVComponents.Scripting.CScript.Evaluators.FlowControl;
using ITVComponents.Scripting.CScript.Exceptions;
using Microsoft.CodeAnalysis.Scripting;

namespace ITVComponents.Scripting.CScript.Evaluators
{
    public class CaseClauseEvaluator:EvaluatorBase
    {
        private readonly SequenceEvaluator statement;
        private List<EvaluatorBase> label;
        private List<EvaluatorBase> statements;
        public IReadOnlyCollection<EvaluatorBase> Label { get; }

        public bool Default { get; }

        public CaseClauseEvaluator(EvaluatorBase label, bool isDefault, SequenceEvaluator statement, ParserRuleContext element) : base(null, null, new[]{statement}, element, null, null)
        {
            this.statement = statement;
            statements = new List<EvaluatorBase>();
            this.label = new List<EvaluatorBase>();
            Label = new ReadOnlyCollection<EvaluatorBase>(this.label);
            SequenceEvaluator st = statement;
            if (label != null)
            {
                EvaluatorBase stm;
                while(st!= null && (stm = st.OneAndOnly) != null)
                {
                    if (stm is not CaseClauseEvaluator cce)
                    {
                        statements.Add(st);
                        st = null;
                    }
                    else
                    {
                        this.label.AddRange(cce.Label);
                        st = cce.statement;
                    }
                }

                if (st != null)
                {
                    if (statements.Count != 0)
                    {
                        throw new ScriptException("Un-Espected Statement-List!");
                    }

                    statements.Add(st);
                }
            }

            Default = isDefault;
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
        public override bool PutValueOnStack { get; } = false;
        protected override object Evaluate(object[] arguments, EvaluationContext context)
        {
            throw new InvalidOperationException("CaseEvaluator should never hit Evaluate!");
        }
    }
}
