/*//#define UseVisitSingleExpression

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
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
using ITVComponents.Scripting.CScript.ReflectionHelpers;
using ITVComponents.Scripting.CScript.ScriptingTree;
using ITVComponents.Scripting.CScript.ScriptValues;
using ITVComponents.Scripting.CScript.Security;
using ITVComponents.Scripting.CScript.Security.Restrictions;
using Newtonsoft.Json.Linq;
using static Antlr4.Runtime.Atn.SemanticContext;
using ValueType = ITVComponents.Scripting.CScript.ScriptValues.ValueType;
using Void = ITVComponents.Scripting.CScript.ScriptValues.Void;

namespace ITVComponents.Scripting.CScript.Core
{
    public class ScriptTreeBuilder : ITVScriptingBaseVisitor<ScriptTree>
    {
        

        public ScriptTreeBuilder()
        {
        }

        protected override ScriptTree DefaultResult
        {
            get { return new ScriptTree(TreeNodeType.Empty); }
        }

        public override ScriptTree VisitProgram(ITVScriptingParser.ProgramContext context)
        {
            return VisitSourceElements(context.sourceElements());
        }

        public override ScriptTree VisitSourceElements(ITVScriptingParser.SourceElementsContext context)
        {
            ScriptTree retVal = new ScriptTree( TreeNodeType.Block);
            ITVScriptingParser.SourceElementContext[] elements = context.sourceElement();
            foreach (var element in elements)
            {
                var item = VisitSourceElement(element);
                retVal.Children.Add(item);
            }

            return retVal;
        }

        public override ScriptTree VisitBlock(ITVScriptingParser.BlockContext context)
        {
            ScriptTree retVal = new ScriptTree(TreeNodeType.Block);
            ITVScriptingParser.StatementListContext list = context.statementList();
            if (list != null)
            {
                var li = VisitStatementList(context.statementList());
                retVal.Children.AddRange(li.Children);
            }

            return retVal;
        }

        public override ScriptTree VisitStatementList(ITVScriptingParser.StatementListContext context)
        {
            var retVal = new ScriptTree (TreeNodeType.Block);
            ITVScriptingParser.StatementContext[] statements = context.statement();
            foreach (ITVScriptingParser.StatementContext statement in statements)
            {
                ScriptTree value = VisitStatement(statement);
                retVal.Children.Add(value);
            }

            return retVal;
        }

        public override ScriptTree VisitEmptyStatement(ITVScriptingParser.EmptyStatementContext context)
        {
            return ScriptTree.Empty;
        }

        public override ScriptTree VisitExpressionStatement(ITVScriptingParser.ExpressionStatementContext context)
        {
            return VisitExpressionSequence(context.expressionSequence());
        }

        public override ScriptTree VisitIfStatement(ITVScriptingParser.IfStatementContext context)
        {
            var retVal = new ScriptTree (TreeNodeType.IfStatement);
            ScriptTree val = Visit(context.singleExpression());
            val.Usage = TreeUsage.Condition;
            retVal.Children.Add(val);
            ITVScriptingParser.StatementContext[] statements = context.statement();
            var thenStatement = VisitStatement(statements[0]);
            thenStatement.Usage = TreeUsage.Then;
            retVal.Children.Add(thenStatement);
            if (statements.Length > 1)
            {
                var elseStatement = VisitStatement(statements[1]);
                elseStatement.Usage = TreeUsage.Else;
                retVal.Children.Add(elseStatement);
            }

            return retVal;
        }

        public override ScriptTree VisitDoStatement(ITVScriptingParser.DoStatementContext context)
        {
            var retVal = new ScriptTree (TreeNodeType.DoLoop);
            ITVScriptingParser.StatementContext body = context.statement();
            ITVScriptingParser.SingleExpressionContext condition = context.singleExpression();
            var bodyTree = VisitStatement(body);
            bodyTree.Usage = TreeUsage.LoopBody;
            var conditionTree = Visit(condition);
            conditionTree.Usage = TreeUsage.Condition;
            retVal.Children.Add(bodyTree);
            retVal.Children.Add(conditionTree);
            return retVal;
        }

        public override ScriptTree VisitWhileStatement(ITVScriptingParser.WhileStatementContext context)
        {
            var retVal = new ScriptTree (TreeNodeType.WhileLoop);
            ITVScriptingParser.StatementContext body = context.statement();
            ITVScriptingParser.SingleExpressionContext condition = context.singleExpression();
            var bodyTree = VisitStatement(body);
            bodyTree.Usage = TreeUsage.LoopBody;
            var conditionTree = Visit(condition);
            conditionTree.Usage = TreeUsage.Condition;
            retVal.Children.Add(bodyTree);
            retVal.Children.Add(conditionTree);
            return retVal;
        }

        public override ScriptTree VisitForStatement(ITVScriptingParser.ForStatementContext context)
        {
            ITVScriptingParser.StatementContext body = context.statement();
            ITVScriptingParser.ExpressionSequenceContext[] header = context.expressionSequence();
            if (header.Length != 3)
            {
                throw new ScriptException(string.Format("Invalid For - Statement at {0}/{1}", context.Start.Line,
                    context.Start.Column));
            }

            var retVal = new ScriptTree (TreeNodeType.ForLoop);
            ITVScriptingParser.ExpressionSequenceContext start, condition, loopAction;
            start = header[0];
            condition = header[1];
            loopAction = header[2];
            var startTree = VisitExpressionSequence(start);
            startTree.Usage = TreeUsage.ForLoopStart;
            retVal.Children.Add(startTree);
            var conditionTree = VisitExpressionSequence(condition);
            conditionTree.Usage = TreeUsage.Condition;
            retVal.Children.Add(conditionTree);
            var loopActionTree = VisitExpressionSequence(loopAction);
            loopActionTree.Usage = TreeUsage.ForLoopAction;
            retVal.Children.Add(loopActionTree);
            var bodyTree = VisitStatement(body);
            bodyTree.Usage = TreeUsage.LoopBody;
            retVal.Children.Add(bodyTree);
            return retVal;
        }

        public override ScriptTree VisitForInStatement(ITVScriptingParser.ForInStatementContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] startExpressions = context.singleExpression();
            ITVScriptingParser.StatementContext body = context.statement();
            ITVScriptingParser.SingleExpressionContext runVar = startExpressions[0];
            ITVScriptingParser.SingleExpressionContext enumerableValue = startExpressions[1];
            var retVal = new ScriptTree (TreeNodeType.EachLoop);
            var runVarTree = Visit(runVar);
            runVarTree.Usage = TreeUsage.EachLoopVar;
            retVal.Children.Add(runVarTree);
            var enumTree = Visit(runVar);
            enumTree.Usage = TreeUsage.EachLoopEnum;
            retVal.Children.Add(enumTree);
            var bodyTree = VisitStatement(body);
            bodyTree.Usage = TreeUsage.LoopBody;
            retVal.Children.Add(bodyTree);
            return retVal;
        }

        public override ScriptTree VisitContinueStatement(ITVScriptingParser.ContinueStatementContext context)
        {
            return new ScriptTree (TreeNodeType.Continue);
        }

        public override ScriptTree VisitBreakStatement(ITVScriptingParser.BreakStatementContext context)
        {
            return new ScriptTree (TreeNodeType.Break);
        }

        public override ScriptTree VisitReturnStatement(ITVScriptingParser.ReturnStatementContext context)
        {
            var retVal = new ScriptTree (TreeNodeType.Return);
            var val = Visit(context.singleExpression());
            retVal.Children.Add(val);
            return retVal;
        }

        public override ScriptTree VisitSwitchStatement(ITVScriptingParser.SwitchStatementContext context)
        {
            var retVal = new ScriptTree (TreeNodeType.Switch);

            ScriptTree caseValue = Visit(context.singleExpression());
            caseValue.Usage = TreeUsage.Condition;
            retVal.Children.Add(caseValue);

            var caseBlock = VisitCaseBlock(context.caseBlock());
            retVal.Children.AddRange(caseBlock.Children);
            return retVal;
        }

        public override ScriptTree VisitCaseBlock(ITVScriptingParser.CaseBlockContext context)
        {
            var retVal = new ScriptTree(TreeNodeType.Block);
            ITVScriptingParser.CaseClausesContext cases = context.caseClauses();
            ITVScriptingParser.DefaultClauseContext defaultClause = context.defaultClause();
            ScriptTree tmp = VisitCaseClauses(cases);
            retVal.Children.AddRange(tmp.Children);

            if (defaultClause != null)
            {
                tmp = VisitDefaultClause(defaultClause);
                retVal.Children.Add(tmp);
            }

            return retVal;
        }

        public override ScriptTree VisitCaseClauses(ITVScriptingParser.CaseClausesContext context)
        {
            var retVal = new ScriptTree(TreeNodeType.Block);
            ITVScriptingParser.CaseClauseContext[] allCases = context.caseClause();
            bool ok = false;
            foreach (ITVScriptingParser.CaseClauseContext singleCase in allCases)
            {
                ok = true;
                ScriptTree ret = VisitCaseClause(singleCase);
                retVal.Children.Add(ret);
            }

            if (!ok)
            {
                throw new ScriptException(string.Format("No Cases defined at {0}/{1}", context.Start.Line,
                    context.Start.Column));
            }

            return retVal;
        }

        public override ScriptTree VisitCaseClause(ITVScriptingParser.CaseClauseContext context)
        {
            ITVScriptingParser.SingleExpressionContext expression = context.singleExpression();
            ITVScriptingParser.StatementListContext statements = context.statementList();
            var retVal = new ScriptTree (TreeNodeType.Block)
            {
                Usage = TreeUsage.CaseBlock
            };
            ScriptTree val = Visit(expression);
            val.Usage = TreeUsage.CaseLabel;
            retVal.Children.Add(val);

            var statementTree = VisitStatementList(statements);
            retVal.Children.AddRange(statementTree.Children);
            return retVal;
        }

        public new ScriptTree VisitDefaultClause(ITVScriptingParser.DefaultClauseContext context)
        {
            var retVal = new ScriptTree (TreeNodeType.Block)
            {
                Usage = TreeUsage.CaseDefault
            };
            var block = VisitStatementList(context.statementList());
            retVal.Children.AddRange(block.Children);
            return retVal;
        }

        public override ScriptTree VisitThrowStatement(ITVScriptingParser.ThrowStatementContext context)
        {
            ITVScriptingParser.SingleExpressionContext exception = context.singleExpression();
            var retVal = new ScriptTree (TreeNodeType.Throw);
            if (exception != null)
            {
                ScriptTree val = Visit(exception);
                val.Usage = TreeUsage.Exception;
                retVal.Children.Add(val);
            }
            else
            {
                retVal.Usage = TreeUsage.ReThrow;
            }

            return retVal;
        }

        public override ScriptTree VisitTryStatement(ITVScriptingParser.TryStatementContext context)
        {
            ITVScriptingParser.BlockContext block = context.block();
            ITVScriptingParser.CatchProductionContext catchBlock = context.catchProduction();
            ITVScriptingParser.FinallyProductionContext finallyBlock = context.finallyProduction();
            ScriptTree retVal = new ScriptTree (TreeNodeType.Try);
            ScriptTree value;
            value = VisitBlock(block);
            value.Usage = TreeUsage.TryBlock;
            retVal.Children.Add(value);
            if (catchBlock != null)
            {
                value = VisitCatchProduction(catchBlock);
                retVal.Children.Add(value);
            }

            if (finallyBlock != null)
            {
                value = VisitFinallyProduction(finallyBlock);
                retVal.Children.Add(value);
            }

            return retVal;
        }

        public override ScriptTree VisitCatchProduction(ITVScriptingParser.CatchProductionContext context)
        {
            var retVal = VisitBlock(context.block());
            retVal.Name = context.Identifier().GetText();
            retVal.Usage = TreeUsage.CatchBlock;
            return retVal;
        }

        public override ScriptTree VisitFinallyProduction(ITVScriptingParser.FinallyProductionContext context)
        {
            var retVal = VisitBlock(context.block());
            retVal.Usage = TreeUsage.FinallyBlock;
            return retVal;
        }

        public override ScriptTree VisitArrayLiteral(ITVScriptingParser.ArrayLiteralContext context)
        {
            ScriptTree retVal = new ScriptTree(TreeNodeType.ArrayLiteral);
            var list = context.elementList();
            ScriptTree value = VisitElementList(list);
            retVal.Children.AddRange(value.Children);
            return retVal;
        }

        public override ScriptTree VisitElementList(ITVScriptingParser.ElementListContext context)
        {
            var retVal = new ScriptTree(TreeNodeType.Sequence);
            retVal.Usage = TreeUsage.ElementList;
            foreach (ITVScriptingParser.SingleExpressionContext se in context.singleExpression())
            {
                ScriptTree tmp = Visit(se);
                retVal.Children.Add(tmp);
            }

            return retVal;
        }

        public override ScriptTree VisitArguments(ITVScriptingParser.ArgumentsContext context)
        {
            return VisitArgumentList(context.argumentList());
        }

        public override ScriptTree VisitFinalGenerics(ITVScriptingParser.FinalGenericsContext context)
        {
            return VisitTypedArguments(context.typedArguments());
        }

        public override ScriptTree VisitTypedArguments(ITVScriptingParser.TypedArgumentsContext context)
        {
            List<ScriptValue> elements = new List<ScriptValue>();
            ITVScriptingParser.TypeIdentifierContext[] types = context.typeIdentifier();
            ScriptTree retVal = new ScriptTree(TreeNodeType.Sequence);
            retVal.Usage = TreeUsage.TypeArgumentList;
            foreach (ITVScriptingParser.TypeIdentifierContext se in types)
            {
                ScriptTree tmp = VisitTypeIdentifier(se);
                retVal.Children.Add(tmp);
            }

            return retVal;
        }

        public override ScriptTree VisitExplicitTypeHint(ITVScriptingParser.ExplicitTypeHintContext context)
        {
            return VisitTypeIdentifier(context.typeIdentifier());
        }

        public override ScriptTree VisitTypeIdentifier(ITVScriptingParser.TypeIdentifierContext context)
        {
            var path = context.Identifier();
            var retVal = new ScriptTree(TreeNodeType.TypePath) { Name = string.Join(".",path.Select(p => p.GetText())) };
            return retVal;
        }

        public override ScriptTree VisitArgumentList(ITVScriptingParser.ArgumentListContext context)
        {
            var retVal = new ScriptTree (TreeNodeType.Sequence) { Usage= TreeUsage.ArgumentList };
            if (context != null)
            {
                ITVScriptingParser.SingleExpressionContext[] expressions = context.singleExpression();
                if (expressions != null)
                {
                    foreach (ITVScriptingParser.SingleExpressionContext se in expressions)
                    {
                        ScriptTree tmp = Visit(se);
                        retVal.Children.Add(tmp);
                    }
                }
            }

            return retVal;
        }

        public override ScriptTree VisitExpressionSequence(ITVScriptingParser.ExpressionSequenceContext context)
        {
            var retVal = new ScriptTree(TreeNodeType.Sequence);
            ITVScriptingParser.SingleExpressionContext[] sequence = context.singleExpression();
            List<ScriptValue> val = new List<ScriptValue>();
            foreach (var item in sequence)
            {
                var node = Visit(item);
                retVal.Children.Add(node);
            }

            return retVal;
        }

        public override ScriptTree VisitTernaryExpression(ITVScriptingParser.TernaryExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] values = context.singleExpression();
            ScriptTree first = Visit(values[0]);
            first.Usage = TreeUsage.Condition;
            ScriptTree trueExpression = Visit(values[1]);
            trueExpression.Usage = TreeUsage.Then;
            ScriptTree falseExpression = Visit(values[2]);
            falseExpression.Usage = TreeUsage.Else;
            var retVal = new ScriptTree (TreeNodeType.TernaryExpression);
            retVal.Children.Add(first);
            retVal.Children.Add(trueExpression);
            retVal.Children.Add(falseExpression);
            return Visit(values[2]);
        }

        public override ScriptTree VisitLogicalAndExpression(ITVScriptingParser.LogicalAndExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] expressions = context.singleExpression();
            ScriptTree v1 = Visit(expressions[0]);
            ScriptTree v2 = Visit(expressions[2]);
            var retVal = new ScriptTree (TreeNodeType.AndAlso);
            retVal.Children.Add(v1);
            retVal.Children.Add(v2);
            return retVal;
        }

        public override ScriptTree VisitPreIncrementExpression(ITVScriptingParser.PreIncrementExpressionContext context)
        {
            ScriptTree val = Visit(context.singleExpression());
            ScriptTree retVal = new ScriptTree (TreeNodeType.PreIncrement);
            retVal.Children.Add(val);
            return retVal;
        }

        public override ScriptTree VisitLogicalOrExpression(ITVScriptingParser.LogicalOrExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] expressions = context.singleExpression();
            ScriptTree v1 = Visit(expressions[0]);
            ScriptTree v2 = Visit(expressions[2]);
            var retVal = new ScriptTree(TreeNodeType.OrElse );
            retVal.Children.Add(v1);
            retVal.Children.Add(v2);
            return retVal;
        }

        public override ScriptTree VisitNotExpression(ITVScriptingParser.NotExpressionContext context)
        {
            ScriptTree value = Visit(context.singleExpression());

            ScriptTree retVal = new ScriptTree(TreeNodeType.Not );
            retVal.Children.Add(value);
            return retVal;
        }

        public override ScriptTree VisitPreDecreaseExpression(ITVScriptingParser.PreDecreaseExpressionContext context)
        {
            ScriptTree val = Visit(context.singleExpression());
            ScriptTree retVal = new ScriptTree(TreeNodeType.PreDecrease );
            retVal.Children.Add(val);
            return retVal;
        }

        public override ScriptTree VisitArgumentsExpression(ITVScriptingParser.ArgumentsExpressionContext context)
        {
            ScriptTree baseValue = Visit(context.singleExpression());
            baseValue.Usage = TreeUsage.MethodCall;
            ScriptTree arguments = VisitArguments(context.arguments());
            baseValue.Children.Add(arguments);
            
            ITVScriptingParser.TypeArgumentsContext targ = context.typeArguments();
            if (targ != null)
            {
                if (targ is ITVScriptingParser.FinalGenericsContext genericsContext)
                {
                    ScriptTree typeArguments = VisitFinalGenerics(genericsContext);
                    baseValue.Children.Add(typeArguments);
                }
                else
                {
                    throw new ScriptException(string.Format("Open Generic Arguments are not supported in Methodcalls! at {0}/{1}",
                            context.Start.Line, context.Start.Column));
                }
            }

            ITVScriptingParser.ExplicitTypeHintContext ext = context.explicitTypeHint();
            if (ext != null)
            {
                var explicitTyping = VisitExplicitTypeHint(ext);
                baseValue.Children.Add(explicitTyping);
            }

            return baseValue;
        }

        public override ScriptTree VisitUnaryMinusExpression(ITVScriptingParser.UnaryMinusExpressionContext context)
        {
            ScriptTree val = Visit(context.singleExpression());
            ScriptTree retVal = new ScriptTree(TreeNodeType.UnaryMinus);
            retVal.Children.Add(val);
            return retVal;
        }

        public override ScriptTree VisitMemberDotQExpression(ITVScriptingParser.MemberDotQExpressionContext context)
        {
            ScriptTree baseVal = Visit(context.singleExpression());
            ScriptTree retVal = new ScriptTree(TreeNodeType.NullableMemberAccess)
                { Name = context.identifierName().GetText() };
            retVal.Children.Add(baseVal);
            var eth = context.explicitTypeHint();
            if (eth != null)
            {
                var typeHint = VisitExplicitTypeHint(eth);
                baseVal.Children.Add(typeHint);
            }

            return retVal;
        }

        public override ScriptTree VisitPostDecreaseExpression(ITVScriptingParser.PostDecreaseExpressionContext context)
        {
            ScriptTree val = Visit(context.singleExpression());
            ScriptTree retVal = new ScriptTree (TreeNodeType.PostDecrease);
            retVal.Children.Add(val);
            return retVal;
        }

        public override ScriptTree VisitAssignmentExpression(ITVScriptingParser.AssignmentExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            ScriptTree target = Visit(subExpressions[0]);
            target.Usage = TreeUsage.AssignmentTarget;
            ScriptTree value = Visit(subExpressions[1]);
            value.Usage = TreeUsage.AssignmentSource;
            var retVal = new ScriptTree(TreeNodeType.Assignment);
            retVal.Children.Add(target);
            retVal.Children.Add(value);
            return retVal;
        }

        public override ScriptTree VisitUnaryPlusExpression(ITVScriptingParser.UnaryPlusExpressionContext context)
        {
            return Visit(context.singleExpression());
        }

        public override ScriptTree VisitEqualityExpression(ITVScriptingParser.EqualityExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] expressions = context.singleExpression();
            ScriptTree leftVal = Visit(expressions[0]);
            leftVal.Usage = TreeUsage.LeftOperand;
            ScriptTree rightVal = Visit(expressions[1]);
            rightVal.Usage = TreeUsage.RightOperand;
            var retVal = new ScriptTree(TreeNodeType.Comparison);
            retVal.Children.Add(leftVal);
            retVal.Children.Add(rightVal);
            return retVal;
        }

        public override ScriptTree VisitBitXOrExpression(ITVScriptingParser.BitXOrExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            ScriptTree leftVal = Visit(subExpressions[0]);
            leftVal.Usage = TreeUsage.LeftOperand;
            ScriptTree rightVal = Visit(subExpressions[1]);
            rightVal.Usage = TreeUsage.RightOperand;
            var retVal = new ScriptTree(TreeNodeType.BitXor);
            retVal.Children.Add(leftVal);
            retVal.Children.Add(rightVal);
            return retVal;
        }

        public override ScriptTree VisitMultiplicativeExpression(ITVScriptingParser.MultiplicativeExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            ScriptTree leftVal = Visit(subExpressions[0]);
            leftVal.Usage = TreeUsage.LeftOperand;
            ScriptTree rightVal = Visit(subExpressions[1]);
            rightVal.Usage = TreeUsage.RightOperand;
            var retVal = new ScriptTree(TreeNodeType.MathOperation);
            retVal.Children.Add(leftVal);
            retVal.Children.Add(rightVal);
            string op = context.GetChild(1).GetText();
            switch (op)
            {
                case "*":
                {
                    retVal.Operator = TreeOperator.Multiply;
                    break;
                }
                case "/":
                {
                    retVal.Operator = TreeOperator.Divide;
                    break;
                }
                case "%":
                {
                    retVal.Operator = TreeOperator.Modulus;
                    break;
                }
                default:
                {
                    throw new ScriptException(string.Format("Unable to identify multiplicative operation at {0}/{1}",
                        context.Start.Line,
                        context.Start.Column));
                }
            }

            return retVal;
        }

        public override ScriptTree VisitBitShiftExpression(ITVScriptingParser.BitShiftExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            ScriptTree leftVal = Visit(subExpressions[0]);
            leftVal.Usage = TreeUsage.LeftOperand;
            ScriptTree rightVal = Visit(subExpressions[1]);
            rightVal.Usage = TreeUsage.RightOperand;
            var retVal = new ScriptTree(TreeNodeType.MathOperation);
            retVal.Children.Add(leftVal);
            retVal.Children.Add(rightVal);
            string op = context.GetChild(1).GetText();
            switch (op)
            {
                case "<<":
                    {
                        retVal.Operator = TreeOperator.ShiftLeft;
                        break;
                    }
                case ">>":
                    {
                        retVal.Operator = TreeOperator.ShiftRight;
                        break;
                    }
                default:
                    {
                        throw new ScriptException(string.Format("Unable to identify shift operation at {0}/{1}", context.Start.Line,
                                          context.Start.Column));
                    }
            }

            return retVal;
        }

        public override ScriptTree VisitParenthesizedExpression(ITVScriptingParser.ParenthesizedExpressionContext context)
        {
            return Visit(context.singleExpression());
        }

        public override ScriptTree VisitAdditiveExpression(ITVScriptingParser.AdditiveExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            ScriptTree leftVal = Visit(subExpressions[0]);
            leftVal.Usage = TreeUsage.LeftOperand;
            ScriptTree rightVal = Visit(subExpressions[1]);
            rightVal.Usage = TreeUsage.RightOperand;
            var retVal = new ScriptTree(TreeNodeType.MathOperation);
            retVal.Children.Add(leftVal);
            retVal.Children.Add(rightVal);
            string op = context.GetChild(1).GetText();
            switch (op)
            {
                case "+":
                    {
                        retVal.Operator = TreeOperator.Plus;
                        break; 
                    }
                case "-":
                    {
                        retVal.Operator = TreeOperator.Minus;
                        break;
                    }
                default:
                    {
                        throw new ScriptException(string.Format("Unable to perform additive operation at {0}/{1}", context.Start.Line,
                                          context.Start.Column));
                    }
            }

            return retVal;
        }

        public override ScriptTree VisitRelationalExpression(ITVScriptingParser.RelationalExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            ScriptTree leftVal = Visit(subExpressions[0]);
            leftVal.Usage = TreeUsage.LeftOperand;
            ScriptTree rightVal = Visit(subExpressions[1]);
            rightVal.Usage = TreeUsage.RightOperand;
            var retVal = new ScriptTree(TreeNodeType.MathOperation);
            retVal.Children.Add(leftVal);
            retVal.Children.Add(rightVal);
            string op = context.GetChild(1).GetText();
            switch (op)
            {
                case ">":
                {
                    retVal.Operator = TreeOperator.GreaterThan;
                    break;
                }
                case ">=":
                {
                    retVal.Operator = TreeOperator.GreaterThanOrEqual;
                        break;
                }
                case "<":
                {
                    retVal.Operator = TreeOperator.LessThan;
                        break;
                }
                case "<=":
                {
                    retVal.Operator = TreeOperator.LessThanOrEqual;
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
    }
}
*/