using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime.Tree;
using ITVComponents.Scripting.CScript.Evaluators;
using ITVComponents.Scripting.CScript.Evaluators.FlowControl;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Operating;

namespace ITVComponents.Scripting.CScript.Core
{
    public class ScriptBuilder:ITVScriptingBaseVisitor<EvaluatorBase>
    {
        protected override EvaluatorBase DefaultResult => new RootEvaluator();

        public override EvaluatorBase VisitBlock(ITVScriptingParser.BlockContext context)
        {
            return new BlockEvaluator(new[]{Visit(context.statementList())}, context);
        }

        public override EvaluatorBase VisitStatementList(ITVScriptingParser.StatementListContext context)
        {
            var children = (from t in context.statement() select Visit(t)).ToArray();
            return new SequenceEvaluator(children, SequenceType.StatementSequence, context);
        }

        public override EvaluatorBase VisitEmptyStatement(ITVScriptingParser.EmptyStatementContext context)
        {
            return new SequenceEvaluator(new EvaluatorBase[0], SequenceType.StatementSequence, context);
        }

        public override EvaluatorBase VisitExpressionStatement(ITVScriptingParser.ExpressionStatementContext context)
        {
            return VisitExpressionSequence(context.expressionSequence());
        }

        public override EvaluatorBase VisitExpressionSequence(ITVScriptingParser.ExpressionSequenceContext context)
        {
            var children = (from t in context.singleExpression() select Visit(t)).ToArray();
            return new SequenceEvaluator(children, SequenceType.ExpressionSequence, context);
        }

        public override EvaluatorBase VisitAdditiveExpression(ITVScriptingParser.AdditiveExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            var leftChild = Visit(subExpressions[0]);
            var rightChild = Visit(subExpressions[1]);
            string op = context.GetChild(1).GetText();
            return new OperationEvaluator(leftChild, rightChild, op, context);
        }

        public override EvaluatorBase VisitMultiplicativeExpression(ITVScriptingParser.MultiplicativeExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            var leftChild = Visit(subExpressions[0]);
            var rightChild = Visit(subExpressions[1]);
            string op = context.GetChild(1).GetText();
            return new OperationEvaluator(leftChild, rightChild, op, context);
        }

        public override EvaluatorBase VisitBitXOrExpression(ITVScriptingParser.BitXOrExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            var leftChild = Visit(subExpressions[0]);
            var rightChild = Visit(subExpressions[1]);
            string op = context.GetChild(1).GetText();
            return new OperationEvaluator(leftChild, rightChild, op, context);
        }

        public override EvaluatorBase VisitSwitchStatement(ITVScriptingParser.SwitchStatementContext context)
        {
            var switchExpression = Visit(context.singleExpression());
            var caseClauses = ( from t in context.caseBlock().caseClauses().caseClause() select (CaseClauseEvaluator)Visit(t)).ToArray();
            var defaultClause = (CaseClauseEvaluator)VisitDefaultClause(context.caseBlock().defaultClause());
            return new SwitchEvaluator(switchExpression, caseClauses, defaultClause, context);
        }

        public override EvaluatorBase VisitCaseClause(ITVScriptingParser.CaseClauseContext context)
        {
            return new CaseClauseEvaluator(Visit(context.singleExpression()), false,
                (SequenceEvaluator)Visit(context.statementList()), context);
        }

        public override EvaluatorBase VisitDefaultClause(ITVScriptingParser.DefaultClauseContext context)
        {
            return new CaseClauseEvaluator(null, true, (SequenceEvaluator)Visit(context.statementList()), context);
        }

        public override EvaluatorBase VisitContinueStatement(ITVScriptingParser.ContinueStatementContext context)
        {
            return new PassThroughValueEvaluator(context);
        }

        public override EvaluatorBase VisitBreakStatement(ITVScriptingParser.BreakStatementContext context)
        {
            return new PassThroughValueEvaluator(context);
        }

        public override EvaluatorBase VisitReturnStatement(ITVScriptingParser.ReturnStatementContext context)
        {
            var value = context.singleExpression();
            if (value != null)
            {
                return new PassThroughValueEvaluator(context, Visit(value));
            }

            return new PassThroughValueEvaluator(context);
        }

        public override EvaluatorBase VisitThrowStatement(ITVScriptingParser.ThrowStatementContext context)
        {
            var value = context.singleExpression();
            if (value != null)
            {
                return new PassThroughValueEvaluator(context, Visit(value));
            }

            return new PassThroughValueEvaluator(context);
        }

