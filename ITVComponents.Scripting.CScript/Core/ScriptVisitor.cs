﻿//#define UseVisitSingleExpression

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Antlr4.Runtime.Tree;
using ITVComponents.AssemblyResolving;
using ITVComponents.Scripting.CScript.Buffering;
using ITVComponents.Scripting.CScript.Core.Literals;
using ITVComponents.Scripting.CScript.Core.Native;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Scripting.CScript.Operating;
using ITVComponents.Scripting.CScript.Optimization;
using ITVComponents.Scripting.CScript.Optimization.LazyExecutors;
using ITVComponents.Scripting.CScript.ScriptValues;
using ITVComponents.Scripting.CScript.Security;
using ITVComponents.Scripting.CScript.Security.Restrictions;
using Newtonsoft.Json.Linq;
using ValueType = ITVComponents.Scripting.CScript.ScriptValues.ValueType;
using Void = ITVComponents.Scripting.CScript.ScriptValues.Void;

namespace ITVComponents.Scripting.CScript.Core
{
    public class ScriptVisitor : ITVScriptingBaseVisitor<ScriptValue>
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
            get { return defaultRet ?? Void.Instance; }
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

            return Void.Instance;
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

                return Void.Instance;
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

            return Void.Instance;
        }

        public override ScriptValue VisitEmptyStatement(ITVScriptingParser.EmptyStatementContext context)
        {
            return Void.Instance;
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

            return Void.Instance;
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

            return Void.Instance;
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

            return Void.Instance;
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

            return Void.Instance;
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

            return Void.Instance;
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

            return Void.Instance;
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
            ScriptValue retVal = Void.Instance;
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

            var ret = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation){ValueType= ValueType.Literal};
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
                return Void.Instance;
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
                    var raw = val.GetValue(new[] { typeArguments, arguments },scriptingPolicy);
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

        public override ScriptValue VisitBitAndExpression(ITVScriptingParser.BitAndExpressionContext context)
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
                retVal.Initialize((bool)value1 & (bool)value2);
                return retVal;
            }

            try
            {
                retVal.Initialize(OperationsHelper.And(value1, value2, typeSafety));
            }
            catch (Exception ex)
            {
                throw new ScriptException($"AND failed at {context.Start.Line}/{context.Start.Column}", ex);
            }

            return retVal;
        }

        public override ScriptValue VisitBitOrExpression(ITVScriptingParser.BitOrExpressionContext context)
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
                retVal.Initialize((bool)value1 | (bool)value2);
                return retVal;
            }

            try
            {
                retVal.Initialize(OperationsHelper.Or(value1, value2, typeSafety));
            }
            catch (Exception ex)
            {
                throw new ScriptException($"OR failed at {context.Start.Line}/{context.Start.Column}", ex);
            }
            return retVal;
        }

        public override ScriptValue VisitAssignmentOperatorExpression(
            ITVScriptingParser.AssignmentOperatorExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            string op = context.assignmentOperator().GetText();
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

            object retVal = null;
            try
            {
                bool ok = false;
                if (lazyInvokation)
                {
                    retVal = context.InvokeExecutor(null, new[] { left, right }, bypassCompatibilityOnLazyInvokation, out ok);
                    if (ok)
                    {
                        left.SetValue(retVal, null, ScriptingPolicy);
                    }
                }

                if (!ok)
                {
                    object v1, v2;
                    v1 = left.GetValue(null, ScriptingPolicy);
                    //left.ReleaseItem();
                    v2 = right.GetValue(null, ScriptingPolicy);
                    switch (op)
                    {
                        case "*=":
                            {
                                left.SetValue(retVal = OperationsHelper.Multiply(v1, v2, typeSafety), null, ScriptingPolicy);
                                if (lazyInvokation)
                                {
                                    context.SetPreferredExecutor(new LazyOp(OperationsHelper.Multiply, typeSafety, ScriptingPolicy));
                                }

                                break;
                            }
                        case "/=":
                            {
                                left.SetValue(retVal = OperationsHelper.Divide(v1, v2, typeSafety), null, ScriptingPolicy);
                                if (lazyInvokation)
                                {
                                    context.SetPreferredExecutor(new LazyOp(OperationsHelper.Divide, typeSafety, ScriptingPolicy));
                                }

                                break;
                            }
                        case "%=":
                            {
                                left.SetValue(retVal = OperationsHelper.Modulus(v1, v2, typeSafety), null, ScriptingPolicy);
                                if (lazyInvokation)
                                {
                                    context.SetPreferredExecutor(new LazyOp(OperationsHelper.Modulus, typeSafety, ScriptingPolicy));
                                }

                                break;
                            }
                        case "+=":
                            {
                                left.SetValue(retVal = OperationsHelper.Add(v1, v2, typeSafety), null, ScriptingPolicy);
                                if (lazyInvokation)
                                {
                                    context.SetPreferredExecutor(new LazyOp(OperationsHelper.Add, typeSafety, ScriptingPolicy));
                                }

                                break;
                            }
                        case "-=":
                            {
                                left.SetValue(retVal = OperationsHelper.Subtract(v1, v2, typeSafety), null, ScriptingPolicy);
                                if (lazyInvokation)
                                {
                                    context.SetPreferredExecutor(new LazyOp(OperationsHelper.Subtract, typeSafety, ScriptingPolicy));
                                }

                                break;
                            }
                        case "<<=":
                            {
                                left.SetValue(retVal = OperationsHelper.LShift(v1, v2), null, ScriptingPolicy);
                                if (lazyInvokation)
                                {
                                    context.SetPreferredExecutor(new LazyOp(OperationsHelper.LShift, typeSafety, ScriptingPolicy));
                                }

                                break;
                            }
                        case ">>=":
                            {
                                left.SetValue(retVal = OperationsHelper.RShift(v1, v2), null, ScriptingPolicy);
                                if (lazyInvokation)
                                {
                                    context.SetPreferredExecutor(new LazyOp(OperationsHelper.RShift, typeSafety, ScriptingPolicy));
                                }
                                break;
                            }
                        case "&=":
                            {
                                if (v1 is bool && v2 is bool)
                                {
                                    left.SetValue(retVal = (bool)v1 & (bool)v2, null, ScriptingPolicy);
                                    if (lazyInvokation)
                                    {
                                        context.SetPreferredExecutor(new LazyOp((a, b, c) => (bool)a & (bool)b, typeSafety, ScriptingPolicy));
                                    }
                                }
                                else
                                {
                                    left.SetValue(retVal = OperationsHelper.And(v1, v2, typeSafety), null, ScriptingPolicy);
                                    if (lazyInvokation)
                                    {
                                        context.SetPreferredExecutor(new LazyOp(OperationsHelper.And, typeSafety, ScriptingPolicy));
                                    }
                                }

                                break;
                            }
                        case "^=":
                            {
                                if (v1 is bool && v2 is bool)
                                {
                                    left.SetValue(retVal = (bool)v1 ^ (bool)v2, null, ScriptingPolicy);
                                    if (lazyInvokation)
                                    {
                                        context.SetPreferredExecutor(new LazyOp((a, b, c) => (bool)a ^ (bool)b, typeSafety, ScriptingPolicy));
                                    }
                                }
                                else
                                {
                                    left.SetValue(retVal = OperationsHelper.Xor(v1, v2, typeSafety), null, ScriptingPolicy);
                                    if (lazyInvokation)
                                    {
                                        context.SetPreferredExecutor(new LazyOp(OperationsHelper.Xor, typeSafety, ScriptingPolicy));
                                    }
                                }
                                break;
                            }
                        case "|=":
                            {
                                if (v1 is bool && v2 is bool)
                                {
                                    left.SetValue(retVal = (bool)v1 | (bool)v2, null, ScriptingPolicy);
                                    if (lazyInvokation)
                                    {
                                        context.SetPreferredExecutor(new LazyOp((a, b, c) => (bool)a | (bool)b, typeSafety, ScriptingPolicy));
                                    }
                                }
                                else
                                {
                                    left.SetValue(retVal = OperationsHelper.Or(v1, v2, typeSafety), null, ScriptingPolicy);
                                    if (lazyInvokation)
                                    {
                                        context.SetPreferredExecutor(new LazyOp(OperationsHelper.Or, typeSafety, ScriptingPolicy));
                                    }
                                }
                                break;
                            }

                    }
                }
            }
            catch (Exception ex)
            {
                throw new ScriptException($"{op} failed at {context.Start.Line}/{context.Start.Column}", ex);
            }

            if (retVal == null)
            {
                Throw t = new Throw();
                t.Initialize(
                    string.Format("Unable to perform Assignment operation at {0}/{1}", context.Start.Line,
                                  context.Start.Column), false);
                return t;
            }

            LiteralScriptValue lrv = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            lrv.Initialize(retVal);
            return lrv;
        }

        public override ScriptValue VisitNativeReference(ITVScriptingParser.NativeReferenceContext context)
        {
            if (scriptingPolicy.IsDenied(scriptingPolicy.NativeScripting))
            {
                var ret = new Throw();
                ret.Initialize("Native scripting was disabled by policy.", false);
                return ret;
            }

            string identifier = context.Identifier().GetText();
            string stringLiteral = StringHelper.Parse(context.StringLiteral().GetText());
            NativeScriptHelper.AddReference(identifier, stringLiteral);
            return Void.Instance;
        }

        public override ScriptValue VisitNativeUsing(ITVScriptingParser.NativeUsingContext context)
        {
            if (scriptingPolicy.IsDenied(scriptingPolicy.NativeScripting))
            {
                var ret = new Throw();
                ret.Initialize("Native scripting was disabled by policy.", false);
                return ret;
            }

            string identifier = context.Identifier().GetText();
            string stringLiteral = StringHelper.Parse(context.StringLiteral().GetText());
            NativeScriptHelper.AddUsing(identifier, stringLiteral);
            return Void.Instance;
        }

        public override ScriptValue VisitNativeExpression(ITVScriptingParser.NativeExpressionContext context)
        {
            if (scriptingPolicy.IsDenied(scriptingPolicy.NativeScripting))
            {
                var ret = new Throw();
                ret.Initialize("Native scripting was disabled by policy.", false);
                return ret;
            }

            var expression = context.singleExpression();
            ScriptValue v = Visit(expression[0]);
            if (v is IPassThroughValue)
            {
                return v;
            }

            ScriptValue execText = Visit(expression[1]);
            if (execText is IPassThroughValue)
            {
                return execText;
            }

            ScriptValue parameterObj = Visit(expression[2]);
            if (parameterObj is IPassThroughValue)
            {
                return parameterObj;
            }

            object value = v.GetValue(null, ScriptingPolicy);
            string text = execText.GetValue(null, ScriptingPolicy) as string;
            if (text == null)
            {
                throw new InvalidOperationException("string value expected for linq execution!");
            }

            var parameters = (parameterObj.GetValue(null, ScriptingPolicy) as ObjectLiteral)?.Snapshot();
            string[] identifier = (from t in context.Identifier() select t.GetText()).ToArray();
            object result = NativeScriptHelper.RunLinqQuery(identifier[1], value, identifier[0], text, parameters);
            LiteralScriptValue lrv = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            lrv.Initialize(result);
            return lrv;
        }

        public override ScriptValue VisitNativeLiteralExpression(ITVScriptingParser.NativeLiteralExpressionContext context)
        {
            if (scriptingPolicy.IsDenied(scriptingPolicy.NativeScripting))
            {
                var ret = new Throw();
                ret.Initialize("Native scripting was disabled by policy.", false);
                return ret;
            }

            var expression = context.singleExpression();
            ScriptValue parameterObj = Visit(expression);
            if (parameterObj is IPassThroughValue)
            {
                return parameterObj;
            }

            var parameters = (parameterObj.GetValue(null, ScriptingPolicy) as ObjectLiteral)?.Snapshot();
            string identifier = context.Identifier().GetText();
            var text = context.NativeCodeLiteral().GetText();
            text = text.Substring(2, text.Length - 3);
            object result = NativeScriptHelper.RunLinqQuery(identifier, text, parameters);
            LiteralScriptValue lrv = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            lrv.Initialize(result);
            return lrv;
        }

        /*public ScriptValue VisitAssignmentOperator(ITVScriptingParser.AssignmentOperatorContext context)
        {
            throw new NotImplementedException();
        }*/

        public override ScriptValue VisitLiteral(ITVScriptingParser.LiteralContext context)
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

            LiteralScriptValue retVal = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            if (child is ITVScriptingParser.BooleanLiteralContext)
            {
                retVal.Initialize(child.GetText().Equals("true", StringComparison.OrdinalIgnoreCase));
                return retVal;
            }

            string s = StringHelper.Parse(child.GetText());
            if (s.ToUpper() == "@@TYPESAFETY OFF")
            {
                typeSafety = false;
            }
            else if (s.ToUpper() == "@@TYPESAFETY ON")
            {
                typeSafety = true;
            }
            else if (s.ToUpper() == "@@LAZYINVOKATION ON")
            {
                lazyInvokation = true;
            }
            else if (s.ToUpper() == "@@LAZYINVOKATION OFF")
            {
                lazyInvokation = false;
            }
            else if (s.ToUpper() == "@@LAZYINVOKATIONSTATICBIND ON")
            {
                bypassCompatibilityOnLazyInvokation = true;
            }
            else if (s.ToUpper() == "@@LAZYINVOKATIONSTATICBIND OFF")
            {
                bypassCompatibilityOnLazyInvokation = false;
            }

            retVal.Initialize(s);
            return retVal;
        }

        public override ScriptValue VisitNumericLiteral(ITVScriptingParser.NumericLiteralContext context)
        {
            ITerminalNode decimalChild = context.DecimalLiteral();
            ITerminalNode octalChild = context.OctalIntegerLiteral();
            ITerminalNode hexalChild = context.HexIntegerLiteral();
            LiteralScriptValue retVal = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            if (decimalChild != null)
            {
                retVal.Initialize(OperationsHelper.ParseDecimalValue(decimalChild.GetText()));
                return retVal;
            }

            if (octalChild != null)
            {
                retVal.Initialize(Convert.ToInt32(octalChild.GetText(), 8));
                return retVal;
            }

            if (hexalChild != null)
            {
                retVal.Initialize(Convert.ToInt32(hexalChild.GetText().Substring(2), 16));
                return retVal;
            }

            Throw t = new Throw();
            t.Initialize(
                string.Format("Unable to create a numeric literal at {0}/{1}", context.Start.Line,
                              context.Start.Column), false);
            return retVal;
        }

        public override ScriptValue VisitObjectLiteral(ITVScriptingParser.ObjectLiteralContext context)
        {
            var assignments = context.propertyNameAndValueList()?.propertyAssignment();
            Dictionary<string, object> objectRaw = new Dictionary<string, object>();
            if (assignments != null)
            {
                foreach (ITVScriptingParser.PropertyExpressionAssignmentContext prop in assignments)
                {
                    string name = prop.identifierName().GetText();
                    var val = Visit(prop.singleExpression());
                    objectRaw[name] = val.GetValue(null, ScriptingPolicy);
                }
            }

            ObjectLiteral retVal = new ObjectLiteral(objectRaw, variables);
            foreach (KeyValuePair<string, object> item in objectRaw.ToArray())
            {
                if (item.Value is FunctionLiteral lit)
                {
                    lit = lit.Copy();
                    retVal[item.Key] = lit;
                    lit.ParentScope = retVal;
                }
            }

            LiteralScriptValue v = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            v.Initialize(retVal);
            return v;
        }

        public override ScriptValue VisitFunctionDeclaration(ITVScriptingParser.FunctionDeclarationContext context)
        {
            if (scriptingPolicy.IsDenied(scriptingPolicy.ScriptMethods))
            {
                var ret = new Throw();
                ret.Initialize("Implementing script-methods was denied by policy.", false);
                return ret;
            }

            Dictionary<string, object> initial = variables.Snapshot();
            var tmp = context.formalParameterList()?.Identifier();
            string[] args = { };
            if (tmp != null)
            {
                args = (from t in context.formalParameterList().Identifier() select t.GetText()).ToArray();
            }

            FunctionLiteral function = new FunctionLiteral(initial, args, context.functionBody(), ScriptingPolicy);
            if (variables is FunctionScope)
            {
                function.ParentScope = ((FunctionScope)variables).ParentScope;
            }

            string identifier = context.Identifier().GetText();
            LiteralScriptValue retVal = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            retVal.Initialize(function);
            variables[identifier] = function;
            return retVal;
        }


        public override ScriptValue VisitFunctionExpression(ITVScriptingParser.FunctionExpressionContext context)
        {
            if (scriptingPolicy.IsDenied(scriptingPolicy.ScriptMethods))
            {
                var ret = new Throw();
                ret.Initialize("Implementing script-methods was denied by policy.", false);
                return ret;
            }

            Dictionary<string, object> initial = variables.Snapshot();
            try
            {
                var tmp = context.formalParameterList()?.Identifier();
                string[] args = { };
                if (tmp != null)
                {
                    args = (from t in context.formalParameterList().Identifier() select t.GetText()).ToArray();
                }

                FunctionLiteral function = new FunctionLiteral(initial, args, context.functionBody(), ScriptingPolicy);
                if (variables is FunctionScope)
                {
                    function.ParentScope = ((FunctionScope)variables).ParentScope;
                }

                string identifier = context.Identifier()?.GetText();
                LiteralScriptValue retVal = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
                retVal.Initialize(function);
                if (identifier != null)
                {
                    variables[identifier] = function;
                }

                return retVal;
            }
            finally
            {
                initial.Clear();
            }
        }

        #region Overrides of ITVScriptingBaseVisitor<ScriptValue>

        public override ScriptValue VisitRefLiteral(ITVScriptingParser.RefLiteralContext context)
        {
            object retVal = null;
            ITVScriptingParser.TypeLiteralContext type = context.typeLiteral();
            ScriptValue v = VisitTypeLiteral(type);
            retVal = new ReferenceWrapper() { Type = ((Type)v.GetValue(null, ScriptingPolicy)).MakeByRefType() };

            LiteralScriptValue ret = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            ret.Initialize(retVal);
            return ret;
        }

        #endregion

        public override ScriptValue VisitNullLiteral(ITVScriptingParser.NullLiteralContext context)
        {
            object retVal = null;
            ITVScriptingParser.TypeLiteralContext type = context.typeLiteral();
            if (type != null)
            {
                ScriptValue v = VisitTypeLiteral(type);
                retVal = new TypedNull { Type = (Type)v.GetValue(null, ScriptingPolicy) };
            }

            LiteralScriptValue ret = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            ret.Initialize(retVal);
            return ret;
        }

        public override ScriptValue VisitTypeLiteral(ITVScriptingParser.TypeLiteralContext context)
        {
            /*if (scriptingPolicy.IsDenied(scriptingPolicy.TypeLoading))
            {
                var retT = new Throw();
                retT.Initialize("Type-Loading was denied by policy.", false);
                return retT;
            }*/

            StringBuilder type = new StringBuilder(context.typeLiteralIdentifier().GetText());
            ITVScriptingParser.TypeArgumentsContext targs = context.typeArguments();
            ScriptValue[] typeArgs = null;
            Type retVal;
            if (targs != null)
            {
                ITVScriptingParser.FinalGenericsContext finalGenerics = targs as ITVScriptingParser.FinalGenericsContext;
                if (finalGenerics != null)
                {
                    ScriptValue tmp = VisitFinalGenerics(finalGenerics);
                    if (tmp is IPassThroughValue)
                    {
                        return tmp;
                    }

                    SequenceValue seq = (SequenceValue)tmp;
                    typeArgs = seq.Sequence;
                    type.Append(string.Format("`{0}", typeArgs.Length));
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

            PolicyMode startPolicy = scriptingPolicy.TypeLoading!= PolicyMode.Default?scriptingPolicy.TypeLoading:scriptingPolicy.PolicyMode;
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
                var retT = new Throw();
                retT.Initialize($"Access to the type {retVal.FullName} was denied by policy.", false);
                return retT;
            }

            LiteralScriptValue ret = new LiteralScriptValue(bypassCompatibilityOnLazyInvokation);
            if (typeArgs != null && typeArgs.Length != 0)
            {
                retVal = retVal.MakeGenericType((from t in typeArgs select (Type)t.GetValue(null, ScriptingPolicy)).ToArray());
                if (scriptingPolicy.IsDenied(retVal, TypeAccessMode.Direct, startPolicy))
                {
                    var retT = new Throw();
                    retT.Initialize($"Access to the type {retVal.FullName} was denied by policy.", false);
                    return retT;
                }
            }

            ret.Initialize(retVal);
            return ret;
        }

        /// <summary>
        /// Creates a copy of the initial scope of a script
        /// </summary>
        /// <returns>a dictionary that represents the root-variables of the current script or block</returns>
        public IDictionary<string, object> CopyInitial()
        {
            return variables.CopyInitial();
        }

#if UseVisitSingleExpression
        private ScriptValue VisitSingleExpression(ITVScriptingParser.SingleExpressionContext item)
        {
            if (item is ITVScriptingParser.ParenthesizedExpressionContext)
            {
                return VisitParenthesizedExpression((ITVScriptingParser.ParenthesizedExpressionContext) item);
            }

            if (item is ITVScriptingParser.InstanceIsNullExpressionContext)
            {
                return VisitInstanceIsNullExpression((ITVScriptingParser.InstanceIsNullExpressionContext) item);
            }

            if (item is ITVScriptingParser.TernaryExpressionContext)
            {
                return VisitTernaryExpression((ITVScriptingParser.TernaryExpressionContext) item);
            }

            if (item is ITVScriptingParser.LogicalAndExpressionContext)
            {
                return VisitLogicalAndExpression((ITVScriptingParser.LogicalAndExpressionContext) item);
            }

            if (item is ITVScriptingParser.LogicalOrExpressionContext)
            {
                return VisitLogicalOrExpression((ITVScriptingParser.LogicalOrExpressionContext) item);
            }

            if (item is ITVScriptingParser.BitAndExpressionContext)
            {
                return VisitBitAndExpression((ITVScriptingParser.BitAndExpressionContext) item);
            }

            if (item is ITVScriptingParser.BitOrExpressionContext)
            {
                return VisitBitOrExpression((ITVScriptingParser.BitOrExpressionContext) item);
            }

            if (item is ITVScriptingParser.BitXOrExpressionContext)
            {
                return VisitBitXOrExpression((ITVScriptingParser.BitXOrExpressionContext) item);
            }

            if (item is ITVScriptingParser.BitNotExpressionContext)
            {
                return VisitBitNotExpression((ITVScriptingParser.BitNotExpressionContext) item);
            }

            if (item is ITVScriptingParser.NotExpressionContext)
            {
                return VisitNotExpression((ITVScriptingParser.NotExpressionContext) item);
            }

            if (item is ITVScriptingParser.MultiplicativeExpressionContext)
            {
                return VisitMultiplicativeExpression((ITVScriptingParser.MultiplicativeExpressionContext) item);
            }

            if (item is ITVScriptingParser.AdditiveExpressionContext)
            {
                return VisitAdditiveExpression((ITVScriptingParser.AdditiveExpressionContext) item);
            }
            if (item is ITVScriptingParser.UnaryMinusExpressionContext)
            {
                return VisitUnaryMinusExpression((ITVScriptingParser.UnaryMinusExpressionContext) item);
            }

            if (item is ITVScriptingParser.UnaryPlusExpressionContext)
            {
                return VisitUnaryPlusExpression((ITVScriptingParser.UnaryPlusExpressionContext) item);
            }

            if (item is ITVScriptingParser.PreIncrementExpressionContext)
            {
                return VisitPreIncrementExpression((ITVScriptingParser.PreIncrementExpressionContext) item);
            }

            if (item is ITVScriptingParser.PreDecreaseExpressionContext)
            {
                return VisitPreDecreaseExpression((ITVScriptingParser.PreDecreaseExpressionContext) item);
            }

            if (item is ITVScriptingParser.PostIncrementExpressionContext)
            {
                return VisitPostIncrementExpression((ITVScriptingParser.PostIncrementExpressionContext) item);
            }

            if (item is ITVScriptingParser.PostDecreaseExpressionContext)
            {
                return VisitPostDecreaseExpression((ITVScriptingParser.PostDecreaseExpressionContext) item);
            }

            if (item is ITVScriptingParser.BitShiftExpressionContext)
            {
                return VisitBitShiftExpression((ITVScriptingParser.BitShiftExpressionContext) item);
            }

            if (item is ITVScriptingParser.ArgumentsExpressionContext)
            {
                return VisitArgumentsExpression((ITVScriptingParser.ArgumentsExpressionContext) item);
            }

            if (item is ITVScriptingParser.IdentifierExpressionContext)
            {
                return VisitIdentifierExpression((ITVScriptingParser.IdentifierExpressionContext) item);
            }

            if (item is ITVScriptingParser.MemberDotQExpressionContext)
            {
                return VisitMemberDotQExpression((ITVScriptingParser.MemberDotQExpressionContext) item);
            }

            if (item is ITVScriptingParser.MemberDotExpressionContext)
            {
                return VisitMemberDotExpression((ITVScriptingParser.MemberDotExpressionContext) item);
            }

            if (item is ITVScriptingParser.NewExpressionContext)
            {
                return VisitNewExpression((ITVScriptingParser.NewExpressionContext) item);
            }

            if (item is ITVScriptingParser.AssignmentExpressionContext)
            {
                return VisitAssignmentExpression((ITVScriptingParser.AssignmentExpressionContext) item);
            }

            if (item is ITVScriptingParser.AssignmentOperatorExpressionContext)
            {
                return VisitAssignmentOperatorExpression((ITVScriptingParser.AssignmentOperatorExpressionContext) item);
            }

            if (item is ITVScriptingParser.EqualityExpressionContext)
            {
                return VisitEqualityExpression((ITVScriptingParser.EqualityExpressionContext) item);
            }

            if (item is ITVScriptingParser.RelationalExpressionContext)
            {
                return VisitRelationalExpression((ITVScriptingParser.RelationalExpressionContext) item);
            }

            if (item is ITVScriptingParser.LiteralExpressionContext)
            {
                return VisitLiteralExpression((ITVScriptingParser.LiteralExpressionContext) item);
            }

            if (item is ITVScriptingParser.ArrayLiteralExpressionContext)
            {
                return VisitArrayLiteralExpression((ITVScriptingParser.ArrayLiteralExpressionContext) item);
            }

            if (item is ITVScriptingParser.MemberIndexExpressionContext)
            {
                return VisitMemberIndexExpression((ITVScriptingParser.MemberIndexExpressionContext) item);
            }

            return Visit(item);
        }

#endif

        protected override bool ShouldVisitNextChild(IRuleNode node, ScriptValue currentResult)
        {
            bool retVal = !(currentResult is IPassThroughValue);
            if (!retVal)
            {
                defaultRet = currentResult;
            }

            return retVal;
            //return base.ShouldVisitNextChild(node, currentResult);
        }

        /// <summary>
        /// Indicates whether the scriptvalue is a boolean that has the value true
        /// </summary>
        /// <param name="value">the scriptvalue to check</param>
        /// <returns>a boolean indicating whether the inner value is true</returns>
        private bool CheckBooleanTrue(ScriptValue value)
        {
            object obj = value.GetValue(null, ScriptingPolicy);
            return obj is bool && (bool)obj;
        }
    }
}
