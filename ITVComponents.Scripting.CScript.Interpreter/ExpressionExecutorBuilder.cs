using Antlr4.Runtime.Tree;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Interpreter.Model;
using ITVComponents.Scripting.CScript.Operating;
using ITVComponents.Scripting.CScript.ScriptValues;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.AssemblyResolving;
using ITVComponents.Scripting.CScript.Core.Literals;
using ITVComponents.Scripting.CScript.Core.Native;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Scripting.CScript.Interpreter.Model.Arguments;
using ITVComponents.Scripting.CScript.ReflectionHelpers;
using ITVComponents.Scripting.CScript.Security;
using ITVComponents.Scripting.CScript.Security.Restrictions;

namespace ITVComponents.Scripting.CScript.Interpreter
{
    public class ExpressionExecutorBuilder: ITVScriptingBaseVisitor<ScriptExecutor>
    {
        private IScope variables;

        //private ValueBuffer valueBuffer = new ValueBuffer();

        private object switchVal = null;

        private InitializeScopeVariables preparer;

        private bool loopJumpAllowed = false;

        private bool catching = false;

        //private Stack<object> switchStack = new Stack<object>();
        //private Stack<bool> loopJumpAllowed = new Stack<bool>();
        //private Stack<bool> returnSupported = new Stack<bool>();
        private bool returnSupported = true;

        private bool typeSafety = true;

        private bool lazyInvokation = false;

        private bool bypassCompatibilityOnLazyInvokation = false;

        private bool openBlockScope = true;

        private ScriptValue defaultRet;
        private ScriptingPolicy scriptingPolicy;

        public ScriptVisitor()
        {
            variables = new Scope(ScriptingPolicy.Default);
        }

        public ScriptVisitor(IScope baseScope)
        {
            variables = baseScope;
            Reactivateable = false;
        }

        internal ScriptingPolicy ScriptingPolicy
        {
            get => scriptingPolicy;
            set
            {
                scriptingPolicy = value;
                variables.OverridePolicy(value);
            }
        }

        protected override ScriptValue DefaultResult
        {
            get { return defaultRet ?? JSType.Void.Instance; }
        }

        public IDisposable Context { get; internal set; }

        public void ClearScope(IDictionary<string, object> baseValues)
        {
            preparer = null;
            variables.Clear(baseValues);
            loopJumpAllowed = false;
            returnSupported = true;
        }

        public void Prepare(InitializeScopeVariables prepareVariables)
        {
            if (prepareVariables != null)
            {
                prepareVariables(new ScopePreparationCallbackArguments(variables, Context, this));
                preparer = prepareVariables;
            }
        }

        public override ScriptValue VisitProgram(ITVScriptingParser.ProgramContext context)
        {
            return VisitSourceElements(context.sourceElements());
        }

        public override ScriptValue VisitSourceElements(ITVScriptingParser.SourceElementsContext context)
        {
            ScriptValue retVal;
            ITVScriptingParser.SourceElementContext[] elements = context.sourceElement();
            foreach (var element in elements)
            {
                retVal = VisitSourceElement(element);
                if (retVal is IPassThroughValue)
                {
                    return retVal;
                }
            }

            return JSType.Void.Instance;
        }

        /*public ScriptValue VisitSourceElement(ITVScriptingParser.SourceElementContext context)
        {
            return VisitStatement(context.statement());
        }*/

        /*public ScriptValue VisitStatement(ITVScriptingParser.StatementContext context)
        {
            var statement = context.GetChild(0);
            Type t = statement.GetType();
            if (t == typeof (ITVScriptingParser.BlockContext))
            {
                return VisitBlock((ITVScriptingParser.BlockContext) statement);
            }

            if (t == typeof (ITVScriptingParser.BreakStatementContext))
            {
                return VisitBreakStatement((ITVScriptingParser.BreakStatementContext) statement);
            }

            if (t == typeof (ITVScriptingParser.ContinueStatementContext))
            {
                return VisitContinueStatement((ITVScriptingParser.ContinueStatementContext) statement);
            }

            if (t == typeof (ITVScriptingParser.EmptyStatementContext))
            {
                return VisitEmptyStatement((ITVScriptingParser.EmptyStatementContext) statement);
            }

            if (t == typeof (ITVScriptingParser.ExpressionStatementContext))
            {
                return VisitExpressionStatement((ITVScriptingParser.ExpressionStatementContext) statement);
            }

            if (t == typeof (ITVScriptingParser.IfStatementContext))
            {
                return VisitIfStatement((ITVScriptingParser.IfStatementContext) statement);
            }

            if (t == typeof (ITVScriptingParser.DoStatementContext))
            {
                return VisitDoStatement((ITVScriptingParser.DoStatementContext) statement);
            }

            if (t == typeof (ITVScriptingParser.WhileStatementContext))
            {
                return VisitWhileStatement((ITVScriptingParser.WhileStatementContext) statement);
            }

            if (t == typeof (ITVScriptingParser.ForInStatementContext))
            {
                return VisitForInStatement((ITVScriptingParser.ForInStatementContext) statement);
            }

            if (t == typeof (ITVScriptingParser.ForStatementContext))
            {
                return VisitForStatement((ITVScriptingParser.ForStatementContext) statement);
            }

            if (t == typeof (ITVScriptingParser.ReturnStatementContext))
            {
                return VisitReturnStatement((ITVScriptingParser.ReturnStatementContext) statement);
            }

            if (t == typeof (ITVScriptingParser.SwitchStatementContext))
            {
                return VisitSwitchStatement((ITVScriptingParser.SwitchStatementContext) statement);
            }

            if (t == typeof (ITVScriptingParser.ThrowStatementContext))
            {
                return VisitThrowStatement((ITVScriptingParser.ThrowStatementContext) statement);
            }

            if (t == typeof (ITVScriptingParser.TryStatementContext))
            {
                return VisitTryStatement((ITVScriptingParser.TryStatementContext) statement);
            }

            Throw val = new Throw>();
            val.Initialize(string.Format("Unexpected Statement found at {0}/{1}", context.Start.Line,
                                         context.Start.StartIndex), false);
            return val;
        }*/

        public override ScriptValue VisitChildren(IRuleNode node)
        {
            try
            {
                return base.VisitChildren(node);
            }
            finally
            {
                defaultRet = null;
            }
        }

        public override ScriptValue VisitBlock(ITVScriptingParser.BlockContext context)
        {
            bool useScope = openBlockScope;
            openBlockScope = true;
            if (useScope)
            {
                variables.OpenInnerScope();
            }

            try
            {
                ITVScriptingParser.StatementListContext list = context.statementList();
                if (list != null)
                {
                    return VisitStatementList(context.statementList());
                }

                return JSType.Void.Instance;
            }
            finally
            {
                if (useScope)
                {
                    variables.CollapseScope();
                }

                openBlockScope = useScope;
            }
        }

        public override ScriptValue VisitStatementList(ITVScriptingParser.StatementListContext context)
        {
            ITVScriptingParser.StatementContext[] statements = context.statement();
            foreach (ITVScriptingParser.StatementContext statement in statements)
            {
                ScriptValue value = VisitStatement(statement);
                if (value is IPassThroughValue)
                {
                    return value;
                }
            }

            return JSType.Void.Instance;
        }

        public override ScriptValue VisitEmptyStatement(ITVScriptingParser.EmptyStatementContext context)
        {
            return JSType.Void.Instance;
        }

        public override ScriptValue VisitExpressionStatement(ITVScriptingParser.ExpressionStatementContext context)
        {
            return VisitExpressionSequence(context.expressionSequence());
        }

        public override ScriptValue VisitIfStatement(ITVScriptingParser.IfStatementContext context)
        {
#if UseVisitSingleExpression
            ScriptValue val = VisitSingleExpression(context.singleExpression());
#else
            ScriptValue val = Visit(context.singleExpression());
#endif
            if (val is IPassThroughValue)
            {
                return val;
            }
            ITVScriptingParser.StatementContext[] statements = context.statement();
            if (CheckBooleanTrue(val))
            {
                return VisitStatement(statements[0]);
            }

            if (statements.Length > 1)
            {
                return VisitStatement(statements[1]);
            }

            return JSType.Void.Instance;
        }

        public override ScriptValue VisitDoStatement(ITVScriptingParser.DoStatementContext context)
        {
            ITVScriptingParser.StatementContext body = context.statement();
            ITVScriptingParser.SingleExpressionContext condition = context.singleExpression();
            bool loopJumps = loopJumpAllowed;
            loopJumpAllowed = true;
            try
            {
                do
                {
                    ScriptValue tmp = VisitStatement(body);
                    if (tmp is IPassThroughValue && !(tmp is Continue))
                    {
                        if (tmp is Break)
                        {
                            break;
                        }

                        return tmp;
                    }

#if UseVisitSingleExpression
            } while (CheckBooleanTrue(VisitSingleExpression(condition)));
#else
                } while (CheckBooleanTrue(Visit(condition)));
#endif

            }
            finally
            {
                loopJumpAllowed = loopJumps;
            }

            return JSType.Void.Instance;
        }

        /*public override ScriptValue VisitInterpolatedStringLiteral(ITVScriptingParser.InterpolatedStringLiteralContext context)
        {
            string[] singleValues = (from t in context.interpolatedStringParts() select (string)VisitInterpolatedStringParts(t).GetValue(null)).ToArray();

            LiteralScriptValue retVal = new LiteralScriptValue>();
            retVal.SetValue(string.Join(" ", singleValues), null);
            return retVal;
        }

        public override ScriptValue VisitInterpolatedStringParts(ITVScriptingParser.InterpolatedStringPartsContext context)
        {
            ITVScriptingParser.DoubleStringPartContext literal = context.doubleStringPart();
            ITVScriptingParser.SingleExpressionContext complex = context.singleExpression();
            LiteralScriptValue retVal = new LiteralScriptValue>();
            if (literal != null)
            {
                retVal.SetValue(literal.GetText(), null);
            }
            else
            {
                ScriptValue value = VisitSingleExpression(complex);
                retVal.SetValue(
                    string.Format(
                        string.Format("{{0{0}{1}}}", context.StringPadding().GetText(), context.StringFormat().GetText()),
                        value.GetValue(null)),null);
            }

            return retVal;
        }*/