        public override EvaluatorBase VisitBitOrExpression(ITVScriptingParser.BitOrExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            var leftChild = Visit(subExpressions[0]);
            var rightChild = Visit(subExpressions[1]);
            string op = context.GetChild(1).GetText();
            return new OperationEvaluator(leftChild, rightChild, op, context);
        }

        public override EvaluatorBase VisitBitAndExpression(ITVScriptingParser.BitAndExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            var leftChild = Visit(subExpressions[0]);
            var rightChild = Visit(subExpressions[1]);
            string op = context.GetChild(1).GetText();
            return new OperationEvaluator(leftChild, rightChild, op, context);
        }

        public override EvaluatorBase VisitBitShiftExpression(ITVScriptingParser.BitShiftExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            var leftChild = Visit(subExpressions[0]);
            var rightChild = Visit(subExpressions[1]);
            string op = context.GetChild(1).GetText();
            return new OperationEvaluator(leftChild, rightChild, op, context);
        }

        public override EvaluatorBase VisitRelationalExpression(ITVScriptingParser.RelationalExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            var leftChild = Visit(subExpressions[0]);
            var rightChild = Visit(subExpressions[1]);
            string op = context.GetChild(1).GetText();
            return new OperationEvaluator(leftChild, rightChild, op, context);
        }

        public override EvaluatorBase VisitEqualityExpression(ITVScriptingParser.EqualityExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            var leftChild = Visit(subExpressions[0]);
            var rightChild = Visit(subExpressions[1]);
            string op = context.GetChild(1).GetText();
            return new OperationEvaluator(leftChild, rightChild, op, context);
        }

        public override EvaluatorBase VisitLogicalAndExpression(ITVScriptingParser.LogicalAndExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            var leftChild = Visit(subExpressions[0]);
            var rightChild = Visit(subExpressions[1]);
            string op = context.GetChild(1).GetText();
            return new LogicalBooleanEvaluator(leftChild, rightChild, op, context);
        }

        public override EvaluatorBase VisitLogicalOrExpression(ITVScriptingParser.LogicalOrExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            var leftChild = Visit(subExpressions[0]);
            var rightChild = Visit(subExpressions[1]);
            string op = context.GetChild(1).GetText();
            return new LogicalBooleanEvaluator(leftChild, rightChild, op, context);

        }

        public override EvaluatorBase VisitInstanceIsNullExpression(ITVScriptingParser.InstanceIsNullExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            var leftChild = Visit(subExpressions[0]);
            var rightChild = Visit(subExpressions[1]);
            string op = context.GetChild(1).GetText();
            return new LogicalBooleanEvaluator(leftChild, rightChild, op, context);
        }

        public override EvaluatorBase VisitTernaryExpression(ITVScriptingParser.TernaryExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            var condition = Visit(subExpressions[0]);
            var leftChild = Visit(subExpressions[1]);
            var rightChild = Visit(subExpressions[2]);
            return new TernaryEvaluator(condition, leftChild, rightChild, context);
        }

        public override EvaluatorBase VisitParenthesizedExpression(ITVScriptingParser.ParenthesizedExpressionContext context)
        {
            return Visit(context.singleExpression());
        }

        public override EvaluatorBase VisitArgumentList(ITVScriptingParser.ArgumentListContext context)
        {
            List<EvaluatorBase> elements = new List<EvaluatorBase>();
            if (context != null)
            {
                ITVScriptingParser.SingleExpressionContext[] expressions = context.singleExpression();
                if (expressions != null)
                {
                    foreach (ITVScriptingParser.SingleExpressionContext se in expressions)
                    {
                        EvaluatorBase tmp = Visit(se);
                        elements.Add(tmp);
                    }
                }
            }

            SequenceEvaluator rv = new SequenceEvaluator(elements, SequenceType.ExpressionSequence, context);
            return rv;
        }

        public override EvaluatorBase VisitArguments(ITVScriptingParser.ArgumentsContext context)
        {
            return VisitArgumentList(context.argumentList());
        }

