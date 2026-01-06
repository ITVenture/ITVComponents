using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using Dynamitey.DynamicObjects;
using ITVComponents.AssemblyResolving;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.Literals;
using ITVComponents.Scripting.CScript.Core.Native;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Scripting.CScript.Interpreter.Model;
using ITVComponents.Scripting.CScript.Interpreter.Model.Arguments;
using ITVComponents.Scripting.CScript.Operating;
using ITVComponents.Scripting.CScript.ReflectionHelpers;
using ITVComponents.Scripting.CScript.ScriptValues;
using ITVComponents.Scripting.CScript.Security;
using ITVComponents.Scripting.CScript.Security.Restrictions;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ITVComponents.Scripting.CScript.Interpreter
{
    public class ExpressionExecutorBuilder: ITVScriptingBaseVisitor<ScriptExecutor>
    {
        private bool loopJumpAllowed = false;

        private bool catching = false;

        private bool hasUnconditionalJump = false;

        public bool conditional = false;

        //private Stack<object> switchStack = new Stack<object>();
        //private Stack<bool> loopJumpAllowed = new Stack<bool>();
        //private Stack<bool> returnSupported = new Stack<bool>();
        private bool returnSupported = true;

        

        //private ScriptValue defaultRet;
        private ScriptingPolicy scriptingPolicy;

        public ExpressionExecutorBuilder()
        {
            scriptingPolicy = Security.ScriptingPolicy.Default;
        }

        internal ScriptingPolicy ScriptingPolicy
        {
            get => scriptingPolicy;
            set => scriptingPolicy = value;
        }

        public override ScriptExecutor VisitProgram(ITVScriptingParser.ProgramContext context)
        {
            return VisitSourceElements(context.sourceElements());
        }

        public override ScriptExecutor VisitSourceElements(ITVScriptingParser.SourceElementsContext context)
        {
            var retVal = new ScriptExecutor(context) { StatusName = ScriptExecutionStatus.Block };
            retVal.SetStatusArguments(new BlockArguments { BlockType = BlockType.SourceElementList });
            ITVScriptingParser.StatementContext[] list = null;
            var statements = context.sourceElement();

            if (statements != null && statements.Length != 0)
            {
                foreach (var statement in statements)
                {
                    var value = Visit(statement);
                    value.ElementName = BlockArguments.SourceElement;
                    retVal.ChildExecutors.Add(value);
                }
            }

            return retVal;
        }
        private ScriptExecutor Block(ParserRuleContext entireBlock, ITVScriptingParser.StatementListContext statements, BlockType blockType = BlockType.StatementList)
        {
            var retVal = new ScriptExecutor(entireBlock) { StatusName = ScriptExecutionStatus.Block };
            retVal.SetStatusArguments(new BlockArguments{BlockType = blockType});
            ITVScriptingParser.StatementContext[] list = null;
            if (statements != null && (list = statements.statement()) != null && list.Length != 0)
            {
                foreach (ITVScriptingParser.StatementContext statement in statements.statement())
                {
                    var value = VisitStatement(statement);
                    value.ElementName = BlockArguments.Statement;
                    retVal.ChildExecutors.Add(value);
                }
            }

            return retVal;
        }

        public override ScriptExecutor VisitBlock(ITVScriptingParser.BlockContext context)
        {
            return Block(context, context.statementList(), BlockType.CodeBlock);
        }

        public override ScriptExecutor VisitStatementList(ITVScriptingParser.StatementListContext context)
        {
            return Block(context, context);
        }

        public override ScriptExecutor VisitEmptyStatement(ITVScriptingParser.EmptyStatementContext context)
        {
            return new ScriptExecutor(context) { StatusName = ScriptExecutionStatus.EmptyStatement };
        }

        public override ScriptExecutor VisitExpressionStatement(ITVScriptingParser.ExpressionStatementContext context)
        {
            var sequence = VisitExpressionSequence(context.expressionSequence());
            if (sequence.ChildExecutors.Count == 1)
            {
                var firstChild = sequence.ChildExecutors[0];
                firstChild.ReleaseFromParent();
                return firstChild;
            }

            return sequence;
        }

        public override ScriptExecutor VisitIfStatement(ITVScriptingParser.IfStatementContext context)
        {
            var val = Visit(context.singleExpression());
            ITVScriptingParser.StatementContext[] statements = context.statement();
            ScriptExecutor ifBlock = null;
            if (statements.Length > 1)
            {
                ifBlock = VisitStatement(statements[1]);
            }

            if (ifBlock == null || ifBlock.StatusName != ScriptExecutionStatus.IfBlock)
            {
                var tmp = ifBlock;
                ifBlock = new ScriptExecutor(context) { StatusName = ScriptExecutionStatus.IfBlock };
                var arg = new IfBlockArguments { };
                ifBlock.SetStatusArguments(arg);
                if (tmp != null)
                {
                    tmp.ElementName = IfBlockArguments.ElseBlock;
                    ifBlock.ChildExecutors.Add(tmp);
                }
            }

            var primaryIf = new ScriptExecutor(context) { StatusName = ScriptExecutionStatus.IfCondition };
            val.ElementName = IfBlockArguments.Condition;
            primaryIf.ChildExecutors.Add(val);
            var primaryBody = VisitStatement(statements[0]);
            primaryBody.ElementName = IfBlockArguments.Body;
            primaryIf.ChildExecutors.Add(primaryBody);
            primaryIf.ElementName = IfBlockArguments.Alternative;
            ifBlock.ChildExecutors.Insert(0, primaryIf);
            return ifBlock;
        }

        public override ScriptExecutor VisitDoStatement(ITVScriptingParser.DoStatementContext context)
        {
            ITVScriptingParser.StatementContext body = context.statement();
            ITVScriptingParser.SingleExpressionContext condition = context.singleExpression();
            return Loop(context, LoopType.DoWhile, null, condition, null, null, body);
        }

        public override ScriptExecutor VisitWhileStatement(ITVScriptingParser.WhileStatementContext context)
        {
            ITVScriptingParser.StatementContext body = context.statement();
            ITVScriptingParser.SingleExpressionContext condition = context.singleExpression();
            return Loop(context, LoopType.While, null, condition, null, null, body);
        }

        public override ScriptExecutor VisitForStatement(ITVScriptingParser.ForStatementContext context)
        {
            ITVScriptingParser.StatementContext body = context.statement();
            ITVScriptingParser.ExpressionSequenceContext[] header = context.expressionSequence();
            if (header.Length != 3)
            {
                throw new ScriptException($"Invalid For - Statement at {context.Start.Line}/{context.Start.Column}");
            }

            ITVScriptingParser.ExpressionSequenceContext start, condition, loopAction;
            start = header[0];
            condition = header[1];
            loopAction = header[2];
            return Loop(context, LoopType.For, start, condition, loopAction, null, body);
        }

        public override ScriptExecutor VisitForInStatement(ITVScriptingParser.ForInStatementContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] startExpressions = context.singleExpression();
            ITVScriptingParser.StatementContext body = context.statement();
            ITVScriptingParser.SingleExpressionContext runVar = startExpressions[0];
            ITVScriptingParser.SingleExpressionContext enumerableValue = startExpressions[1];
            return Loop(context, LoopType.ForEach, runVar, null, null, enumerableValue, body);
        }

        private ScriptExecutor Loop(ParserRuleContext entireLoopContext, LoopType type, ParserRuleContext initContextOrItem, ParserRuleContext conditionContext, ParserRuleContext oneToNEntryActionContext, ParserRuleContext iteratorContext, ParserRuleContext bodyContext)
        {
            var retVal = new ScriptExecutor(entireLoopContext) { StatusName = ScriptExecutionStatus.Loop };
            var arg = new LoopArguments { Type = type };
            var lja = loopJumpAllowed;
            var cnd = conditional;
            if (type == LoopType.For)
            {
                var init = Visit(initContextOrItem);
                init.ElementName = LoopArguments.Initializer;
                retVal.ChildExecutors.Add(init);
                var oneToN = Visit(oneToNEntryActionContext);
                oneToN.ElementName = LoopArguments.Iterator;
                retVal.ChildExecutors.Add(oneToN);
            }

            if (type == LoopType.For || type == LoopType.While || type == LoopType.DoWhile)
            {
                var condition = Visit(conditionContext);
                condition.ElementName = LoopArguments.Condition;
                retVal.ChildExecutors.Add(condition);
            }

            if (type == LoopType.ForEach)
            {
                var item = Visit(initContextOrItem);
                item.ElementName = LoopArguments.ItemVariable;
                retVal.ChildExecutors.Add(item);
                var iterator = Visit(iteratorContext);
                iterator.ElementName = LoopArguments.Iterator;
                retVal.ChildExecutors.Add(iterator);
            }

            ScriptExecutor body;
            try
            {
                loopJumpAllowed = true;
                conditional = true;
                body= Visit(bodyContext);
            }
            finally
            {
                loopJumpAllowed = lja;
                conditional = cnd;
            }

            body.ElementName = LoopArguments.Body;
            retVal.ChildExecutors.Add(body);
            retVal.SetStatusArguments(arg);
            return retVal;
        }

        public override ScriptExecutor VisitContinueStatement(ITVScriptingParser.ContinueStatementContext context)
        {
            if (loopJumpAllowed)
            {
                if (!conditional)
                {
                    hasUnconditionalJump = true;
                }

                return LoopJump(context, LoopJumpType.Continue);
            }

            throw new ScriptException(
                $"Invalid usage of Continue found at {context.Start.Line}/{context.Start.Column}");
        }

        public override ScriptExecutor VisitBreakStatement(ITVScriptingParser.BreakStatementContext context)
        {
            if (loopJumpAllowed)
            {
                if (!conditional)
                {
                    hasUnconditionalJump = true;
                }

                return LoopJump(context,LoopJumpType.Break);
            }

            throw new ScriptException($"Invalid usage of Break found at {context.Start.Line}/{context.Start.Column}");
        }

        private ScriptExecutor LoopJump(ParserRuleContext context, LoopJumpType type)
        {
            var retVal = new ScriptExecutor(context) { StatusName = ScriptExecutionStatus.LoopJump };
            retVal.SetStatusArguments(new LoopJumpArguments { Type = type });
            return retVal;
        }

        public override ScriptExecutor VisitReturnStatement(ITVScriptingParser.ReturnStatementContext context)
        {
            if (returnSupported)
            {
                ScriptExecutor val = null;
                var rv = context.singleExpression();
                if (rv != null)
                {
                    val = Visit(rv);
                }
                if (!conditional)
                {
                    hasUnconditionalJump = true;
                }

                var retVal = new ScriptExecutor(context) { StatusName = ScriptExecutionStatus.ReturnStatement };
                retVal.SetStatusArguments(new ReturnArguments());
                if (val != null)
                {
                    val.ElementName = ReturnArguments.Value;
                    retVal.ChildExecutors.Add(val);
                }

                return retVal;
            }

            throw new ScriptException($"Invalid usage of Return found at {context.Start.Line}/{context.Start.Column}");
        }

        public override ScriptExecutor VisitSwitchStatement(ITVScriptingParser.SwitchStatementContext context)
        {
            ScriptExecutor caseValue = Visit(context.singleExpression());
            var arg = new SwitchArguments();
            var retVal = new ScriptExecutor(context) { StatusName = ScriptExecutionStatus.Switch };
            caseValue.ElementName = SwitchArguments.SwitchValue;
            retVal.ChildExecutors.Add(caseValue);
            retVal.SetStatusArguments(arg);
            VisitCaseBlock(context.caseBlock(), retVal);
            return retVal;
        }

        public void VisitCaseBlock(ITVScriptingParser.CaseBlockContext context, ScriptExecutor rootSwitch)
        {
            ITVScriptingParser.CaseClausesContext cases = context.caseClauses();
            ITVScriptingParser.DefaultClauseContext defaultClause = context.defaultClause();
            VisitCaseClauses(cases, defaultClause, rootSwitch);
        }

        public void VisitCaseClauses(ITVScriptingParser.CaseClausesContext context, ITVScriptingParser.DefaultClauseContext defaultClause, ScriptExecutor rootCase)
        {
            ITVScriptingParser.CaseClauseContext[] allCases = context.caseClause();
            bool ok = false;
            foreach (ITVScriptingParser.CaseClauseContext singleCase in allCases)
            {
                var ret = VisitCaseClause(singleCase);
                ret.ElementName = SwitchArguments.Case;
                rootCase.ChildExecutors.Add(ret);
            }

            if (defaultClause != null)
            {
                var ret = VisitDefaultClause(defaultClause);
                ret.ElementName = SwitchArguments.Case;
                rootCase.ChildExecutors.Add(ret);
            }
        }

        public ScriptExecutor VisitCaseClause(ITVScriptingParser.CaseClauseContext context)
        {
            ITVScriptingParser.SingleExpressionContext expression = context.singleExpression();
            ITVScriptingParser.StatementListContext statements = context.statementList();
            ScriptExecutor val = Visit(expression);
            var retVal = new ScriptExecutor(context) { StatusName = ScriptExecutionStatus.SwitchCase };
            var arg = new SwitchCaseArguments { Type = CaseType.Standard };

            retVal.SetStatusArguments(arg);
            val.ElementName = SwitchCaseArguments.Label;
            retVal.ChildExecutors.Add(val);
            ScriptExecutor list;
            var lja = loopJumpAllowed;
            var cnd = conditional;
            try
            {
                loopJumpAllowed = true;
                hasUnconditionalJump = false;
                conditional = false;
                list = VisitStatementList(statements);
                if (!hasUnconditionalJump)
                {
                    throw new ScriptException(
                        $"Falling through case-labels at {context.Start.Line}/{context.Start.Column}");
                }
            }
            finally
            {
                loopJumpAllowed = lja;
                conditional = cnd;
            }

            list.ElementName = SwitchCaseArguments.Statements;
            retVal.ChildExecutors.Add(list);
            return retVal;
        }

        public new ScriptExecutor VisitDefaultClause(ITVScriptingParser.DefaultClauseContext context)
        {
            var retVal = new ScriptExecutor(context) { StatusName = ScriptExecutionStatus.SwitchCase };
            retVal.SetStatusArguments(new SwitchCaseArguments{Type = CaseType.DefaultLabel});
            ScriptExecutor list;
            var lja = loopJumpAllowed;
            var cnd = conditional;
            try
            {
                loopJumpAllowed = true;
                hasUnconditionalJump = false;
                conditional = false;
                list = VisitStatementList(context.statementList());
                if (!hasUnconditionalJump)
                {
                    throw new ScriptException(
                        $"Falling through case-labels at {context.Start.Line}/{context.Start.Column}");
                }
            }
            finally
            {
                loopJumpAllowed = lja;
                conditional = cnd;
            }

            list.ElementName = SwitchCaseArguments.Statements;
            retVal.ChildExecutors.Add(list);
            return retVal;
        }

        public override ScriptExecutor VisitThrowStatement(ITVScriptingParser.ThrowStatementContext context)
        {
            ITVScriptingParser.SingleExpressionContext exception = context.singleExpression();
            var retVal = new ScriptExecutor(context) { StatusName = ScriptExecutionStatus.ThrowStatement };
            var arg = new ThrowArguments();
            retVal.SetStatusArguments(arg);
            if (exception != null)
            {
                var ex = Visit(exception);
                ex.ElementName = ThrowArguments.Exception;
                retVal.ChildExecutors.Add(ex);
                arg.ThrowMode = ThrowMode.ThrowException;
            }
            else if (!catching)
            {
                throw new ScriptException($"Illegal Re-Throw statement found at {context.Start.Line}/{context.Start.Column}!");
            }

            return retVal;
        }

        public override ScriptExecutor VisitTryStatement(ITVScriptingParser.TryStatementContext context)
        {
            ITVScriptingParser.BlockContext block = context.block();
            ITVScriptingParser.CatchProductionContext catchBlock = context.catchProduction();
            ITVScriptingParser.FinallyProductionContext finallyBlock = context.finallyProduction();
            string name = null;
            if (catchBlock != null)
            {
                name = catchBlock.Identifier().GetText();
            }

            ScriptExecutor tryBlockExecutor = Block(block, block.statementList(), BlockType.TryBlock);
            ScriptExecutor catchBlockExecutor = null;
            ScriptExecutor finallyBlockExecutor = null;
            bool hasFollowUp = false;
            var retVal = new ScriptExecutor(context) { StatusName = ScriptExecutionStatus.TryStatement };
            var arguments = new TryArguments();
            retVal.SetStatusArguments(arguments);

            tryBlockExecutor.ElementName = TryArguments.Try;
            retVal.ChildExecutors.Add(tryBlockExecutor);
            if (catchBlock != null)
            {
                var ct = catching;
                try
                {
                    catching = true;
                    catchBlockExecutor = VisitCatchProduction(catchBlock);
                }
                finally
                {
                    catching = ct;
                }

                hasFollowUp = true;
                catchBlockExecutor.ElementName = TryArguments.Catch;
                retVal.ChildExecutors.Add(catchBlockExecutor);
                arguments.CatchVariable = name;
                arguments.HasCatch = true;
            }

            if (finallyBlock != null)
            {
                var lja = loopJumpAllowed;
                var rs = returnSupported;
                try
                {
                    loopJumpAllowed = false;
                    returnSupported = false;
                    finallyBlockExecutor = VisitFinallyProduction(finallyBlock);
                    hasFollowUp = true;
                    finallyBlockExecutor.ElementName = TryArguments.Finally;
                    retVal.ChildExecutors.Add(finallyBlockExecutor);
                    arguments.HasFinally = true;
                }
                finally
                {
                    loopJumpAllowed = lja;
                    returnSupported = rs;
                }
            }

            if (!hasFollowUp)
            {
                throw new ScriptException(
                    $"Incomplete Try-Statement detected at {context.Start.Line}/{context.Start.Column}.");
            }

            return retVal;
        }

        public override ScriptExecutor VisitCatchProduction(ITVScriptingParser.CatchProductionContext context)
        {
            return Block(context, context.block()?.statementList(), BlockType.CatchBlock);
        }

        public override ScriptExecutor VisitFinallyProduction(ITVScriptingParser.FinallyProductionContext context)
        {
            return Block(context, context.block()?.statementList(), BlockType.FinallyBlock);
        }

        public override ScriptExecutor VisitArrayLiteral(ITVScriptingParser.ArrayLiteralContext context)
        {
            var list = context.elementList();
            if (list != null)
            {
                ScriptExecutor value = VisitElementList(list);
                var arg = value.GetStatusArguments<SequenceArguments>();
                arg.SequenceType = SequenceType.Array;
                arg.VoidWhenEmpty = false;
                return value;
            }

            var retVal = new ScriptExecutor(context) { StatusName = ScriptExecutionStatus.ExpressionSequence };
            retVal.SetStatusArguments(
                new SequenceArguments { SequenceType = SequenceType.Array, VoidWhenEmpty = false });
            return retVal;
        }

        public override ScriptExecutor VisitElementList(ITVScriptingParser.ElementListContext context)
        {
            var retVal = new ScriptExecutor(context) { StatusName = ScriptExecutionStatus.ExpressionSequence };
            retVal.SetStatusArguments(new SequenceArguments());
            foreach (ITVScriptingParser.SingleExpressionContext se in context.singleExpression())
            {
                var tmp = Visit(se);
                tmp.ElementName = SequenceArguments.SequenceItem;
                retVal.ChildExecutors.Add(tmp);
            }

            return retVal;
        }

        public override ScriptExecutor VisitArguments(ITVScriptingParser.ArgumentsContext context)
        {
            return ArgumentList(context, context.argumentList());
        }

        public override ScriptExecutor VisitFinalGenerics(ITVScriptingParser.FinalGenericsContext context)
        {
            return VisitTypedArguments(context.typedArguments());
        }

        public override ScriptExecutor VisitTypedArguments(ITVScriptingParser.TypedArgumentsContext context)
        {
            //List<ScriptValue> elements = new List<ScriptValue>();
            ITVScriptingParser.TypeIdentifierContext[] types = context.typeIdentifier();
            var retVal = new ScriptExecutor(context) { StatusName = ScriptExecutionStatus.ExpressionSequence };
            retVal.SetStatusArguments(new SequenceArguments());
            foreach (ITVScriptingParser.TypeIdentifierContext se in types)
            {
                var tmp = Visit(se);
                tmp.ElementName = SequenceArguments.SequenceItem;
                retVal.ChildExecutors.Add(tmp);
            }

            return retVal;
        }

        #region Overrides of ITVScriptingBaseVisitor<ScriptValue>

        public override ScriptExecutor VisitExplicitTypeHint(ITVScriptingParser.ExplicitTypeHintContext context)
        {
            return VisitTypeIdentifier(context.typeIdentifier());
        }

        #endregion

        public override ScriptExecutor VisitTypeIdentifier(ITVScriptingParser.TypeIdentifierContext context)
        {
            var retVal = new ScriptExecutor(context){StatusName = ScriptExecutionStatus.MemberAccess};
            var param = new MemberAccessArguments();
            //VariableAccessValue retVal = new VariableAccessValue(bypassCompatibilityOnLazyInvokation);
            var path = context.Identifier();
            for (int i = 0; i < path.Length; i++)
            {
                var node = path[i];
                var targetName = node.GetText();
                param.MemberPath.Add(targetName);
            }

            return retVal;
        }

        public override ScriptExecutor VisitArgumentList(ITVScriptingParser.ArgumentListContext context)
        {
            return ArgumentList(context, context);
        }

        private ScriptExecutor ArgumentList(ParserRuleContext entireExpression,
            ITVScriptingParser.ArgumentListContext list)
        {
            var retVal = new ScriptExecutor(entireExpression) { StatusName = ScriptExecutionStatus.ExpressionSequence };
            if (list != null)
            {
                ITVScriptingParser.SingleExpressionContext[] sequence = list.singleExpression();
                foreach (var item in sequence)
                {
                    var tmp = Visit(item);
                    tmp.ElementName = SequenceArguments.SequenceItem;
                    retVal.ChildExecutors.Add(tmp);
                }
            }

            retVal.SetStatusArguments(new SequenceArguments { VoidWhenEmpty = false });

            return retVal;
        }

        public override ScriptExecutor VisitExpressionSequence(ITVScriptingParser.ExpressionSequenceContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] sequence = context.singleExpression();
            var retVal = new ScriptExecutor(context) { StatusName = ScriptExecutionStatus.ExpressionSequence };
            foreach (var item in sequence)
            {
                var tmp = Visit(item);
                tmp.ElementName = SequenceArguments.SequenceItem;
                retVal.ChildExecutors.Add(tmp);
            }

            retVal.SetStatusArguments(new SequenceArguments { VoidWhenEmpty = true });
            return retVal;
        }

        public override ScriptExecutor VisitTernaryExpression(ITVScriptingParser.TernaryExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] values = context.singleExpression();
            var condition = Visit(values[0]);
            var first = Visit(values[1]);
            var second = Visit(values[2]);
            var retVal = new ScriptExecutor(context) { StatusName = ScriptExecutionStatus.ConditionalValue };
            condition.ElementName = ConditionalValueArguments.Condition;
            first.ElementName = ConditionalValueArguments.FirstValue;
            second.ElementName = ConditionalValueArguments.AlternativeValue;
            retVal.ChildExecutors.Add(condition);
            retVal.ChildExecutors.Add(first);
            retVal.ChildExecutors.Add(second);
            retVal.SetStatusArguments(new ConditionalValueArguments
            {
                Type = ConditionalValueType.Ternary
            });

            return retVal;
        }

        public override ScriptExecutor VisitLogicalAndExpression(ITVScriptingParser.LogicalAndExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] expressions = context.singleExpression();
            var v1 = Visit(expressions[0]);
            var v2 = Visit(expressions[1]);
            return OperationExecutor(context, v1, v2, BaseOperations.AndAlso);
        }

        public override ScriptExecutor VisitPreIncrementExpression(ITVScriptingParser.PreIncrementExpressionContext context)
        {
            var val = Visit(context.singleExpression());
            return IncrementExecutor(context, val, IncrementType.PreIncrement);
        }

        public override ScriptExecutor VisitLogicalOrExpression(ITVScriptingParser.LogicalOrExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] expressions = context.singleExpression();
            var v1 = Visit(expressions[0]);
            var v2 = Visit(expressions[1]);
            return OperationExecutor(context, v1, v2, BaseOperations.OrElse);
        }

        public override ScriptExecutor VisitNotExpression(ITVScriptingParser.NotExpressionContext context)
        {
            var val = Visit(context.singleExpression());
            return UnaryOp(context, val, UnaryOperator.Not);
        }

        public override ScriptExecutor VisitPreDecreaseExpression(ITVScriptingParser.PreDecreaseExpressionContext context)
        {
            var val = Visit(context.singleExpression());
            return IncrementExecutor(context, val, IncrementType.PreDecrement);
        }

        public override ScriptExecutor VisitArgumentsExpression(ITVScriptingParser.ArgumentsExpressionContext context)
        {
            ScriptExecutor baseValue = Visit(context.singleExpression());
            ScriptExecutor arguments = VisitArguments(context.arguments());
            ScriptExecutor typeArguments = null;
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
                    throw new ScriptException(
                        $"Open Generic Arguments are not supported in Methodcalls! at {context.Start.Line}/{context.Start.Column}");
                }
            }

            ScriptExecutor explicitTyping = null;
            ITVScriptingParser.ExplicitTypeHintContext ext = context.explicitTypeHint();
            if (ext != null)
            {
                explicitTyping = VisitExplicitTypeHint(ext);
            }

            return UpdateMemberExpression(context, baseValue, explicitTyping, arguments, typeArguments);
        }

        public override ScriptExecutor VisitUnaryMinusExpression(ITVScriptingParser.UnaryMinusExpressionContext context)
        {
            var val = Visit(context.singleExpression());
            return UnaryOp(context, val, UnaryOperator.Minus);
        }

        public override ScriptExecutor VisitMemberDotQExpression(ITVScriptingParser.MemberDotQExpressionContext context)
        {
            var baseVal = Visit(context.singleExpression());
            ScriptExecutor explicitType = null;
            var eth = context.explicitTypeHint();
            if (eth != null)
            {
                explicitType = VisitExplicitTypeHint(eth);
            }

            var name = context.identifierName().GetText();
            return MemberExpression(context, baseVal, explicitType, null, null, name, false, true);
        }

        public override ScriptExecutor VisitPostDecreaseExpression(ITVScriptingParser.PostDecreaseExpressionContext context)
        {
            var val = Visit(context.singleExpression());
            return IncrementExecutor(context, val, IncrementType.PostDecrement);
        }

        public override ScriptExecutor VisitAssignmentExpression(ITVScriptingParser.AssignmentExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            var target = Visit(subExpressions[0]);
            var value = Visit(subExpressions[1]);
            var retVal = new ScriptExecutor(context) { StatusName = ScriptExecutionStatus.Assignment };
            target.ElementName = AssignArguments.Target;
            value.ElementName = AssignArguments.Source;
            retVal.ChildExecutors.Add(target);
            retVal.ChildExecutors.Add(value);
            retVal.SetStatusArguments(new AssignArguments());
            return retVal;
        }

        public override ScriptExecutor VisitUnaryPlusExpression(ITVScriptingParser.UnaryPlusExpressionContext context)
        {
            var v1 = Visit(context.singleExpression());
            return UnaryOp(context, v1, UnaryOperator.Plus);
        }

        private ScriptExecutor UnaryOp(ParserRuleContext entireExpression, ScriptExecutor baseValue, UnaryOperator op)
        {
            var retVal = new ScriptExecutor(entireExpression) { StatusName = ScriptExecutionStatus.UnaryOperation };
            retVal.SetStatusArguments(new UnaryOpArguments{Operator = op});
            baseValue.ElementName = UnaryOpArguments.BaseValue;
            retVal.ChildExecutors.Add(baseValue);
            return retVal;
        }

        public override ScriptExecutor VisitEqualityExpression(ITVScriptingParser.EqualityExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] expressions = context.singleExpression();
            var leftVal = Visit(expressions[0]);
            var rightVal = Visit(expressions[1]);
            string s = context.GetChild(1).GetText();
            if (s == "==")
            {
                return CompareExecutor(context, leftVal, rightVal, ComparisonType.Equal);
            }

            return CompareExecutor(context, leftVal, rightVal, ComparisonType.NotEqual);
        }

        public override ScriptExecutor VisitBitXOrExpression(ITVScriptingParser.BitXOrExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            var leftVal = Visit(subExpressions[0]);
            var rightVal = Visit(subExpressions[1]);
            return OperationExecutor(context, leftVal, rightVal, BaseOperations.Xor);
        }

        public override ScriptExecutor VisitMultiplicativeExpression(ITVScriptingParser.MultiplicativeExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            var leftVal = Visit(subExpressions[0]);
            var rightVal = Visit(subExpressions[1]);
            string op = context.GetChild(1).GetText();
            switch (op)
            {
                case "*":
                {
                    return OperationExecutor(context, leftVal, rightVal, BaseOperations.Multiply);
                }
                case "/":
                {
                    return OperationExecutor(context, leftVal, rightVal, BaseOperations.Divide);
                }
                case "%":
                {
                    return OperationExecutor(context, leftVal, rightVal, BaseOperations.Modulus);
                }
                default:
                {
                    throw new ScriptException(
                        $"Unable to perform shift operation at {context.Start.Line}/{context.Start.Column}");
                }
            }
        }

        public override ScriptExecutor VisitBitShiftExpression(ITVScriptingParser.BitShiftExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            var leftVal = Visit(subExpressions[0]);
            var rightVal = Visit(subExpressions[1]);
            string op = context.GetChild(1).GetText();
            switch (op)
            {
                case "<<":
                {
                    return OperationExecutor(context, leftVal, rightVal, BaseOperations.LeftShift);
                }
                case ">>":
                {
                    return OperationExecutor(context, leftVal, rightVal, BaseOperations.RightShift);
                }
                default:
                {
                    throw new ScriptException(
                        $"Unable to perform shift operation at {context.Start.Line}/{context.Start.Column}");
                }
            }
        }

        public override ScriptExecutor VisitParenthesizedExpression(ITVScriptingParser.ParenthesizedExpressionContext context)
        {
            return Visit(context.singleExpression());
        }

        public override ScriptExecutor VisitAdditiveExpression(ITVScriptingParser.AdditiveExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            var leftVal = Visit(subExpressions[0]);
            var rightVal = Visit(subExpressions[1]);
            string op = context.GetChild(1).GetText();
            switch (op)
            {
                case "+":
                {
                    return OperationExecutor(context, leftVal, rightVal, BaseOperations.Add);
                }
                case "-":
                {
                    return OperationExecutor(context, leftVal, rightVal, BaseOperations.Subtract);
                }
                default:
                {
                    throw new ScriptException(
                        $"Unable to perform additive operation at {context.Start.Line}/{context.Start.Column}");
                }
            }
        }

        public override ScriptExecutor VisitRelationalExpression(ITVScriptingParser.RelationalExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            var leftVal = Visit(subExpressions[0]);
            var rightVal = Visit(subExpressions[1]);
            string op = context.GetChild(1).GetText();
            switch (op)
            {
                case ">":
                {
                    return CompareExecutor(context, leftVal, rightVal, ComparisonType.GreaterThan);
                }
                case ">=":
                {
                    return CompareExecutor(context, leftVal, rightVal, ComparisonType.GreaterThanOrEqual);
                    }
                case "<":
                {
                    return CompareExecutor(context, leftVal, rightVal, ComparisonType.LessThan);
                    }
                case "<=":
                {
                    return CompareExecutor(context, leftVal, rightVal, ComparisonType.LessThanOrEqual);
                    }
                default:
                {
                    throw new ScriptException(
                        $"Unable to perform compare operation at {context.Start.Line}/{context.Start.Column}");
                }
            }
        }

        public override ScriptExecutor VisitPostIncrementExpression(ITVScriptingParser.PostIncrementExpressionContext context)
        {
            ScriptExecutor val = Visit(context.singleExpression());
            return IncrementExecutor(context, val, IncrementType.PostIncrement);
        }

        private ScriptExecutor IncrementExecutor(ParserRuleContext entireExpression, ScriptExecutor value, IncrementType type)
        {
            ScriptExecutor retVal = new ScriptExecutor(entireExpression) { StatusName = ScriptExecutionStatus.Increment };
            value.ElementName = IncrementArguments.BaseValue;
            retVal.ChildExecutors.Add(value);
            retVal.SetStatusArguments(new IncrementArguments { IncType = type });
            return retVal;
        }

        public override ScriptExecutor VisitBitNotExpression(ITVScriptingParser.BitNotExpressionContext context)
        {
            var subExpression = Visit(context.singleExpression());
            var retVal = new ScriptExecutor(context){StatusName = ScriptExecutionStatus.Negate};
            subExpression.ElementName = NegateArguments.BaseValue;
            retVal.ChildExecutors.Add(subExpression);
            retVal.SetStatusArguments(new NegateArguments());
            return retVal;
        }

        public override ScriptExecutor VisitNewImplicitInit(ITVScriptingParser.NewImplicitInitContext context)
        {
            ITVScriptingParser.SingleExpressionContext subExpression = context.singleExpression();
            ScriptExecutor val = Visit(subExpression);
            ScriptExecutor typeArguments = null;
            ITVScriptingParser.TypeArgumentsContext targs = context.typeArguments();
            var retVal = new ScriptExecutor(context)
                { StatusName = ScriptExecutionStatus.New };
            val.ElementName = NewArguments.Type;
            retVal.ChildExecutors.Add(val);
            retVal.SetStatusArguments(new NewArguments{UseDefaultConstructor=true});
            if (targs != null)
            {
                var genericsContext = targs as ITVScriptingParser.FinalGenericsContext;
                if (genericsContext != null)
                {
                    typeArguments = VisitFinalGenerics(genericsContext);
                    typeArguments.ElementName = NewArguments.GenericArguments;
                    retVal.ChildExecutors.Add(typeArguments);
                }
                else
                {
                    throw new ScriptException(
                        $"Open Generic Arguments are not supported in final Construction calls! at {context.Start.Line}/{context.Start.Column}");
                }
            }

            var arguments = Visit(context.objectLiteral());
            arguments.ElementName = NewArguments.InitialValues;
            retVal.ChildExecutors.Add(arguments);
            return arguments;
        }

        public override ScriptExecutor VisitNewExpression(ITVScriptingParser.NewExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext subExpression = context.singleExpression();
            var val = Visit(subExpression);
            var arguments = VisitArguments(context.arguments());
            var retVal = new ScriptExecutor(context)
                { StatusName = ScriptExecutionStatus.New };
            val.ElementName = NewArguments.Type;
            arguments.ElementName = NewArguments.ConstructorArguments;
            retVal.ChildExecutors.Add(val);
            retVal.ChildExecutors.Add(arguments);
            retVal.SetStatusArguments(new NewArguments());
            ScriptExecutor typeArguments = null;
            ITVScriptingParser.TypeArgumentsContext targs = context.typeArguments();
            if (targs != null)
            {
                var genericsContext = targs as ITVScriptingParser.FinalGenericsContext;
                if (genericsContext != null)
                {
                    typeArguments = VisitFinalGenerics(genericsContext);
                    typeArguments.ElementName = NewArguments.GenericArguments;
                    retVal.ChildExecutors.Add(typeArguments);
                }
                else
                {
                    throw new ScriptException(
                        $"Open Generic Arguments are not supported in final Construction calls! at {context.Start.Line}/{context.Start.Column}");
                }
            }


            var literalExt = context.objectLiteral();
            if (literalExt != null)
            {
                var lit = Visit(literalExt);
                lit.ElementName = NewArguments.InitialValues;
                retVal.ChildExecutors.Add(lit);
            }

            return retVal;
        }

        public override ScriptExecutor VisitLiteralExpression(ITVScriptingParser.LiteralExpressionContext context)
        {
            ITVScriptingParser.LiteralContext literal = context.literal();
            return VisitLiteral(literal);
        }

        public override ScriptExecutor VisitArrayLiteralExpression(ITVScriptingParser.ArrayLiteralExpressionContext context)
        {
            return VisitArrayLiteral(context.arrayLiteral());
        }

        private ScriptExecutor MemberExpression(ParserRuleContext entireExpression, ScriptExecutor baseValue, ScriptExecutor explicitTyping, ScriptExecutor arguments, ScriptExecutor typeArguments, string identifier, bool indicator = false, bool nullPropagation = false)
        {
            ScriptExecutor retVal = baseValue;
            MemberAccessArguments memberArgs;
            var setBase = false;
            if (baseValue == null || baseValue.StatusName != ScriptExecutionStatus.MemberAccess ||
                (memberArgs = baseValue.GetStatusArguments<MemberAccessArguments>()).Indicator != indicator ||
                memberArgs.NullPropageted != nullPropagation || baseValue.GetElements(MemberAccessArguments.ExplicitType).Length != 0)
            {
                retVal = new ScriptExecutor(entireExpression) { StatusName = ScriptExecutionStatus.MemberAccess };
                memberArgs = new MemberAccessArguments
                {
                    ExpectedMemberType = MemberAccessType.PropertyOrFieldOrEvent,
                    Indicator = indicator,
                    NullPropageted = nullPropagation
                };
                retVal.SetStatusArguments(memberArgs);
                setBase = true;
            }

            memberArgs.MemberPath.Add(identifier);
            if (baseValue != null && setBase)
            {
                baseValue.ElementName = MemberAccessArguments.BaseValue;
                retVal.ChildExecutors.Add(baseValue);
            }

            retVal = UpdateMemberExpression(entireExpression, retVal, explicitTyping, arguments, typeArguments);

            return retVal;
        }

        private ScriptExecutor UpdateMemberExpression(ParserRuleContext entireExpression, ScriptExecutor memberExpression, ScriptExecutor explicitTyping, ScriptExecutor arguments,
            ScriptExecutor typeArguments)
        {
            var retVal = memberExpression;
            MemberAccessArguments arg;
            if (memberExpression.StatusName != ScriptExecutionStatus.MemberAccess)
            {
                retVal = new ScriptExecutor(entireExpression) { StatusName = ScriptExecutionStatus.MemberAccess };
                memberExpression.ElementName = MemberAccessArguments.DirectMethod;
                retVal.ChildExecutors.Add(memberExpression);
                retVal.SetStatusArguments(arg=new MemberAccessArguments
                    { ExpectedMemberType = MemberAccessType.Method });
            }
            else
            {
                retVal.UpdateRuleContext(entireExpression);
                arg = retVal.GetStatusArguments<MemberAccessArguments>();
            }

            if (arguments != null)
            {
                arguments.ElementName = MemberAccessArguments.MethodArguments;
                retVal.ChildExecutors.Add(arguments);
                arg.ExpectedMemberType = MemberAccessType.Method;
            }

            if (typeArguments != null)
            {
                typeArguments.ElementName = MemberAccessArguments.GenericArguments;
                retVal.ChildExecutors.Add(typeArguments);
            }

            if (explicitTyping != null)
            {
                if (retVal.GetElements(MemberAccessArguments.ExplicitType).Length != 0)
                {
                    throw new ScriptException(
                        $"Ambigious Typing expression found at {entireExpression.Start.Line}/{entireExpression.Start.Column}");
                }

                explicitTyping.ElementName = MemberAccessArguments.ExplicitType;
                retVal.ChildExecutors.Add(explicitTyping);
            }

            return retVal;
        }

        public override ScriptExecutor VisitHasMemberExpression(ITVScriptingParser.HasMemberExpressionContext context)
        {
            var sample = Visit(context.singleExpression());
            string name = context.identifierName().GetText();
            ScriptExecutor explicitTyping = null;
            ScriptExecutor arguments = null;
            ScriptExecutor typeArguments = null;
            ITVScriptingParser.ExplicitTypeHintContext ext = context.explicitTypeHint();
            if (ext != null)
            {
                explicitTyping = VisitExplicitTypeHint(ext);
            }

            if (context.arguments() != null)
            {
                arguments = VisitArguments(context.arguments());
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
                        throw new ScriptException(
                            $"Open Generic Arguments are not supported in Methodcalls! at {context.Start.Line}/{context.Start.Column}");
                    }
                }
            }

            return MemberExpression(context, sample, explicitTyping, arguments, typeArguments, name, true);
        }

        public override ScriptExecutor VisitMemberIsExpression(ITVScriptingParser.MemberIsExpressionContext context)
        {
            var sample = Visit(context.singleExpression(0));
            var typex = Visit(context.singleExpression(1));
            var retVal = new ScriptExecutor(context)
                { StatusName = ScriptExecutionStatus.ValueIsType };
            sample.ElementName = ValueIsTypeArguments.Value;
            typex.ElementName = ValueIsTypeArguments.Type;
            retVal.ChildExecutors.Add(sample);
            retVal.ChildExecutors.Add(typex);
            retVal.SetStatusArguments(new ValueIsTypeArguments());
            return retVal;
        }

        public override ScriptExecutor VisitMemberDotExpression(ITVScriptingParser.MemberDotExpressionContext context)
        {
            var val = Visit(context.singleExpression());
            ScriptExecutor explicitType = null;
            var eth = context.explicitTypeHint();
            if (eth != null)
            {
                explicitType = VisitExplicitTypeHint(eth);
            }

            var name = context.identifierName().GetText();
            return MemberExpression(context, val, explicitType, null, null, name);
        }

        public override ScriptExecutor VisitMemberIndexExpression(ITVScriptingParser.MemberIndexExpressionContext context)
        {
            var baseValue = Visit(context.singleExpression());
            var indexArgs = VisitExpressionSequence(context.expressionSequence());

            ScriptExecutor retVal = new ScriptExecutor(context)
                { StatusName = ScriptExecutionStatus.Indexer };
            baseValue.ElementName = IndexerArguments.Source;
            indexArgs.ElementName = IndexerArguments.Index;
            retVal.ChildExecutors.Add(baseValue);
            retVal.ChildExecutors.Add(indexArgs);
            ScriptExecutor explicitType = null;
            var eth = context.explicitTypeHint();
            if (eth != null)
            {
                explicitType = VisitExplicitTypeHint(eth);
                explicitType.ElementName = IndexerArguments.ExplicitType;
                retVal.ChildExecutors.Add(explicitType);
            }

            retVal.SetStatusArguments(new IndexerArguments());
            /*IndexerScriptValue retVal = new IndexerScriptValue(lazyInvokation ? context : null, ScriptingPolicy, bypassCompatibilityOnLazyInvokation);
            retVal.Initialize(baseValue, ((SequenceValue)indexArgs).Sequence, explicitType);*/
            return retVal;
        }

        public override ScriptExecutor VisitInstanceIsNullExpression(ITVScriptingParser.InstanceIsNullExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            ScriptExecutor left = Visit(subExpressions[0]);
            ScriptExecutor right = Visit(subExpressions[1]);
            left.ElementName = ConditionalValueArguments.FirstValue;
            right.ElementName = ConditionalValueArguments.AlternativeValue;
            var retVal = new ScriptExecutor(context){StatusName = ScriptExecutionStatus.ConditionalValue};
            retVal.ChildExecutors.Add(left);
            retVal.ChildExecutors.Add(right);
            retVal.SetStatusArguments(new ConditionalValueArguments{Type= ConditionalValueType.IsNull});
            return retVal;
        }

        public override ScriptExecutor VisitIdentifierExpression(ITVScriptingParser.IdentifierExpressionContext context)
        {
            var name = context.Identifier().GetText();
            return MemberExpression(context, null, null, null, null, name);
            /*var retVal = new ScriptExecutor(context){StatusName = ScriptExecutionStatus.Identifier};
            retVal.SetStatusArguments(new IdentifierArguments{Name =  });
            return retVal;*/
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

        private ScriptExecutor OperationExecutor(ParserRuleContext entireExpression, ScriptExecutor left, ScriptExecutor right,
            BaseOperations operation)
        {
            var baseOp = new ScriptExecutor(entireExpression)
                { StatusName = ScriptExecutionStatus.Operation };
            left.ElementName = OperationArguments.LeftOperand;
            baseOp.ChildExecutors.Add(left);
            right.ElementName = OperationArguments.RightOperand;
            baseOp.ChildExecutors.Add(right);
            baseOp.SetStatusArguments(new OperationArguments { Operation = operation });
            return baseOp;
        }

        private ScriptExecutor CompareExecutor(ParserRuleContext entireExpression, ScriptExecutor leftVal,
            ScriptExecutor rightVal, ComparisonType type)
        {
            ScriptExecutor retVal = new ScriptExecutor(entireExpression) { StatusName = ScriptExecutionStatus.Compare };
            leftVal.ElementName = CompareArguments.Left;
            rightVal.ElementName = CompareArguments.Right;
            retVal.ChildExecutors.Add(leftVal);
            retVal.ChildExecutors.Add(rightVal);
            var arg = new CompareArguments();
            retVal.SetStatusArguments(arg);
            arg.ComparisonType = type;
            return retVal;
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

            var baseOp = new ScriptExecutor(context)
                { StatusName = ScriptExecutionStatus.Operation };
            left.ElementName = OperationArguments.LeftOperand;
            baseOp.ChildExecutors.Add(left);
            right.ElementName = OperationArguments.RightOperand;
            baseOp.ChildExecutors.Add(right);
            baseOp.SetStatusArguments(new OperationArguments { Operation = operation });
            left = Visit(subExpressions[0]);
            var assign = new ScriptExecutor(context)
                { StatusName = ScriptExecutionStatus.Assignment };
            left.ElementName = AssignArguments.Target;
            assign.ChildExecutors.Add(left);
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
            return new ScriptExecutor(context)
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
            return new ScriptExecutor(context)
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
            var retVal = new ScriptExecutor(context)
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
            var retVal = new ScriptExecutor(context) { StatusName = ScriptExecutionStatus.NativeLiteralExecute };
            retVal.SetStatusArguments(new NativeScriptArguments{Configuration=identifier, Text=text});
            parameterObj.ElementName = NativeScriptArguments.ExpressionArguments;
            retVal.ChildExecutors.Add(parameterObj);
            return retVal;
        }

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

            var retVal = new ScriptExecutor(context) { StatusName = ScriptExecutionStatus.Literal};
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
            ScriptExecutor retVal = new ScriptExecutor(context) { StatusName = ScriptExecutionStatus.Literal};
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
            var retVal = new ScriptExecutor(context)
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
            var retVal = new ScriptExecutor(context)
            {
                StatusName = ScriptExecutionStatus.FunctionLiteral
            };

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

            var retVal = new ScriptExecutor(context)
            {
                StatusName = ScriptExecutionStatus.FunctionLiteral
            };

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

            var ret = new ScriptExecutor(context)
            {
                StatusName = ScriptExecutionStatus.Literal
            };
            ret.SetStatusArguments(new LiteralArguments{Value = null});
            if (typeEx != null)
            {
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

            var retExecutor = new ScriptExecutor(context)
            {
                StatusName = ScriptExecutionStatus.TypeLiteral
            };
            retExecutor.SetStatusArguments(new TypeLiteralArguments{BaseType = retVal});
            if (typeArgs != null && typeArgs.ChildExecutors.Count != 0)
            {
                typeArgs.ElementName = TypeLiteralArguments.GenericArguments;
                retExecutor.ChildExecutors.Add(typeArgs);
            }

            return retExecutor;
        }

    }
}