        public override ScriptValue VisitWhileStatement(ITVScriptingParser.WhileStatementContext context)
        {
            ITVScriptingParser.StatementContext body = context.statement();
            ITVScriptingParser.SingleExpressionContext condition = context.singleExpression();
            bool loopJumps = loopJumpAllowed;
            loopJumpAllowed = true;
            try
            {
#if UseVisitSingleExpression
            while (CheckBooleanTrue(VisitSingleExpression(condition)))
                    {
#else
                while (CheckBooleanTrue(Visit(context.singleExpression())))
                {
#endif

                    ScriptValue tmp = VisitStatement(body);
                    if (tmp is IPassThroughValue && !(tmp is Continue))
                    {
                        if (tmp is Break)
                        {
                            break;
                        }

                        return tmp;
                    }
                }
            }
            finally
            {
                loopJumpAllowed = loopJumps;
            }

            return JSType.Void.Instance;
        }

        public override ScriptValue VisitForStatement(ITVScriptingParser.ForStatementContext context)
        {
            ITVScriptingParser.StatementContext body = context.statement();
            ITVScriptingParser.ExpressionSequenceContext[] header = context.expressionSequence();
            if (header.Length != 3)
            {
                Throw t = new Throw();
                t.Initialize(
                    string.Format("Invalid For - Statement at {0}/{1}", context.Start.Line, context.Start.Column),
                    false);
                return t;
            }

            ITVScriptingParser.ExpressionSequenceContext start, condition, loopAction;
            start = header[0];
            condition = header[1];
            loopAction = header[2];
            variables.OpenInnerScope();
            openBlockScope = false;
            try
            {
                bool loopJumps = loopJumpAllowed;
                loopJumpAllowed = true;
                try
                {
                    for (VisitExpressionSequence(start);
                         CheckBooleanTrue(VisitExpressionSequence(condition));
                         VisitExpressionSequence(loopAction))
                    {
                        ScriptValue tmp = VisitStatement(body);
                        if (tmp is IPassThroughValue && !(tmp is Continue))
                        {
                            if (tmp is Break)
                            {
                                break;
                            }

                            return tmp;
                        }
                    }
                }
                finally
                {
                    loopJumpAllowed = loopJumps;
                }
            }
            finally
            {
                openBlockScope = true;
                variables.CollapseScope();
            }

            return JSType.Void.Instance;
        }

        public override ScriptValue VisitForInStatement(ITVScriptingParser.ForInStatementContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] startExpressions = context.singleExpression();
            ITVScriptingParser.StatementContext body = context.statement();
            ITVScriptingParser.SingleExpressionContext runVar = startExpressions[0];
            ITVScriptingParser.SingleExpressionContext enumerableValue = startExpressions[1];
#if UseVisitSingleExpression
            ScriptValue en = VisitSingleExpression(enumerableValue);
#else
            ScriptValue en = Visit(enumerableValue);
#endif
            if (en is IPassThroughValue)
            {
                return en;
            }

#if UseVisitSingleExpression
            ScriptValue targetVal = VisitSingleExpression(runVar);
#else
            ScriptValue targetVal = Visit(runVar);
#endif
            if (targetVal is IPassThroughValue)
            {
                return targetVal;
            }
            variables.OpenInnerScope();
            openBlockScope = false;
            try
            {
                object enumerator = en.GetValue(null, ScriptingPolicy);
                if (!(enumerator is IEnumerable))
                {
                    Throw t = new Throw();
                    t.Initialize(
                        string.Format("Enumerable object required at {0}/{1}", context.Start.Line,
                                      context.Start.Column),
                        false);
                    return t;
                }

                IEnumerable enumerable = (IEnumerable)enumerator;
                bool loopJumps = loopJumpAllowed;
                loopJumpAllowed = true;
                try
                {
                    foreach (object current in enumerable)
                    {
                        targetVal.SetValue(current, null, ScriptingPolicy);
                        ScriptValue tmp = VisitStatement(body);
                        if (tmp is IPassThroughValue && !(tmp is Continue))
                        {
                            if (tmp is Break)
                            {
                                break;
                            }

                            return tmp;
                        }
                    }
                }
                finally
                {
                    loopJumpAllowed = loopJumps;
                }
            }
            finally
            {
                openBlockScope = true;
                variables.CollapseScope();
            }

            return JSType.Void.Instance;
        }

        public override ScriptValue VisitContinueStatement(ITVScriptingParser.ContinueStatementContext context)
        {
            if (loopJumpAllowed)
            {
                return Continue.Instance;
            }

            Throw t = new Throw();
            t.Initialize(
                string.Format(
                    "Invalid usage of Continue found at {0}/{1}",
                    context.Start.Line,
                    context.Start.Column),
                false);
            return t;
        }

        public override ScriptValue VisitBreakStatement(ITVScriptingParser.BreakStatementContext context)
        {
            if (loopJumpAllowed)
            {
                return Break.Instance;
            }

            Throw t = new Throw();
            t.Initialize(
                string.Format(
                    "Invalid usage of Break found at {0}/{1}",
                    context.Start.Line,
                    context.Start.Column),
                false);
            return t;
        }

        public override ScriptValue VisitReturnStatement(ITVScriptingParser.ReturnStatementContext context)
        {
            if (returnSupported)
            {
#if UseVisitSingleExpression
            ScriptValue val = VisitSingleExpression(context.singleExpression());
#else
                ScriptValue val = Visit(context.singleExpression());
#endif
                if (val is IPassThroughValue)
                {
                    return val;
                }

                ReturnValue r = new ReturnValue();
                r.Initialize(val.GetValue(null, ScriptingPolicy));
                return r;
            }

            Throw t = new Throw();
            t.Initialize(
                string.Format(
                    "Invalid usage of Return found at {0}/{1}",
                    context.Start.Line,
                    context.Start.Column),
                false);
            return t;
        }

        public override ScriptValue VisitSwitchStatement(ITVScriptingParser.SwitchStatementContext context)
        {
#if UseVisitSingleExpression
            ScriptValue caseValue = VisitSingleExpression(context.singleExpression());
#else
            ScriptValue caseValue = Visit(context.singleExpression());
#endif
            if (caseValue is IPassThroughValue)
            {
                return caseValue;
            }

            object lastVal = switchVal;
            bool loopJumps = loopJumpAllowed;
            switchVal = caseValue.GetValue(null, ScriptingPolicy);
            try
            {
                loopJumpAllowed = true;
                return VisitCaseBlock(context.caseBlock());
            }
            finally
            {
                switchVal = lastVal;
                loopJumpAllowed = loopJumps;
            }
        }

        public override ScriptValue VisitCaseBlock(ITVScriptingParser.CaseBlockContext context)
        {
            ITVScriptingParser.CaseClausesContext cases = context.caseClauses();
            ITVScriptingParser.DefaultClauseContext defaultClause = context.defaultClause();
            ScriptValue tmp = VisitCaseClauses(cases);
            if (tmp is IPassThroughValue)
            {
                return tmp;
            }

            if (!CheckBooleanTrue(tmp) && defaultClause != null)
            {
                tmp = VisitDefaultClause(defaultClause);
                if (tmp is IPassThroughValue)
                {
                    return tmp;
                }
            }

            return JSType.Void.Instance;
        }

        public override ScriptValue VisitCaseClauses(ITVScriptingParser.CaseClausesContext context)
        {
            ITVScriptingParser.CaseClauseContext[] allCases = context.caseClause();
            bool ok = false;
            foreach (ITVScriptingParser.CaseClauseContext singleCase in allCases)
            {
                ok = true;
                ScriptValue ret = VisitCaseClause(singleCase);
                if (ret is Break)
                {
                    LiteralScriptValue l = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
                    l.Initialize(true);
                    return l;
                }
                if (ret is Continue)
                {
                    switchVal = ret;
                }
                else if (ret is IPassThroughValue)
                {
                    return ret;
                }
                else
                {
                    object obj = ret.GetValue(null, ScriptingPolicy);
                    if (!(obj is bool))
                    {
                        Throw t = new Throw();
                        t.Initialize(
                            string.Format(
                                "Should not fall implicit through Case Labels. Use Continue for falling through {0}/{1}",
                                context.Start.Line,
                                context.Start.Column),
                            false);
                        return t;
                    }
                }
            }

            if (!ok)
            {
                Throw t = new Throw();
                t.Initialize(string.Format("No Cases defined at {0}/{1}", context.Start.Line,
                                           context.Start.Column),
                             false);
                return t;
            }

            LiteralScriptValue v = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            v.Initialize(false);
            return v;
        }

        public override ScriptValue VisitCaseClause(ITVScriptingParser.CaseClauseContext context)
        {
            ITVScriptingParser.SingleExpressionContext expression = context.singleExpression();
            ITVScriptingParser.StatementListContext statements = context.statementList();
#if UseVisitSingleExpression
            ScriptValue val = VisitSingleExpression(expression);
#else
            ScriptValue val = Visit(expression);
#endif
            if (val is IPassThroughValue)
            {
                return val;
            }

            object foundVal = val.GetValue(null, ScriptingPolicy);
            if (switchVal is Continue || (switchVal == null && foundVal == null) ||
                (switchVal != null && foundVal != null && switchVal.Equals(foundVal)))
            {
                return VisitStatementList(statements);
            }

            LiteralScriptValue r = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            r.Initialize(false);
            return r;
        }

        public new ScriptValue VisitDefaultClause(ITVScriptingParser.DefaultClauseContext context)
        {
            return VisitStatementList(context.statementList());
        }

        public override ScriptValue VisitThrowStatement(ITVScriptingParser.ThrowStatementContext context)
        {
            ITVScriptingParser.SingleExpressionContext exception = context.singleExpression();
            if (exception != null)
            {
                Throw t = new Throw();
#if UseVisitSingleExpression
                
#else
                t.Initialize(Visit(exception).GetValue(null, ScriptingPolicy), true);
#endif

                return t;
            }

            if (!catching)
            {
                throw new ScriptException("Illegal Re-Throw statement found!");
            }

            return ReThrow.Instance;
        }