        public override EvaluatorBase VisitArgumentsExpression(ITVScriptingParser.ArgumentsExpressionContext context)
        {
            var argumentEvaluator = (SequenceEvaluator)VisitArguments(context.arguments());
            SequenceEvaluator typeEvaluator = null;
            ITVScriptingParser.TypeArgumentsContext targ = context.typeArguments();
            if (targ != null)
            {
                var genericsContext = targ as ITVScriptingParser.FinalGenericsContext;
                if (genericsContext != null)
                {
                    typeEvaluator = (SequenceEvaluator)VisitFinalGenerics(genericsContext);
                }
                else
                {
                    throw new InvalidOperationException($"Open Generic Arguments are not supported in Methodcalls! at {context.Start.Line}/{context.Start.Column}");
                }
            }

            TypeIdentifierEvaluator explicitTyping = null;
            ITVScriptingParser.ExplicitTypeHintContext ext = context.explicitTypeHint();
            if (ext != null)
            {
                explicitTyping = (TypeIdentifierEvaluator)VisitExplicitTypeHint(ext);
            }

            EvaluatorBase parentObj = Visit(context.singleExpression());
            parentObj.ExpectedResult = ResultType.Method;
            return new CallMethodEvaluator(parentObj, argumentEvaluator, typeEvaluator, explicitTyping, context);
        }

        public override EvaluatorBase VisitExplicitTypeHint(ITVScriptingParser.ExplicitTypeHintContext context)
        {
            return VisitTypeIdentifier(context.typeIdentifier());
        }

        public override EvaluatorBase VisitTypeIdentifier(ITVScriptingParser.TypeIdentifierContext context)
        {
            return new TypeIdentifierEvaluator(context);
        }

        public override EvaluatorBase VisitTypedArguments(ITVScriptingParser.TypedArgumentsContext context)
        {
            var typeContexts = new List<TypeIdentifierEvaluator>();
            foreach (var i in context.typeIdentifier())
            {
                typeContexts.Add((TypeIdentifierEvaluator)Visit(i));
            }

            return new SequenceEvaluator(typeContexts.ToList<EvaluatorBase>(), SequenceType.ExpressionSequence, context);
        }

        public override EvaluatorBase VisitFinalGenerics(ITVScriptingParser.FinalGenericsContext context)
        {
            return Visit(context.typedArguments());
        }

        public override EvaluatorBase VisitMemberIndexExpression(ITVScriptingParser.MemberIndexExpressionContext context)
        {
            var baseVal = Visit(context.singleExpression());
            var argumentEvaluator = (SequenceEvaluator)VisitExpressionSequence(context.expressionSequence());
            EvaluatorBase explicitType = null;
            var t = context.explicitTypeHint();
            if (t != null)
            {
                explicitType = Visit(t);
            }

            return new CallIndexerEvaluator(baseVal, argumentEvaluator, explicitType, context);
        }

        public override EvaluatorBase VisitHasMemberExpression(ITVScriptingParser.HasMemberExpressionContext context)
        {
            EvaluatorBase sample = Visit(context.singleExpression());
            string name = context.identifierName().GetText();
            TypeIdentifierEvaluator explicitTyping = null;
            ITVScriptingParser.ExplicitTypeHintContext ext = context.explicitTypeHint();
            if (ext != null)
            {
                explicitTyping = (TypeIdentifierEvaluator)VisitExplicitTypeHint(ext);
            }

            var prop = new MemberAccessEvaluator(sample, explicitTyping, context);
            var arg = context.arguments();
            EvaluatorBase retVal = prop;
            if (arg != null)
            {
                SequenceEvaluator arguments = (SequenceEvaluator)VisitArguments(arg);
                SequenceEvaluator typeArguments = null;
                ITVScriptingParser.TypeArgumentsContext targ = context.typeArguments();
                if (targ != null)
                {
                    var genericsContext = targ as ITVScriptingParser.FinalGenericsContext;
                    if (genericsContext != null)
                    {
                        typeArguments = (SequenceEvaluator)VisitFinalGenerics(genericsContext);
                    }
                    else
                    {
                        throw new ScriptException(string.Format(
                            "Open Generic Arguments are not supported in Methodcalls! at {0}/{1}",
                            context.Start.Line, context.Start.Column));
                    }
                }

                prop.ExpectedResult = ResultType.Method;
                retVal = new CallMethodEvaluator(prop, arguments, typeArguments, explicitTyping, context);
            }

            return retVal;
        }

        public override EvaluatorBase VisitMemberIsExpression(ITVScriptingParser.MemberIsExpressionContext context)
        {
            var left = context.singleExpression(0);
            var right = context.singleExpression(1);
            var obj = Visit(left);
            var typ = Visit(right);
            return new IsOfTypeEvaluator(obj, typ, context);
        }

        public override EvaluatorBase VisitAssignmentExpression(ITVScriptingParser.AssignmentExpressionContext context)
        {
            var left = context.singleExpression(0);
            var right = context.singleExpression(1);
            EvaluatorBase leftOperand = Visit(left);
            leftOperand.AccessMode = AccessMode.ReadWrite;
            EvaluatorBase rightOperand = Visit(right);
            var retVal = new AssignmentEvaluator(leftOperand, rightOperand, context);
            return retVal;
        }

        public override EvaluatorBase VisitUnaryMinusExpression(ITVScriptingParser.UnaryMinusExpressionContext context)
        {
            var baseValue = Visit(context.singleExpression());
            return new UnaryOpEvaluator(baseValue, OpPosition.Pre, "-", context);
        }

        public override EvaluatorBase VisitPreIncrementExpression(ITVScriptingParser.PreIncrementExpressionContext context)
        {
            var baseValue = Visit(context.singleExpression());
            baseValue.AccessMode = AccessMode.ReadWrite;
            return new UnaryOpEvaluator(baseValue, OpPosition.Pre, "++", context);
        }

        public override EvaluatorBase VisitPostIncrementExpression(ITVScriptingParser.PostIncrementExpressionContext context)
        {
            var baseValue = Visit(context.singleExpression());
            baseValue.AccessMode = AccessMode.ReadWrite;
            return new UnaryOpEvaluator(baseValue, OpPosition.Post, "++", context);
        }

        public override EvaluatorBase VisitPostDecreaseExpression(ITVScriptingParser.PostDecreaseExpressionContext context)
        {
            var baseValue = Visit(context.singleExpression());
            baseValue.AccessMode = AccessMode.ReadWrite;
            return new UnaryOpEvaluator(baseValue, OpPosition.Post, "--", context);
        }

        public override EvaluatorBase VisitPreDecreaseExpression(ITVScriptingParser.PreDecreaseExpressionContext context)
        {
            var baseValue = Visit(context.singleExpression());
            baseValue.AccessMode = AccessMode.ReadWrite;
            return new UnaryOpEvaluator(baseValue, OpPosition.Pre, "--", context);
        }

        public override EvaluatorBase VisitNotExpression(ITVScriptingParser.NotExpressionContext context)
        {
            var baseValue = Visit(context.singleExpression());
            return new UnaryOpEvaluator(baseValue, OpPosition.Pre, "!", context);
        }

        public override EvaluatorBase VisitBitNotExpression(ITVScriptingParser.BitNotExpressionContext context)
        {
            var baseValue = Visit(context.singleExpression());
            return new UnaryOpEvaluator(baseValue, OpPosition.Pre, "~", context);
        }

        public override EvaluatorBase VisitAssignmentOperatorExpression(ITVScriptingParser.AssignmentOperatorExpressionContext context)
        {
            var left = context.singleExpression(0);
            var op = context.assignmentOperator().GetText();
            var right = context.singleExpression(1);
            EvaluatorBase leftOperand = Visit(left);
            leftOperand.AccessMode = AccessMode.ReadWrite;
            EvaluatorBase rightOperand = Visit(right);
            var opLen = op.IndexOf("=");
            op = op.Substring(0, opLen);
            var retVal = new DirectOpAssignmentEvaluator(leftOperand, rightOperand, op, context);
            return retVal;
        }

        public override EvaluatorBase VisitUnaryPlusExpression(ITVScriptingParser.UnaryPlusExpressionContext context)
        {
            return Visit(context.singleExpression());
        }

        public override EvaluatorBase VisitNewExpression(ITVScriptingParser.NewExpressionContext context)
        {
            var argumentEvaluator = (SequenceEvaluator)VisitArguments(context.arguments());
            SequenceEvaluator typeEvaluator = null;
            ITVScriptingParser.TypeArgumentsContext targ = context.typeArguments();
            if (targ != null)
            {
                var genericsContext = targ as ITVScriptingParser.FinalGenericsContext;
                if (genericsContext != null)
                {
                    typeEvaluator = (SequenceEvaluator)VisitFinalGenerics(genericsContext);
                }
                else
                {
                    throw new InvalidOperationException($"Open Generic Arguments are not supported in Constructors! at {context.Start.Line}/{context.Start.Column}");
                }
            }

            EvaluatorBase parentObj = Visit(context.singleExpression());
            parentObj.ExpectedResult = ResultType.Constructor;
            return new CallConstructorEvaluator(parentObj, argumentEvaluator, typeEvaluator, context);
        }

        public override EvaluatorBase VisitIdentifierExpression(ITVScriptingParser.IdentifierExpressionContext context)
        {
            return new VariableAccessEvaluator(context)
                { AccessMode = AccessMode.Read, ExpectedResult = ResultType.PropertyOrField };
        }