        public override ScriptValue VisitTryStatement(ITVScriptingParser.TryStatementContext context)
        {
            ITVScriptingParser.BlockContext block = context.block();
            ITVScriptingParser.CatchProductionContext catchBlock = context.catchProduction();
            ITVScriptingParser.FinallyProductionContext finallyBlock = context.finallyProduction();
            string name = null;
            if (catchBlock != null)
            {
                name = catchBlock.Identifier().GetText();
            }
            ScriptValue retVal = JSType.Void.Instance;
            try
            {

                ScriptValue value;
                value = VisitBlock(block);
                if (value is Throw)
                {
                    variables.OpenInnerScope();
                    bool isCatching = catching;
                    catching = true;
                    openBlockScope = false;
                    try
                    {
                        if (name != null && ((Throw)value).Catchable)
                        {
                            variables[name] = value.GetValue(null, ScriptingPolicy);
                            value = VisitCatchProduction(catchBlock);
                            if (value is ReThrow)
                            {
                                retVal = value;
                            }
                        }
                        else
                        {
                            retVal = value;
                        }
                    }
                    finally
                    {
                        catching = isCatching;
                        openBlockScope = true;
                        variables.CollapseScope();
                    }
                }
                else
                {
                    retVal = value;
                }
            }
            catch (Exception ex)
            {
                bool isCatching = catching;
                catching = true;
                variables.OpenInnerScope();
                openBlockScope = false;
                try
                {
                    if (name != null)
                    {
                        variables[name] = ex;
                        retVal = VisitCatchProduction(catchBlock);
                        if (retVal is ReThrow)
                        {
                            retVal = new Throw();
                            ((Throw)retVal).Initialize(ex, true);
                        }
                    }
                    else
                    {
                        retVal = new Throw();
                        ((Throw)retVal).Initialize(ex, true);
                    }
                }
                finally
                {
                    catching = isCatching;
                    openBlockScope = true;
                    variables.CollapseScope();
                }
            }
            finally
            {
                if (finallyBlock != null)
                {
                    ScriptValue val = VisitFinallyProduction(finallyBlock);
                    if (val is Throw)
                    {
                        retVal = val;
                    }
                }
            }

            return retVal;
        }

        public override ScriptValue VisitCatchProduction(ITVScriptingParser.CatchProductionContext context)
        {
            return VisitBlock(context.block());
        }

        public override ScriptValue VisitFinallyProduction(ITVScriptingParser.FinallyProductionContext context)
        {
            bool loopJumps = loopJumpAllowed;
            bool ret = returnSupported;
            loopJumpAllowed = false;
            returnSupported = false;
            try
            {
                ScriptValue retVal = VisitBlock(context.block());
                return retVal;
            }
            finally
            {
                loopJumpAllowed = loopJumps;
                returnSupported = ret;
            }
        }

        public override ScriptValue VisitArrayLiteral(ITVScriptingParser.ArrayLiteralContext context)
        {
            var list = context.elementList();
            if (list != null)
            {
                ScriptValue value = VisitElementList(list);
                if (value is SequenceValue)
                {
                    SequenceValue sv = (SequenceValue)value;
                    object[] tmp = new object[sv.Sequence.Length];
                    for (int i = 0; i < tmp.Length; i++)
                    {
                        tmp[i] = sv.Sequence[i].GetValue(null, ScriptingPolicy);
                    }

                    LiteralScriptValue rv = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
                    rv.Initialize(tmp);
                    return rv;
                }

                return value;
            }

            var ret = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation) { ValueType = ValueType.Literal };
            ret.Initialize(Array.Empty<object>());
            return ret;
        }

        public override ScriptValue VisitElementList(ITVScriptingParser.ElementListContext context)
        {
            List<ScriptValue> elements = new List<ScriptValue>();

            foreach (ITVScriptingParser.SingleExpressionContext se in context.singleExpression())
            {
#if UseVisitSingleExpression
            ScriptValue tmp= VisitSingleExpression(se);
#else
                ScriptValue tmp = Visit(se);
#endif
                if (tmp is IPassThroughValue)
                {
                    return tmp;
                }

                elements.Add(tmp);
            }

            SequenceValue rv = new SequenceValue(bypassCompatibilityOnLazyInvokation);
            rv.Initialize(elements.ToArray());
            return rv;
        }

        public override ScriptValue VisitArguments(ITVScriptingParser.ArgumentsContext context)
        {
            return VisitArgumentList(context.argumentList());
        }

        #region Overrides of ITVScriptingBaseVisitor<ScriptValue>

        public override ScriptValue VisitFinalGenerics(ITVScriptingParser.FinalGenericsContext context)
        {
            return VisitTypedArguments(context.typedArguments());
        }

        #endregion

        /*public override ScriptValue (ITVScriptingParser.TypeArgumentsContext context)
        {
            ITVScriptingParser.FinalGenericsContext finalGenerics = context as ITVScriptingParser.FinalGenericsContext;
            if (finalGenerics != null)
                return VisitTypedArguments(finalGenerics.typedArguments());
            ITVScriptingParser.OpenGenericsContext openGenerics = context as ITVScriptingParser.OpenGenericsContext;
            return 
        }*/

        public override ScriptValue VisitTypedArguments(ITVScriptingParser.TypedArgumentsContext context)
        {
            List<ScriptValue> elements = new List<ScriptValue>();
            ITVScriptingParser.TypeIdentifierContext[] types = context.typeIdentifier();
            foreach (ITVScriptingParser.TypeIdentifierContext se in types)
            {
                ScriptValue tmp = VisitTypeIdentifier(se);
                if (tmp is IPassThroughValue)
                {
                    return tmp;
                }

                elements.Add(tmp);
            }

            SequenceValue rv = new SequenceValue(bypassCompatibilityOnLazyInvokation);
            rv.Initialize(elements.ToArray());
            return rv;
        }

        #region Overrides of ITVScriptingBaseVisitor<ScriptValue>

        public override ScriptValue VisitExplicitTypeHint(ITVScriptingParser.ExplicitTypeHintContext context)
        {
            return VisitTypeIdentifier(context.typeIdentifier());
        }

        #endregion

        public override ScriptValue VisitTypeIdentifier(ITVScriptingParser.TypeIdentifierContext context)
        {
            VariableAccessValue retVal = new VariableAccessValue(bypassCompatibilityOnLazyInvokation);
            IScope tmpVar = variables;
            var path = context.Identifier();
            string finalMember = path[path.Length - 1].GetText();
            for (int i = 0; i < path.Length - 1; i++)
            {
                var node = path[i];
                var targetName = node.GetText();
                if (!(tmpVar[targetName] is IScope))
                {
                    throw new ScriptException($"Failed to resolve Type at {context.Start.Line}/{context.Start.Column}");
                }

                tmpVar = (IScope)tmpVar[targetName];
            }
            retVal.Initialize(tmpVar, finalMember);
            return retVal;
        }

        public override ScriptValue VisitArgumentList(ITVScriptingParser.ArgumentListContext context)
        {
            List<ScriptValue> elements = new List<ScriptValue>();
            if (context != null)
            {
                ITVScriptingParser.SingleExpressionContext[] expressions = context.singleExpression();
                if (expressions != null)
                {
                    foreach (ITVScriptingParser.SingleExpressionContext se in expressions)
                    {
#if UseVisitSingleExpression
            ScriptValue tmp = VisitSingleExpression(se);
#else
                        ScriptValue tmp = Visit(se);
#endif
                        if (tmp is IPassThroughValue)
                        {
                            return tmp;
                        }

                        elements.Add(tmp);
                    }
                }
            }

            SequenceValue rv = new SequenceValue(bypassCompatibilityOnLazyInvokation);
            rv.Initialize(elements.ToArray());
            return rv;
        }

        public override ScriptValue VisitExpressionSequence(ITVScriptingParser.ExpressionSequenceContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] sequence = context.singleExpression();
            List<ScriptValue> val = new List<ScriptValue>();
            foreach (var item in sequence)
            {
                ScriptValue retVal;
#if UseVisitSingleExpression
            retVal = VisitSingleExpression(item);
#else
                retVal = Visit(item);
#endif
                if (retVal is IPassThroughValue)
                {
                    return retVal;
                }

                val.Add(retVal);
            }

            if (val.Count == 0)
            {
                return JSType.Void.Instance;
            }

            SequenceValue sv = new SequenceValue(bypassCompatibilityOnLazyInvokation);
            sv.Initialize(val.ToArray());
            return sv;
        }

        public override ScriptValue VisitTernaryExpression(ITVScriptingParser.TernaryExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] values = context.singleExpression();
#if UseVisitSingleExpression
            ScriptValue first = VisitSingleExpression(values[0]);
#else
            ScriptValue first = Visit(values[0]);
#endif
            if (first is IPassThroughValue)
            {
                return first;
            }

            if (CheckBooleanTrue(first))
            {
#if UseVisitSingleExpression
            return VisitSingleExpression(values[1]);
#else
                return Visit(values[1]);
#endif
            }

#if UseVisitSingleExpression
            return VisitSingleExpression(values[2]);
#else
            return Visit(values[2]);
#endif
        }

        public override ScriptValue VisitLogicalAndExpression(ITVScriptingParser.LogicalAndExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] expressions = context.singleExpression();
#if UseVisitSingleExpression
            ScriptValue v1 = VisitSingleExpression(expressions[0]);
#else
            ScriptValue v1 = Visit(expressions[0]);
#endif
            if (v1 is IPassThroughValue)
            {
                return v1;
            }

            if (!CheckBooleanTrue(v1))
            {
                LiteralScriptValue r = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
                r.Initialize(false);
                return r;
            }

#if UseVisitSingleExpression
            ScriptValue v2 = VisitSingleExpression(expressions[1]);
#else
            ScriptValue v2 = Visit(expressions[1]);
#endif
            if (v2 is IPassThroughValue)
            {
                return v2;
            }

            LiteralScriptValue v = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            v.Initialize(CheckBooleanTrue(v2));
            return v;
        }

        public override ScriptValue VisitPreIncrementExpression(ITVScriptingParser.PreIncrementExpressionContext context)
        {
#if UseVisitSingleExpression
            ScriptValue val = VisitSingleExpression(context.singleExpression());
#else
            ScriptValue val = Visit(context.singleExpression());
#endif
            if (val is IPassThroughValue)
            {
                return val;
            }

            object value = val.GetValue(null, ScriptingPolicy);
            try
            {
                value = OperationsHelper.Increment(value);
            }
            catch (Exception ex)
            {
                throw new ScriptException($"Pre-Increment failed at {context.Start.Line}/{context.Start.Column}", ex);
            }
            val.SetValue(value, null, ScriptingPolicy);
            LiteralScriptValue retVal = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            retVal.Initialize(value);
            return retVal;
        }

        public override ScriptValue VisitLogicalOrExpression(ITVScriptingParser.LogicalOrExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] expressions = context.singleExpression();
#if UseVisitSingleExpression
            ScriptValue v1 = VisitSingleExpression(expressions[0]);
#else
            ScriptValue v1 = Visit(expressions[0]);