        public override EvaluatorBase VisitMemberDotExpression(ITVScriptingParser.MemberDotExpressionContext context)
        {
            var baseValue = Visit(context.singleExpression());
            var xp = context.explicitTypeHint();
            EvaluatorBase explicitType = null;
            if (xp != null)
            {
                explicitType = Visit(xp);
            }

            return new MemberAccessEvaluator(baseValue, explicitType, context);
        }

        public override EvaluatorBase VisitMemberDotQExpression(ITVScriptingParser.MemberDotQExpressionContext context)
        {
            var baseValue = Visit(context.singleExpression());
            var xp = context.explicitTypeHint();
            EvaluatorBase explicitType = null;
            if (xp != null)
            {
                explicitType = Visit(xp);
            }
             
            return new MemberAccessEvaluator(baseValue, explicitType, context);
        }

        public override EvaluatorBase VisitLiteral(ITVScriptingParser.LiteralContext context)
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

            if (child is ITVScriptingParser.BooleanLiteralContext)
            {
                return new LiteralEvaluator(child.GetText(), LiteralType.Boolean, context);
            }

            return new LiteralEvaluator(child.GetText(), LiteralType.String, context);
        }

        public override EvaluatorBase VisitNumericLiteral(ITVScriptingParser.NumericLiteralContext context)
        {
            ITerminalNode decimalChild = context.DecimalLiteral();
            ITerminalNode octalChild = context.OctalIntegerLiteral();
            ITerminalNode hexalChild = context.HexIntegerLiteral();
            if (decimalChild != null)
            {
                return new LiteralEvaluator(decimalChild.GetText(), LiteralType.Decimal, context);
            }

            if (octalChild != null)
            {
                return new LiteralEvaluator(octalChild.GetText(), LiteralType.OctalInt, context);
            }

            if (hexalChild != null)
            {
                return new LiteralEvaluator(hexalChild.GetText().Substring(2), LiteralType.HexalInt, context);
            }

            throw new ScriptException(string.Format("Unable to create a numeric literal at {0}/{1}", context.Start.Line,
                    context.Start.Column));
        }

        public override EvaluatorBase VisitTypeLiteral(ITVScriptingParser.TypeLiteralContext context)
        {
            string type = context.typeLiteralIdentifier().GetText();
            var targs = context.typeArguments();
            SequenceEvaluator typeArgs = null;
            if (targs != null)
            {
                ITVScriptingParser.FinalGenericsContext finalGenerics = targs as ITVScriptingParser.FinalGenericsContext;
                if (finalGenerics != null)
                {
                    var tmp = VisitFinalGenerics(finalGenerics);
                    typeArgs = (SequenceEvaluator)tmp;
                    type += string.Format("`{0}", typeArgs.EvaluationChildCount);
                }
                else
                {
                    type += targs.GetText();
                }
            }

            string assembly = null;
            ITerminalNode path = context.StringLiteral();
            if (path != null)
            {
                assembly = StringHelper.Parse(path.GetText());
            }

            return new LiteralEvaluator(type, LiteralType.Type, typeArgs, assembly, context);
        }

        public override EvaluatorBase VisitNullLiteral(ITVScriptingParser.NullLiteralContext context)
        {
            LiteralEvaluator baseVal = null;
            var tl = context.typeLiteral();
            if (tl != null)
            {
                baseVal = (LiteralEvaluator)VisitTypeLiteral(tl);
            }

            return new LiteralExtensionEvaluator(baseVal, ExtensionType.Null, context);
        }

        public override EvaluatorBase VisitRefLiteral(ITVScriptingParser.RefLiteralContext context)
        {
            LiteralEvaluator baseVal = (LiteralEvaluator)VisitTypeLiteral(context.typeLiteral());
            return new LiteralExtensionEvaluator(baseVal, ExtensionType.Ref, context);
        }

        public override EvaluatorBase VisitElementList(ITVScriptingParser.ElementListContext context)
        {
            List<EvaluatorBase> elements = new List<EvaluatorBase>();
            foreach (ITVScriptingParser.SingleExpressionContext se in context.singleExpression())
            {
#if UseVisitSingleExpression
            ScriptValue tmp= VisitSingleExpression(se);
#else
                EvaluatorBase tmp = Visit(se);
#endif
                elements.Add(tmp);
            }


            return new SequenceEvaluator(elements, SequenceType.ExpressionSequence, context);
        }

        public override EvaluatorBase VisitArrayLiteral(ITVScriptingParser.ArrayLiteralContext context)
        {
            return Visit(context.elementList());
        }
    }
}