#endif
            if (v1 is IPassThroughValue)
            {
                return v1;
            }

            if (CheckBooleanTrue(v1))
            {
                LiteralScriptValue r = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
                r.Initialize(true);
                return r;
            }

#if UseVisitSingleExpression
            ScriptValue v2 = VisitSingleExpression(expressions[1]);
#else
            ScriptValue v2 = Visit(expressions[1]);
#endif
            if (v2 is IPassThroughValue)
            {
                return v2;
            }

            LiteralScriptValue v = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            v.Initialize(CheckBooleanTrue(v2));
            return v;
        }

        public override ScriptValue VisitNotExpression(ITVScriptingParser.NotExpressionContext context)
        {
#if UseVisitSingleExpression
            ScriptValue value = VisitSingleExpression(context.singleExpression());
#else
            ScriptValue value = Visit(context.singleExpression());
#endif
            if (value is IPassThroughValue)
            {
                return value;
            }

            LiteralScriptValue rv = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            rv.Initialize(!CheckBooleanTrue(value));
            return rv;
        }

        public override ScriptValue VisitPreDecreaseExpression(ITVScriptingParser.PreDecreaseExpressionContext context)
        {
#if UseVisitSingleExpression
            ScriptValue val = VisitSingleExpression(context.singleExpression());
#else
            ScriptValue val = Visit(context.singleExpression());
#endif
            if (val is IPassThroughValue)
            {
                return val;
            }

            object value = val.GetValue(null, ScriptingPolicy);
            try
            {
                value = OperationsHelper.Decrement(value);
            }
            catch (Exception ex)
            {
                throw new ScriptException($"Pre-Decrement failed at {context.Start.Line}/{context.Start.Column}", ex);
            }
            val.SetValue(value, null, ScriptingPolicy);
            LiteralScriptValue retVal = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            retVal.Initialize(value);
            return retVal;
        }

        public override ScriptValue VisitArgumentsExpression(ITVScriptingParser.ArgumentsExpressionContext context)
        {
#if UseVisitSingleExpression
            ScriptValue baseValue = VisitSingleExpression(context.singleExpression());
#else
            ScriptValue baseValue = Visit(context.singleExpression());
#endif
            if (baseValue is IPassThroughValue)
            {
                return baseValue;
            }

            ScriptValue arguments = VisitArguments(context.arguments());
            if (arguments is IPassThroughValue)
            {
                return arguments;
            }

            ScriptValue typeArguments = null;
            ITVScriptingParser.TypeArgumentsContext targ = context.typeArguments();
            if (targ != null)
            {
                var genericsContext = targ as ITVScriptingParser.FinalGenericsContext;
                if (genericsContext != null)
                {
                    typeArguments = VisitFinalGenerics(genericsContext);
                }
                else
                {
                    Throw th = new Throw();
                    th.Initialize(
                        string.Format("Open Generic Arguments are not supported in Methodcalls! at {0}/{1}",
                            context.Start.Line, context.Start.Column),
                        false);
                    return th;
                }
            }

            ScriptValue explicitTyping = null;
            ITVScriptingParser.ExplicitTypeHintContext ext = context.explicitTypeHint();
            if (ext != null)
            {
                explicitTyping = VisitExplicitTypeHint(ext);
            }

            if (arguments is SequenceValue && (typeArguments == null || typeArguments is SequenceValue))
            {
                baseValue.ValueType = ValueType.Method;
                LiteralScriptValue rv = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
                try
                {
                    rv.Initialize(baseValue.GetValue(new[] { typeArguments, arguments, explicitTyping }, ScriptingPolicy));
                    return rv;
                }
                catch (Exception ex)
                {
                    throw new ScriptException($"Method-Call failed! at {context.Start.Line}/{context.Start.Column}", ex);
                }
            }

            Throw t = new Throw();
            t.Initialize(
                string.Format("Unable to perform method call at {0}/{1}", context.Start.Line, context.Start.Column),
                false);
            return t;
        }

        public override ScriptValue VisitUnaryMinusExpression(ITVScriptingParser.UnaryMinusExpressionContext context)
        {
#if UseVisitSingleExpression
            ScriptValue val = VisitSingleExpression(context.singleExpression());
#else
            ScriptValue val = Visit(context.singleExpression());
#endif
            if (val is IPassThroughValue)
            {
                return val;
            }
            object value = val.GetValue(null, ScriptingPolicy);
            try
            {
                value = OperationsHelper.UnaryMinus(value);
            }
            catch (Exception ex)
            {
                throw new ScriptException($"Unary Minus failed at {context.Start.Line}/{context.Start.Column}", ex);
            }
            LiteralScriptValue retVal = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            retVal.Initialize(value);
            return retVal;
        }

        public override ScriptValue VisitMemberDotQExpression(ITVScriptingParser.MemberDotQExpressionContext context)
        {
#if UseVisitSingleExpression
            ScriptValue baseVal = VisitSingleExpression(context.singleExpression());
#else
            ScriptValue baseVal = Visit(context.singleExpression());
#endif
            if (baseVal is IPassThroughValue)
            {
                return baseVal;
            }

            Type explicitType = null;
            var eth = context.explicitTypeHint();
            if (eth != null)
            {
                explicitType = VisitExplicitTypeHint(eth).GetValue(null, ScriptingPolicy) as Type;
            }

            WeakReferenceMemberAccessValue retVal = new WeakReferenceMemberAccessValue(lazyInvokation ? context : null, bypassCompatibilityOnLazyInvokation, ScriptingPolicy);
            retVal.Initialize(baseVal, context.identifierName().GetText(), explicitType);
            return retVal;
        }

        public override ScriptValue VisitPostDecreaseExpression(ITVScriptingParser.PostDecreaseExpressionContext context)
        {
#if UseVisitSingleExpression
            ScriptValue val = VisitSingleExpression(context.singleExpression());
#else
            ScriptValue val = Visit(context.singleExpression());
#endif
            if (val is IPassThroughValue)
            {
                return val;
            }

            object value = val.GetValue(null, ScriptingPolicy);
            LiteralScriptValue retVal = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            retVal.Initialize(value);
            try
            {
                value = OperationsHelper.Decrement(value);
            }
            catch (Exception ex)
            {
                throw new ScriptException($"Post-Decrement failed at {context.Start.Line}/{context.Start.Column}", ex);
            }

            val.SetValue(value, null, ScriptingPolicy);
            return retVal;
        }

        public override ScriptValue VisitAssignmentExpression(ITVScriptingParser.AssignmentExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
#if UseVisitSingleExpression
            ScriptValue target = VisitSingleExpression(subExpressions[0]);
#else
            ScriptValue target = Visit(subExpressions[0]);
#endif
            if (target is IPassThroughValue)
            {
                return target;
            }

#if UseVisitSingleExpression
            ScriptValue value = VisitSingleExpression(subExpressions[1]);
#else
            ScriptValue value = Visit(subExpressions[1]);
#endif
            if (value is IPassThroughValue)
            {
                return value;
            }

            if (!target.Writable)
            {
                Throw t = new Throw();
                t.Initialize(
                    string.Format("Unable to set the Value at {0}/{1}", context.Start.Line, context.Start.Column),
                    false);
                return t;
            }

            target.SetValue(value.GetValue(null, ScriptingPolicy), null, ScriptingPolicy);
            LiteralScriptValue ret = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            ret.Initialize(target.GetValue(null, ScriptingPolicy));
            return ret;
        }

        public override ScriptValue VisitUnaryPlusExpression(ITVScriptingParser.UnaryPlusExpressionContext context)
        {
#if UseVisitSingleExpression
            ScriptValue v1 = VisitSingleExpression(context.singleExpression());
#else
            ScriptValue v1 = Visit(context.singleExpression());
#endif
            if (v1 is IPassThroughValue)
            {
                return v1;
            }

            LiteralScriptValue ret = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            ret.Initialize(v1.GetValue(null, ScriptingPolicy));
            return ret;
        }

        public override ScriptValue VisitEqualityExpression(ITVScriptingParser.EqualityExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] expressions = context.singleExpression();
#if UseVisitSingleExpression
            ScriptValue leftVal = VisitSingleExpression(expressions[0]);
#else
            ScriptValue leftVal = Visit(expressions[0]);
#endif
            if (leftVal is IPassThroughValue)
            {
                return leftVal;
            }

#if UseVisitSingleExpression
            ScriptValue rightVal = VisitSingleExpression(expressions[1]);
#else
            ScriptValue rightVal = Visit(expressions[1]);
#endif
            if (rightVal is IPassThroughValue)
            {
                return rightVal;
            }
            object left = leftVal.GetValue(null, ScriptingPolicy);
            object right = rightVal.GetValue(null, ScriptingPolicy);
            bool isEqual = (left == null && right == null) || (left != null && left.Equals(right));
            string s = context.GetChild(1).GetText();
            LiteralScriptValue retVal = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            retVal.Initialize(!(isEqual ^ (s == "==")));
            return retVal;
        }

        public override ScriptValue VisitBitXOrExpression(ITVScriptingParser.BitXOrExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
#if UseVisitSingleExpression
            ScriptValue leftVal = VisitSingleExpression(subExpressions[0]);
#else
            ScriptValue leftVal = Visit(subExpressions[0]);
#endif
            if (leftVal is IPassThroughValue)
            {
                return leftVal;
            }

#if UseVisitSingleExpression
            ScriptValue rightVal = VisitSingleExpression(subExpressions[1]);
#else
            ScriptValue rightVal = Visit(subExpressions[1]);
#endif
            if (rightVal is IPassThroughValue)
            {
                return rightVal;
            }

            object value1 = leftVal.GetValue(null, ScriptingPolicy);
            object value2 = rightVal.GetValue(null, ScriptingPolicy);
            LiteralScriptValue retVal = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            if (value1 is bool && value2 is bool)
            {
                retVal.Initialize((bool)value1 ^ (bool)value2);
            }
            else
            {
                try
                {
                    retVal.Initialize(OperationsHelper.Xor(value1, value2, typeSafety));
                }
                catch (Exception ex)
                {
                    throw new ScriptException($"XOR failed at {context.Start.Line}/{context.Start.Column}", ex);
                }
            }

            return retVal;
        }

        public override ScriptValue VisitMultiplicativeExpression(ITVScriptingParser.MultiplicativeExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
#if UseVisitSingleExpression
            ScriptValue leftVal = VisitSingleExpression(subExpressions[0]);
#else
            ScriptValue leftVal = Visit(subExpressions[0]);
#endif
            if (leftVal is IPassThroughValue)
            {
                return leftVal;
            }

#if UseVisitSingleExpression
            ScriptValue rightVal = VisitSingleExpression(subExpressions[1]);
#else
            ScriptValue rightVal = Visit(subExpressions[1]);
#endif
            if (rightVal is IPassThroughValue)
            {
                return rightVal;
            }
            LiteralScriptValue retVal = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            if (lazyInvokation)
            {
                bool ok;
                object obj = context.InvokeExecutor(null, new[] { leftVal, rightVal }, bypassCompatibilityOnLazyInvokation, out ok);
                if (ok)
                {
                    retVal.Initialize(obj);
                    return retVal;
                }
            }

            object value1 = leftVal.GetValue(null, ScriptingPolicy);
            object value2 = rightVal.GetValue(null, ScriptingPolicy);
            string op = context.GetChild(1).GetText();
            switch (op)
            {
                case "*":
                    {
                        try
                        {
                            retVal.Initialize(OperationsHelper.Multiply(value1, value2, typeSafety));
                            if (lazyInvokation)
                            {
                                context.SetPreferredExecutor(new LazyOp(OperationsHelper.Multiply, typeSafety, ScriptingPolicy));
                            }
                            break;
                        }
                        catch (Exception ex)
                        {
                            throw new ScriptException(
                                $"Multiply failed at {context.Start.Line}/{context.Start.Column}", ex);
                        }
                    }
                case "/":
                    {
                        try
                        {
                            retVal.Initialize(OperationsHelper.Divide(value1, value2, typeSafety));
                            if (lazyInvokation)
                            {
                                context.SetPreferredExecutor(new LazyOp(OperationsHelper.Divide, typeSafety, ScriptingPolicy));
                            }
                            break;
                        }
                        catch (Exception ex)
                        {
                            throw new ScriptException(
                                $"Divide failed at {context.Start.Line}/{context.Start.Column}", ex);
                        }
                    }
                case "%":
                    {
                        try
                        {
                            retVal.Initialize(OperationsHelper.Modulus(value1, value2, typeSafety));
                            if (lazyInvokation)
                            {
                                context.SetPreferredExecutor(new LazyOp(OperationsHelper.Modulus, typeSafety, ScriptingPolicy));
                            }
                            break;
                        }
                        catch (Exception ex)
                        {
                            throw new ScriptException(
                                $"Modulus failed at {context.Start.Line}/{context.Start.Column}", ex);
                        }
                    }
                default:
                    {
                        Throw t = new Throw();
                        t.Initialize(
                            string.Format("Unable to perform multiplicative operation at {0}/{1}", context.Start.Line,
                                          context.Start.Column), false);
                        return t;
                    }
            }

            return retVal;
        }

        public override ScriptValue VisitBitShiftExpression(ITVScriptingParser.BitShiftExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
#if UseVisitSingleExpression
            ScriptValue leftVal = VisitSingleExpression(subExpressions[0]);
#else
            ScriptValue leftVal = Visit(subExpressions[0]);
#endif
            if (leftVal is IPassThroughValue)
            {
                return leftVal;
            }

#if UseVisitSingleExpression
            ScriptValue rightVal = VisitSingleExpression(subExpressions[1]);
#else
            ScriptValue rightVal = Visit(subExpressions[1]);
#endif
            if (rightVal is IPassThroughValue)
            {
                return rightVal;
            }

            object value1 = leftVal.GetValue(null, ScriptingPolicy);
            object value2 = rightVal.GetValue(null, ScriptingPolicy);
            string shiftDirection = context.GetChild(1).GetText();
            LiteralScriptValue retVal = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            switch (shiftDirection)
            {
                case "<<":
                    {
                        try
                        {
                            retVal.Initialize(OperationsHelper.LShift(value1, value2));
                            break;
                        }
                        catch (Exception ex)
                        {
                            throw new ScriptException(
                                $"Left-Shift failed at {context.Start.Line}/{context.Start.Column}", ex);
                        }
                    }
                case ">>":
                    {
                        try
                        {
                            retVal.Initialize(OperationsHelper.RShift(value1, value2));
                            break;
                        }
                        catch (Exception ex)
                        {
                            throw new ScriptException(
                                $"Right-Shift failed at {context.Start.Line}/{context.Start.Column}", ex);
                        }
                    }
                default:
                    {
                        Throw t = new Throw();
                        t.Initialize(
                            string.Format("Unable to perform shift operation at {0}/{1}", context.Start.Line,
                                          context.Start.Column), false);
                        return t;
                    }
            }

            return retVal;
        }

        public override ScriptValue VisitParenthesizedExpression(ITVScriptingParser.ParenthesizedExpressionContext context)
        {
#if UseVisitSingleExpression
            return VisitSingleExpression(context.singleExpression());
#else
            return Visit(context.singleExpression());
#endif
        }

        public override ScriptValue VisitAdditiveExpression(ITVScriptingParser.AdditiveExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
#if UseVisitSingleExpression
            ScriptValue leftVal = VisitSingleExpression(subExpressions[0]);
#else
            ScriptValue leftVal = Visit(subExpressions[0]);
#endif
            if (leftVal is IPassThroughValue)
            {
                return leftVal;
            }

#if UseVisitSingleExpression
            ScriptValue rightVal = VisitSingleExpression(subExpressions[1]);
#else
            ScriptValue rightVal = Visit(subExpressions[1]);
#endif
            if (rightVal is IPassThroughValue)
            {
                return rightVal;
            }
            LiteralScriptValue retVal = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);

            if (lazyInvokation)
            {
                bool ok;
                object obj = context.InvokeExecutor(null, new[] { leftVal, rightVal }, bypassCompatibilityOnLazyInvokation, out ok);
                if (ok)
                {
                    retVal.Initialize(obj);
                    return retVal;
                }
            }

            object value1 = leftVal.GetValue(null, ScriptingPolicy);
            object value2 = rightVal.GetValue(null, ScriptingPolicy);
            string op = context.GetChild(1).GetText();
            switch (op)
            {
                case "+":
                    {
                        try
                        {
                            retVal.Initialize(OperationsHelper.Add(value1, value2, typeSafety));
                            if (lazyInvokation)
                            {
                                context.SetPreferredExecutor(new LazyOp(OperationsHelper.Add, typeSafety, ScriptingPolicy));
                            }
                            break;
                        }
                        catch (Exception ex)
                        {
                            throw new ScriptException(
                                $"Add failed at {context.Start.Line}/{context.Start.Column}", ex);
                        }
                    }
                case "-":
                    {
                        try
                        {
                            retVal.Initialize(OperationsHelper.Subtract(value1, value2, typeSafety));
                            if (lazyInvokation)
                            {
                                context.SetPreferredExecutor(new LazyOp(OperationsHelper.Subtract, typeSafety, ScriptingPolicy));
                            }
                            break;
                        }
                        catch (Exception ex)
                        {
                            throw new ScriptException(
                                $"Subtract failed at {context.Start.Line}/{context.Start.Column}", ex);
                        }
                    }
                default:
                    {
                        Throw t = new Throw();
                        t.Initialize(
                            string.Format("Unable to perform additive operation at {0}/{1}", context.Start.Line,
                                          context.Start.Column), false);
                        return t;
                    }
            }

            return retVal;
        }

        public override ScriptValue VisitRelationalExpression(ITVScriptingParser.RelationalExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
#if UseVisitSingleExpression
            ScriptValue leftVal = VisitSingleExpression(subExpressions[0]);
#else
            ScriptValue leftVal = Visit(subExpressions[0]);
#endif
            if (leftVal is IPassThroughValue)
            {
                return leftVal;
            }

#if UseVisitSingleExpression
            ScriptValue rightVal = VisitSingleExpression(subExpressions[1]);
#else
            ScriptValue rightVal = Visit(subExpressions[1]);
#endif
            if (rightVal is IPassThroughValue)
            {
                return rightVal;
            }

            LiteralScriptValue retVal = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            if (lazyInvokation)
            {
                bool ok;
                object obj = context.InvokeExecutor(null, new[] { leftVal, rightVal }, bypassCompatibilityOnLazyInvokation, out ok);
                if (ok)
                {
                    retVal.Initialize(obj);
                    return retVal;
                }
            }

            object value1 = leftVal.GetValue(null, ScriptingPolicy);
            object value2 = rightVal.GetValue(null, ScriptingPolicy);
            string op = context.GetChild(1).GetText();
            if (typeSafety)
            {
                if (value1 is IComparable && value2 is IComparable)
                {
                    try
                    {
                        int compValue = OperationsHelper.Compare(value1, value2, typeSafety);
                        switch (op)
                        {
                            case ">":
                                {
                                    retVal.Initialize(compValue > 0);
                                    break;
                                }
                            case ">=":
                                {
                                    retVal.Initialize(compValue >= 0);
                                    break;
                                }
                            case "<":
                                {
                                    retVal.Initialize(compValue < 0);
                                    break;
                                }
                            case "<=":
                                {
                                    retVal.Initialize(compValue <= 0);
                                    break;
                                }
                            default:
                                {
                                    Throw t = new Throw();
                                    t.Initialize(
                                        string.Format("Unable to perform compare operation at {0}/{1}", context.Start.Line,
                                            context.Start.Column), false);
                                    return t;
                                }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new ScriptException($"Compare failed at {context.Start.Line}/{context.Start.Column}", ex);
                    }
                }
            }
            else
            {
                Func<dynamic, dynamic, bool> d;
                switch (op)
                {
                    case ">":
                        {
                            d = (a, b) => a > b;
                            //retVal.Initialize(compValue > 0);
                            break;
                        }
                    case ">=":
                        {

                            d = (a, b) => a >= b;
                            break;
                        }
                    case "<":
                        {
                            d = (a, b) => a < b;
                            break;
                        }
                    case "<=":
                        {
                            d = (a, b) => a <= b;
                            break;
                        }
                    default:
                        {
                            Throw t = new Throw();
                            t.Initialize(
                                string.Format("Unable to perform compare operation at {0}/{1}", context.Start.Line,
                                    context.Start.Column), false);
                            return t;
                        }

                }

                retVal.Initialize(d(value1, value2));
                if (lazyInvokation)
                {
                    context.SetPreferredExecutor(new LazyOp((a, b, c) => d(a, b), true, ScriptingPolicy));
                }
            }

            return retVal;
        }

        public override ScriptValue VisitPostIncrementExpression(ITVScriptingParser.PostIncrementExpressionContext context)
        {
#if UseVisitSingleExpression
            ScriptValue val = VisitSingleExpression(context.singleExpression());
#else
            ScriptValue val = Visit(context.singleExpression());
#endif
            if (val is IPassThroughValue)
            {
                return val;
            }

            object value = val.GetValue(null, ScriptingPolicy);
            LiteralScriptValue retVal = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            retVal.Initialize(value);
            try
            {
                value = OperationsHelper.Increment(value);
            }
            catch (Exception ex)
            {
                throw new ScriptException($"Post-Increment failed at {context.Start.Line}/{context.Start.Column}", ex);
            }
            val.SetValue(value, null, ScriptingPolicy);
            return retVal;
        }

        public override ScriptValue VisitBitNotExpression(ITVScriptingParser.BitNotExpressionContext context)
        {
#if UseVisitSingleExpression
            ScriptValue subExpression = VisitSingleExpression(context.singleExpression());
#else
            ScriptValue subExpression = Visit(context.singleExpression());
#endif
            if (subExpression is IPassThroughValue)
            {
                return subExpression;
            }

            object d = subExpression.GetValue(null, ScriptingPolicy);
            LiteralScriptValue retVal = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            try
            {
                retVal.Initialize(OperationsHelper.Negate(d));
            }
            catch (Exception ex)
            {
                throw new ScriptException($"Negate failed at {context.Start.Line}/{context.Start.Column}", ex);
            }

            return retVal;
        }

        public override ScriptValue VisitNewImplicitInit(ITVScriptingParser.NewImplicitInitContext context)
        {
            ITVScriptingParser.SingleExpressionContext subExpression = context.singleExpression();
            ScriptValue val = Visit(subExpression);
            if (val is IPassThroughValue)
            {
                return val;
            }

            ScriptValue typeArguments = null;
            ITVScriptingParser.TypeArgumentsContext targs = context.typeArguments();
            if (targs != null)
            {
                var genericsContext = targs as ITVScriptingParser.FinalGenericsContext;
                if (genericsContext != null)
                {
                    typeArguments = VisitFinalGenerics(genericsContext);
                }
                else
                {
                    Throw t = new Throw();
                    t.Initialize(
                        string.Format(
                            "Open Generic Arguments are not supported in final Construction calls! at {0}/{1}",
                            context.Start.Line, context.Start.Column),
                        false);
                    return t;
                }
            }

            if (typeArguments is IPassThroughValue)
            {
                return typeArguments;
            }

            var arguments = Visit(context.objectLiteral());

            if (arguments is LiteralScriptValue && (typeArguments == null || typeArguments is SequenceValue))
            {
                try
                {
                    val.ValueType = ValueType.Constructor;
                    LiteralScriptValue retVal = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
                    var emptyArgs = new SequenceValue(bypassCompatibilityOnLazyInvokation);
                    emptyArgs.Initialize(Array.Empty<ScriptValue>());
                    var raw = val.GetValue(new[] { typeArguments, emptyArgs }, ScriptingPolicy);
                    var obj = arguments.GetValue(null, scriptingPolicy);
                    if (obj is ObjectLiteral oli)
                    {
                        foreach (var item in oli)
                        {
                            raw.SetMemberValue(item.Key, item.Value, null, ValueType.PropertyOrField, scriptingPolicy);
                        }
                    }
                    else
                    {
                        Throw t = new Throw();
                        t.Initialize(
                            string.Format(
                                "Unexpected value provided as initializer! at {0}/{1}",
                                context.Start.Line, context.Start.Column),
                            false);
                        return t;
                    }
                    retVal.Initialize(raw);
                    return retVal;
                }
                catch (Exception ex)
                {
                    throw new ScriptException($"Failed to create new instance at {context.Start.Line}/{context.Start.Column}", ex);
                }
            }

            return arguments;
        }

        public override ScriptValue VisitNewExpression(ITVScriptingParser.NewExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext subExpression = context.singleExpression();
#if UseVisitSingleExpression
            ScriptValue val = VisitSingleExpression(subExpression);
#else
            ScriptValue val = Visit(subExpression);
#endif
            if (val is IPassThroughValue)
            {
                return val;
            }

            ScriptValue arguments = VisitArguments(context.arguments());
            if (arguments is IPassThroughValue)
            {
                return arguments;
            }

            ScriptValue typeArguments = null;
            ITVScriptingParser.TypeArgumentsContext targs = context.typeArguments();
            if (targs != null)
            {
                var genericsContext = targs as ITVScriptingParser.FinalGenericsContext;
                if (genericsContext != null)
                {
                    typeArguments = VisitFinalGenerics(genericsContext);
                }
                else
                {
                    Throw t = new Throw();
                    t.Initialize(
                        string.Format(
                            "Open Generic Arguments are not supported in final Construction calls! at {0}/{1}",
                            context.Start.Line, context.Start.Column),
                        false);
                    return t;
                }
            }

            if (typeArguments is IPassThroughValue)
            {
                return typeArguments;
            }

            if (arguments is SequenceValue && (typeArguments == null || typeArguments is SequenceValue))
            {
                try
                {
                    val.ValueType = ValueType.Constructor;
                    LiteralScriptValue retVal = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
                    var raw = val.GetValue(new[] { typeArguments, arguments }, scriptingPolicy);
                    retVal.Initialize(raw);
                    var literalExt = context.objectLiteral();
                    if (literalExt != null)
                    {
                        var lit = Visit(literalExt);
                        ObjectLiteral oli = null;
                        if (lit is LiteralScriptValue lsv &&
                            (oli = lsv.GetValue(null, scriptingPolicy) as ObjectLiteral) != null)
                        {
                            foreach (var item in oli)
                            {
                                raw.SetMemberValue(item.Key, item.Value, null, ValueType.PropertyOrField, scriptingPolicy);
                            }
                        }
                        else if (lit is not LiteralScriptValue)
                        {
                            return lit;
                        }
                        else
                        {
                            Throw t = new Throw();
                            t.Initialize(
                                string.Format(
                                    "Unexpected value provided as initializer! at {0}/{1}",
                                    context.Start.Line, context.Start.Column),
                                false);
                            return t;
                        }
                    }
                    return retVal;
                }
                catch (Exception ex)
                {
                    throw new ScriptException($"Failed to create new instance at {context.Start.Line}/{context.Start.Column}", ex);
                }
            }

            return arguments;
        }

        public override ScriptValue VisitLiteralExpression(ITVScriptingParser.LiteralExpressionContext context)
        {
            ITVScriptingParser.LiteralContext literal = context.literal();
            return VisitLiteral(literal);
        }

        public override ScriptValue VisitArrayLiteralExpression(ITVScriptingParser.ArrayLiteralExpressionContext context)
        {
            return VisitArrayLiteral(context.arrayLiteral());
        }


        public override ScriptValue VisitHasMemberExpression(ITVScriptingParser.HasMemberExpressionContext context)
        {
            ScriptValue sample = Visit(context.singleExpression());
            if (sample is IPassThroughValue)
            {
                return sample;
            }

            string name = context.identifierName().GetText();
            MemberAccessValue baseValue = new MemberAccessValue(null, bypassCompatibilityOnLazyInvokation, ScriptingPolicy);
            ScriptValue explicitTyping = null;
            ITVScriptingParser.ExplicitTypeHintContext ext = context.explicitTypeHint();
            if (ext != null)
            {
                explicitTyping = VisitExplicitTypeHint(ext);
            }

            baseValue.Initialize(sample, name, explicitTyping?.GetValue(null, ScriptingPolicy) as Type);
            LiteralScriptValue rv = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            if (context.arguments() != null)
            {
                ScriptValue arguments = VisitArguments(context.arguments());
                if (arguments is IPassThroughValue)
                {
                    return arguments;
                }

                ScriptValue typeArguments = null;
                ITVScriptingParser.TypeArgumentsContext targ = context.typeArguments();
                if (targ != null)
                {
                    var genericsContext = targ as ITVScriptingParser.FinalGenericsContext;
                    if (genericsContext != null)
                    {
                        typeArguments = VisitFinalGenerics(genericsContext);
                    }
                    else
                    {
                        Throw th = new Throw();
                        th.Initialize(
                            string.Format("Open Generic Arguments are not supported in Methodcalls! at {0}/{1}",
                                context.Start.Line, context.Start.Column),
                            false);
                        return th;
                    }
                }

                if (arguments is SequenceValue && (typeArguments == null || typeArguments is SequenceValue))
                {
                    baseValue.ValueType = ValueType.Method;
                    try
                    {
                        rv.Initialize(baseValue.CanGetValue(new[] { typeArguments, arguments, explicitTyping }, ScriptingPolicy));
                        return rv;
                    }
                    catch (Exception ex)
                    {
                        throw new ScriptException(
                            $"Method-Lookup failed! at {context.Start.Line}/{context.Start.Column}", ex);
                    }
                }

                rv.Initialize(false);
                return rv;
            }

            rv.Initialize(baseValue.CanGetValue(null, ScriptingPolicy));
            return rv;
        }

        public override ScriptValue VisitMemberIsExpression(ITVScriptingParser.MemberIsExpressionContext context)
        {
            ScriptValue sample = Visit(context.singleExpression(0));
            if (sample is IPassThroughValue)
            {
                return sample;
            }

            object sampleObj = sample.GetValue(null, ScriptingPolicy);
            bool retVal = sampleObj != null;
            if (retVal)
            {
                var typex = context.singleExpression(1);
                ScriptValue expection = Visit(typex);
                if (expection is IPassThroughValue)
                {
                    return expection;
                }

                Type typ = expection.GetValue(null, ScriptingPolicy) as Type;
                if (typ == null)
                {
                    throw new ScriptException($"Type expected at {typex.Start.Line}/{typex.Start.Column}");
                }

                retVal = typ.IsInstanceOfType(sampleObj);
            }

            LiteralScriptValue ret = new LiteralScriptValue(null, false);
            ret.Initialize(retVal);
            return ret;
        }

        public override ScriptValue VisitMemberDotExpression(ITVScriptingParser.MemberDotExpressionContext context)
        {
#if UseVisitSingleExpression
            ScriptValue val = VisitSingleExpression(context.singleExpression());
#else
            ScriptValue val = Visit(context.singleExpression());
#endif
            if (val is IPassThroughValue)
            {
                return val;
            }
            Type explicitType = null;
            var eth = context.explicitTypeHint();
            if (eth != null)
            {
                explicitType = VisitExplicitTypeHint(eth).GetValue(null, ScriptingPolicy) as Type;
            }

            MemberAccessValue retVal = new MemberAccessValue(lazyInvokation ? context : null, bypassCompatibilityOnLazyInvokation, ScriptingPolicy);
            retVal.Initialize(val,
                              context.identifierName().GetText(), explicitType);
            return retVal;
        }

        public override ScriptValue VisitMemberIndexExpression(ITVScriptingParser.MemberIndexExpressionContext context)
        {
#if UseVisitSingleExpression
            ScriptValue baseValue = VisitSingleExpression(context.singleExpression());
#else
            ScriptValue baseValue = Visit(context.singleExpression());
#endif
            if (baseValue is IPassThroughValue)
            {
                return baseValue;
            }
            ScriptValue indexArgs = VisitExpressionSequence(context.expressionSequence());
            if (indexArgs is IPassThroughValue)
            {
                return indexArgs;
            }

            if (!(indexArgs is SequenceValue))
            {
                return indexArgs;
            }

            Type explicitType = null;
            var eth = context.explicitTypeHint();
            if (eth != null)
            {
                explicitType = VisitExplicitTypeHint(eth).GetValue(null, ScriptingPolicy) as Type;
            }

            IndexerScriptValue retVal = new IndexerScriptValue(lazyInvokation ? context : null, ScriptingPolicy, bypassCompatibilityOnLazyInvokation);
            retVal.Initialize(baseValue, ((SequenceValue)indexArgs).Sequence, explicitType);
            return retVal;
        }

        public override ScriptValue VisitInstanceIsNullExpression(ITVScriptingParser.InstanceIsNullExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
#if UseVisitSingleExpression
            ScriptValue left = VisitSingleExpression(subExpressions[0]);
#else
            ScriptValue left = Visit(subExpressions[0]);
#endif
            if (left is IPassThroughValue)
            {
                return left;
            }

#if UseVisitSingleExpression
            ScriptValue right = VisitSingleExpression(subExpressions[1]);
#else
            ScriptValue right = Visit(subExpressions[1]);
#endif
            if (right is IPassThroughValue)
            {
                return right;
            }

            LiteralScriptValue retVal = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            retVal.Initialize(left.GetValue(null, ScriptingPolicy) ?? right.GetValue(null, ScriptingPolicy));
            return retVal;
        }

        public override ScriptValue VisitIdentifierExpression(ITVScriptingParser.IdentifierExpressionContext context)
        {
            VariableAccessValue retVal = new VariableAccessValue(bypassCompatibilityOnLazyInvokation);
            string name = context.Identifier().GetText();
            retVal.Initialize(variables, name);
            return retVal;
        }

        public override ScriptExecutor VisitBitAndExpression(ITVScriptingParser.BitAndExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            var leftVal = Visit(subExpressions[0]);
            var rightVal = Visit(subExpressions[1]);
            return OperationExecutor(context, leftVal, rightVal, BaseOperations.And);
        }

        public override ScriptExecutor VisitBitOrExpression(ITVScriptingParser.BitOrExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            var leftVal = Visit(subExpressions[0]);
            var rightVal = Visit(subExpressions[1]);
            return OperationExecutor(context, leftVal, rightVal, BaseOperations.Or);
        }

        private ScriptExecutor OperationExecutor(IParseTree entireExpression, ScriptExecutor left, ScriptExecutor right,
            BaseOperations operation)
        {
            var baseOp = new ScriptExecutor(entireExpression.SourceInterval.a, entireExpression.SourceInterval.Length)
                { StatusName = ScriptExecutionStatus.Operation };
            left.ElementName = OperationArguments.LeftOperand;
            left.Parent = baseOp;
            baseOp.ChildExecutors.Add(left);
            right.ElementName = OperationArguments.RightOperand;
            right.Parent = baseOp;
            baseOp.ChildExecutors.Add(right);
            baseOp.SetStatusArguments(new OperationArguments { Operation = operation });
            return baseOp;
        }

        public override ScriptExecutor VisitAssignmentOperatorExpression(
            ITVScriptingParser.AssignmentOperatorExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            string op = context.assignmentOperator().GetText();
            var left = Visit(subExpressions[0]);
            var right = Visit(subExpressions[1]);
            BaseOperations operation = BaseOperations.None;
            switch (op)
            {
                case "*=":
                    operation = BaseOperations.Multiply;
                    break;
                case "/=":
                    operation = BaseOperations.Divide;
                    break;
                case "%=":
                    operation = BaseOperations.Modulus;
                    break;
                case "+=":
                    operation = BaseOperations.Add;
                    break;
                case "-=":
                    operation = BaseOperations.Subtract;
                    break;
                case "<<=":
                    operation = BaseOperations.LeftShift;
                    break;
                case ">>=":
                    operation = BaseOperations.RightShift;
                    break;
                case "&=":
                    operation = BaseOperations.And;
                    break;
                case "^=":
                    operation = BaseOperations.Xor;
                    break;
                case "|=":
                    operation = BaseOperations.Or;
                    break;
            }

            var baseOp = new ScriptExecutor(context.SourceInterval.a, context.SourceInterval.Length)
                { StatusName = ScriptExecutionStatus.Operation };
            left.ElementName = OperationArguments.LeftOperand;
            left.Parent = baseOp;
            baseOp.ChildExecutors.Add(left);
            right.ElementName = OperationArguments.RightOperand;
            right.Parent = baseOp;
            baseOp.ChildExecutors.Add(right);
            baseOp.SetStatusArguments(new OperationArguments { Operation = operation });
            left = Visit(subExpressions[0]);
            var assign = new ScriptExecutor(context.SourceInterval.a, context.SourceInterval.Length)
                { StatusName = ScriptExecutionStatus.Assignment };
            left.Parent = assign;
            left.ElementName = AssignArguments.Target;
            assign.ChildExecutors.Add(left);
            baseOp.Parent = assign;
            baseOp.ElementName = AssignArguments.Source;
            assign.ChildExecutors.Add(baseOp);
            return assign;
        }

        public override ScriptExecutor VisitNativeReference(ITVScriptingParser.NativeReferenceContext context)
        {
            if (scriptingPolicy.IsDenied(scriptingPolicy.NativeScripting))
            {
                throw new ScriptException("Native scripting was disabled by policy.");
            }

            string identifier = context.Identifier().GetText();
            string stringLiteral = StringHelper.Parse(context.StringLiteral().GetText());
            NativeScriptHelper.AddReference(identifier, stringLiteral);
            return new ScriptExecutor(context.SourceInterval.a, context.SourceInterval.Length)
                { StatusName = ScriptExecutionStatus.Void };
        }

        public override ScriptExecutor VisitNativeUsing(ITVScriptingParser.NativeUsingContext context)
        {
            if (scriptingPolicy.IsDenied(scriptingPolicy.NativeScripting))
            {
                throw new ScriptException("Native scripting was disabled by policy.");
            }

            string identifier = context.Identifier().GetText();
            string stringLiteral = StringHelper.Parse(context.StringLiteral().GetText());
            NativeScriptHelper.AddUsing(identifier, stringLiteral);
            return new ScriptExecutor(context.SourceInterval.a, context.SourceInterval.Length)
                { StatusName = ScriptExecutionStatus.Void };
        }

        public override ScriptExecutor VisitNativeExpression(ITVScriptingParser.NativeExpressionContext context)
        {
            if (scriptingPolicy.IsDenied(scriptingPolicy.NativeScripting))
            {
                throw new ScriptException("Native scripting was disabled by policy.");
            }

            var expression = context.singleExpression();
            ScriptExecutor v = Visit(expression[0]);
            ScriptExecutor execText = Visit(expression[1]);
            ScriptExecutor parameterObj = Visit(expression[2]);
            //object value = v.GetValue(null, ScriptingPolicy);
            //string text = execText.GetValue(null, ScriptingPolicy) as string;
            var retVal = new ScriptExecutor(context.SourceInterval.a, context.SourceInterval.Length)
            {
                StatusName = ScriptExecutionStatus.NativeExpressionExecute
            };
            v.ElementName = NativeScriptArguments.ExpressionTarget;
            retVal.ChildExecutors.Add(v);
            execText.ElementName = NativeScriptArguments.ExpressionBody;
            retVal.ChildExecutors.Add(execText);
            parameterObj.ElementName = NativeScriptArguments.ExpressionArguments;
            retVal.ChildExecutors.Add(parameterObj);
            string[] identifier = (from t in context.Identifier() select t.GetText()).ToArray();
            retVal.SetStatusArguments(new NativeScriptArguments{Configuration = identifier[1], NameOfTarget = identifier[0] });
            return retVal;
        }

        public override ScriptExecutor VisitNativeLiteralExpression(ITVScriptingParser.NativeLiteralExpressionContext context)
        {
            if (scriptingPolicy.IsDenied(scriptingPolicy.NativeScripting))
            {
                throw new ScriptException("Native scripting was disabled by policy.");
            }

            var expression = context.singleExpression();
            var parameterObj = Visit(expression);
            //var parameters = (parameterObj.GetValue(null, ScriptingPolicy) as ObjectLiteral)?.Snapshot();
            string identifier = context.Identifier().GetText();
            var text = context.NativeCodeLiteral().GetText();
            text = text.Substring(2, text.Length - 3);
            var retVal = new ScriptExecutor(context.SourceInterval.a, context.SourceInterval.Length) { StatusName = ScriptExecutionStatus.NativeLiteralExecute };
            retVal.SetStatusArguments(new NativeScriptArguments{Configuration=identifier, Text=text});
            parameterObj.ElementName = NativeScriptArguments.ExpressionArguments;
            parameterObj.Parent = retVal;
            retVal.ChildExecutors.Add(parameterObj);
            return retVal;
        }

        /*public ScriptValue VisitAssignmentOperator(ITVScriptingParser.AssignmentOperatorContext context)
        {
            throw new NotImplementedException();
        }*/

        public override ScriptExecutor VisitLiteral(ITVScriptingParser.LiteralContext context)
        {
            var child = context.GetChild(0);
            if (child is ITVScriptingParser.NumericLiteralContext)
            {
                return VisitNumericLiteral((ITVScriptingParser.NumericLiteralContext)child);
            }

            if (child is ITVScriptingParser.TypeLiteralContext)
            {
                return VisitTypeLiteral((ITVScriptingParser.TypeLiteralContext)child);
            }

            if (child is ITVScriptingParser.NullLiteralContext)
            {
                return VisitNullLiteral((ITVScriptingParser.NullLiteralContext)child);
            }

            if (child is ITVScriptingParser.RefLiteralContext)
            {
                return VisitRefLiteral((ITVScriptingParser.RefLiteralContext)child);
            }

            var retVal = new ScriptExecutor(context.SourceInterval.a, context.SourceInterval.Length) { StatusName = ScriptExecutionStatus.Literal};
            if (child is ITVScriptingParser.BooleanLiteralContext)
            {
                retVal.SetStatusArguments(new LiteralArguments
                    { Value = child.GetText().Equals("true", StringComparison.OrdinalIgnoreCase) });
            }

            string s = StringHelper.Parse(child.GetText());
            bool flag = false;
            bool isRuntimeSwitch = false;
            ExecutionSwitchType type = ExecutionSwitchType.None;
            if (s.ToUpper() == "@@TYPESAFETY OFF")
            {
                type = ExecutionSwitchType.TypeSafety;
                flag = false;
                isRuntimeSwitch = true;
            }
            else if (s.ToUpper() == "@@TYPESAFETY ON")
            {
                type = ExecutionSwitchType.TypeSafety;
                flag = true;
                isRuntimeSwitch = true;
            }
            else if (s.ToUpper() == "@@LAZYINVOKATION ON")
            {
                type = ExecutionSwitchType.LazyInvokation;
                flag = true;
                isRuntimeSwitch = true;
            }
            else if (s.ToUpper() == "@@LAZYINVOKATION OFF")
            {
                type = ExecutionSwitchType.LazyInvokation;
                flag = false;
                isRuntimeSwitch = true;
            }
            else if (s.ToUpper() == "@@LAZYINVOKATIONSTATICBIND ON")
            {
                type = ExecutionSwitchType.BypassCompatibilityForLazyInvokation;
                flag = true;
                isRuntimeSwitch = true;
            }
            else if (s.ToUpper() == "@@LAZYINVOKATIONSTATICBIND OFF")
            {
                type = ExecutionSwitchType.BypassCompatibilityForLazyInvokation;
                flag = true;
                isRuntimeSwitch = true;
            }

            if (!isRuntimeSwitch)
            {
                retVal.SetStatusArguments(new LiteralArguments { Value = s });
                return retVal;
            }

            var switchArgs = new ExecutionSwitchArguments
            {
                SwitchType = type,
                Flag = flag
            };

            retVal.SetStatusArguments(switchArgs);
            retVal.StatusName = ScriptExecutionStatus.ExecutionSwitch;
            return retVal;
        }

        public override ScriptExecutor VisitNumericLiteral(ITVScriptingParser.NumericLiteralContext context)
        {
            ITerminalNode decimalChild = context.DecimalLiteral();
            ITerminalNode octalChild = context.OctalIntegerLiteral();
            ITerminalNode hexalChild = context.HexIntegerLiteral();
            ScriptExecutor retVal = new ScriptExecutor(context.SourceInterval.a, context.SourceInterval.Length) { StatusName = ScriptExecutionStatus.Literal};
            if (decimalChild != null)
            {
                retVal.SetStatusArguments(new LiteralArguments{Value = OperationsHelper.ParseDecimalValue(decimalChild.GetText()) });
                return retVal;
            }

            if (octalChild != null)
            {
                retVal.SetStatusArguments(new LiteralArguments { Value = Convert.ToInt32(octalChild.GetText(), 8) });
                return retVal;
            }

            if (hexalChild != null)
            {
                retVal.SetStatusArguments(new LiteralArguments { Value = Convert.ToInt32(hexalChild.GetText().Substring(2), 16) });
                return retVal;
            }

            throw new ScriptException(
                $"Unable to create a numeric literal at {context.Start.Line}/{context.Start.Column}");
        }

        public override ScriptExecutor VisitObjectLiteral(ITVScriptingParser.ObjectLiteralContext context)
        {
            var assignments = context.propertyNameAndValueList()?.propertyAssignment();
            List<string> properties = new List<string>();
            var retVal = new ScriptExecutor(context.SourceInterval.a, context.SourceInterval.Length)
            {
                StatusName = ScriptExecutionStatus.ObjectLiteral
            };
            if (assignments != null)
            {
                foreach (ITVScriptingParser.PropertyExpressionAssignmentContext prop in assignments)
                {
                    string name = prop.identifierName().GetText();
                    properties.Add(name);
                    var val = Visit(prop.singleExpression());
                    val.Parent = retVal;
                    val.ElementName = name;
                    retVal.ChildExecutors.Add(val);
                }
            }

            return retVal;
        }

        public override ScriptExecutor VisitFunctionDeclaration(ITVScriptingParser.FunctionDeclarationContext context)
        {
            if (scriptingPolicy.IsDenied(scriptingPolicy.ScriptMethods))
            {
                throw new ScriptException("Implementing script-methods was denied by policy.");
            }

            var tmp = context.formalParameterList()?.Identifier();
            string[] args = [];
            if (tmp != null)
            {
                args = (from t in context.formalParameterList().Identifier() select t.GetText()).ToArray();
            }

            var body = Visit(context.functionBody());
            string identifier = context.Identifier().GetText();
            var retVal = new ScriptExecutor(context.SourceInterval.a, context.SourceInterval.Length)
            {
                StatusName = ScriptExecutionStatus.FunctionLiteral
            };

            body.Parent = retVal;
            body.ElementName = FunctionArguments.FunctionBody;
            retVal.ChildExecutors.Add(body);
            retVal.SetStatusArguments(new FunctionArguments
            {
                Arguments = args,
                Name = identifier
            });

            return retVal;
        }

        public override ScriptExecutor VisitFunctionExpression(ITVScriptingParser.FunctionExpressionContext context)
        {
            if (scriptingPolicy.IsDenied(scriptingPolicy.ScriptMethods))
            {
                throw new ScriptException("Implementing script-methods was denied by policy.");
            }

            var tmp = context.formalParameterList()?.Identifier();
            string[] args = [];
            if (tmp != null)
            {
                args = (from t in context.formalParameterList().Identifier() select t.GetText()).ToArray();
            }

            var body = Visit(context.functionBody());
            string identifier = context.Identifier()?.GetText();

            var retVal = new ScriptExecutor(context.SourceInterval.a, context.SourceInterval.Length)
            {
                StatusName = ScriptExecutionStatus.FunctionLiteral
            };

            body.Parent = retVal;
            body.ElementName = FunctionArguments.FunctionBody;
            retVal.ChildExecutors.Add(body);
            retVal.SetStatusArguments(new FunctionArguments
            {
                Arguments = args,
                Name = identifier
            });

            return retVal;
        }

        public override ScriptExecutor VisitRefLiteral(ITVScriptingParser.RefLiteralContext context)
        {
            object retVal = null;
            ITVScriptingParser.TypeLiteralContext type = context.typeLiteral();
            ScriptExecutor v = VisitTypeLiteral(type);
            var arg = v.GetStatusArguments<TypeLiteralArguments>();
            arg.IsByRef = true;
            return v;
        }


        public override ScriptExecutor VisitNullLiteral(ITVScriptingParser.NullLiteralContext context)
        {
            object retVal = null;
            ScriptExecutor typeEx = null;
            ITVScriptingParser.TypeLiteralContext type = context.typeLiteral();
            if (type != null)
            {
                typeEx = VisitTypeLiteral(type);
            }

            var ret = new ScriptExecutor(context.SourceInterval.a, context.SourceInterval.Length)
            {
                StatusName = ScriptExecutionStatus.Literal
            };
            ret.SetStatusArguments(new LiteralArguments{Value = null});
            if (typeEx != null)
            {
                typeEx.Parent = ret;
                typeEx.ElementName = LiteralArguments.LiteralType;
                ret.ChildExecutors.Add(typeEx);
            }
            //LiteralScriptValue ret = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            //ret.Initialize(retVal);
            return ret;
        }

        public override ScriptExecutor VisitTypeLiteral(ITVScriptingParser.TypeLiteralContext context)
        {
            /*if (scriptingPolicy.IsDenied(scriptingPolicy.TypeLoading))
            {
                var retT = new Throw();
                retT.Initialize("Type-Loading was denied by policy.", false);
                return retT;
            }*/

            StringBuilder type = new StringBuilder(context.typeLiteralIdentifier().GetText());
            ITVScriptingParser.TypeArgumentsContext targs = context.typeArguments();
            ScriptExecutor typeArgs = null;
            Type retVal;
            if (targs != null)
            {
                ITVScriptingParser.FinalGenericsContext finalGenerics = targs as ITVScriptingParser.FinalGenericsContext;
                if (finalGenerics != null)
                {
                    typeArgs = VisitFinalGenerics(finalGenerics);
                    type.Append(string.Format("`{0}", typeArgs.ChildExecutors.Count));
                }
                else
                {
                    type.Append(targs.GetText());
                }
            }

            string assembly = null;
            ITerminalNode path = context.StringLiteral();
            if (path != null)
            {
                assembly = StringHelper.Parse(path.GetText());
                //assembly = assembly.Substring(1, assembly.Length - 2);
            }

            PolicyMode startPolicy = scriptingPolicy.TypeLoading != PolicyMode.Default ? scriptingPolicy.TypeLoading : scriptingPolicy.PolicyMode;
            if (assembly != null)
            {
                var src = AssemblyResolver.FindAssemblyByName(assembly);
                if (scriptingPolicy.IsDenied(src, startPolicy))
                {
                    startPolicy = PolicyMode.Deny;
                }

                retVal = src.GetType(type.ToString());
            }
            else
            {
                retVal = Type.GetType(type.ToString());
            }

            if (scriptingPolicy.IsDenied(retVal, TypeAccessMode.Direct, startPolicy))
            {
                throw new ScriptException($"Access to the type {retVal.FullName} was denied by policy.");
            }

            var retExecutor = new ScriptExecutor(context.SourceInterval.a, context.SourceInterval.Length)
            {
                StatusName = ScriptExecutionStatus.TypeLiteral
            };
            retExecutor.SetStatusArguments(new TypeLiteralArguments{BaseType = retVal});
            if (typeArgs != null && typeArgs.ChildExecutors.Count != 0)
            {
                typeArgs.ElementName = TypeLiteralArguments.GenericArguments;
                typeArgs.Parent = retExecutor;
                retExecutor.ChildExecutors.Add(typeArgs);
            }

            return retExecutor;
        }

    }
}
