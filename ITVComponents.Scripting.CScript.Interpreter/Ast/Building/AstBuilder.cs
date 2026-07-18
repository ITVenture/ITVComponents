using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Interpreter.Ast.Expressions;
using ITVComponents.Scripting.CScript.Operating;

namespace ITVComponents.Scripting.CScript.Interpreter.Ast.Building
{
    /// <summary>
    /// Baut aus dem ANTLR-Parsebaum den Ausfuehrungsbaum des Interpreters.
    /// </summary>
    /// <remarks>
    /// Der Builder laeuft genau einmal pro Quelltext; das Ergebnis ist unveraenderlich und
    /// wird zwischengespeichert. Was hier passiert, passiert also nicht zur Laufzeit.
    ///
    /// Portiert vom abgeloesten ExpressionExecutorBuilder. Uebernommen wurde dessen
    /// Konstrukt-Abdeckung, nicht seine Struktur: statt eines Datenbaums aus ScriptExecutor +
    /// IExecutorArgument entstehen direkt verhaltensreiche Knoten.
    ///
    /// Stand: Ausdruecke. Statements folgen in der naechsten Phase - die entsprechenden
    /// Visit-Methoden sind noch nicht ueberschrieben und laufen in NotSupported.
    /// </remarks>
    public class AstBuilder : ITVScriptingBaseVisitor<INode>
    {
        /// <summary>
        /// Baut den Ausdrucks-Knoten fuer einen Parsebaum.
        /// </summary>
        public IExpressionNode BuildExpression(IParseTree tree)
        {
            return Expression(tree);
        }

        /// <summary>
        /// Packt den Statement-Rahmen aus, in den der Parser auch einen einzelnen Ausdruck
        /// legt (ExpressionParser.GetRawExpressionTree liefert einen ExpressionStatementContext).
        /// </summary>
        public override INode VisitExpressionStatement(ITVScriptingParser.ExpressionStatementContext context)
        {
            return Visit(context.expressionSequence());
        }

        /// <summary>
        /// Eine Ausdrucksfolge in Ausdrucksposition. Mehrere durch Komma getrennte Ausdruecke
        /// sind hier nicht sinnvoll - erst als Argumentliste, und die wird anderswo gelesen.
        /// </summary>
        public override INode VisitExpressionSequence(ITVScriptingParser.ExpressionSequenceContext context)
        {
            var expressions = context.singleExpression();
            if (expressions.Length != 1)
            {
                throw new ScriptException(
                    $"Single expression expected at {Position(context).Line}/{Position(context).Column}");
            }

            return Visit(expressions[0]);
        }

        #region Operatoren

        public override INode VisitMultiplicativeExpression(
            ITVScriptingParser.MultiplicativeExpressionContext context)
        {
            BinaryOperator op;
            if (context.Multiply() != null)
            {
                op = BinaryOperator.Multiply;
            }
            else if (context.Divide() != null)
            {
                op = BinaryOperator.Divide;
            }
            else if (context.Modulus() != null)
            {
                op = BinaryOperator.Modulus;
            }
            else
            {
                throw Unexpected(context);
            }

            return Binary(context, context.singleExpression(), op);
        }

        public override INode VisitAdditiveExpression(ITVScriptingParser.AdditiveExpressionContext context)
        {
            BinaryOperator op;
            if (context.Plus() != null)
            {
                op = BinaryOperator.Add;
            }
            else if (context.Minus() != null)
            {
                op = BinaryOperator.Subtract;
            }
            else
            {
                throw Unexpected(context);
            }

            return Binary(context, context.singleExpression(), op);
        }

        public override INode VisitBitShiftExpression(ITVScriptingParser.BitShiftExpressionContext context)
        {
            BinaryOperator op;
            if (context.LeftShiftArithmetic() != null)
            {
                op = BinaryOperator.LeftShift;
            }
            else if (context.RightShiftArithmetic() != null)
            {
                op = BinaryOperator.RightShift;
            }
            else
            {
                throw Unexpected(context);
            }

            return Binary(context, context.singleExpression(), op);
        }

        public override INode VisitBitAndExpression(ITVScriptingParser.BitAndExpressionContext context)
        {
            return Binary(context, context.singleExpression(), BinaryOperator.And);
        }

        public override INode VisitBitOrExpression(ITVScriptingParser.BitOrExpressionContext context)
        {
            return Binary(context, context.singleExpression(), BinaryOperator.Or);
        }

        public override INode VisitBitXOrExpression(ITVScriptingParser.BitXOrExpressionContext context)
        {
            return Binary(context, context.singleExpression(), BinaryOperator.Xor);
        }

        public override INode VisitLogicalAndExpression(ITVScriptingParser.LogicalAndExpressionContext context)
        {
            var operands = context.singleExpression();
            return new LogicalNode(Position(context), Expression(operands[0]), Expression(operands[1]), true);
        }

        public override INode VisitLogicalOrExpression(ITVScriptingParser.LogicalOrExpressionContext context)
        {
            var operands = context.singleExpression();
            return new LogicalNode(Position(context), Expression(operands[0]), Expression(operands[1]), false);
        }

        public override INode VisitEqualityExpression(ITVScriptingParser.EqualityExpressionContext context)
        {
            // Der abgeloeste Builder behandelte hier jeden Operator ausser "==" als Ungleichheit.
            // Explizit pruefen, damit eine Grammatikerweiterung nicht still als != durchgeht.
            ComparisonType comparison;
            if (context.Equals() != null)
            {
                comparison = ComparisonType.Equal;
            }
            else if (context.NotEquals() != null)
            {
                comparison = ComparisonType.NotEqual;
            }
            else
            {
                throw Unexpected(context);
            }

            var operands = context.singleExpression();
            return new CompareNode(Position(context), Expression(operands[0]), Expression(operands[1]), comparison);
        }

        public override INode VisitRelationalExpression(ITVScriptingParser.RelationalExpressionContext context)
        {
            ComparisonType comparison;
            if (context.MoreThan() != null)
            {
                comparison = ComparisonType.GreaterThan;
            }
            else if (context.GreaterThanEquals() != null)
            {
                comparison = ComparisonType.GreaterThanOrEqual;
            }
            else if (context.LessThan() != null)
            {
                comparison = ComparisonType.LessThan;
            }
            else if (context.LessThanEquals() != null)
            {
                comparison = ComparisonType.LessThanOrEqual;
            }
            else
            {
                throw Unexpected(context);
            }

            var operands = context.singleExpression();
            return new CompareNode(Position(context), Expression(operands[0]), Expression(operands[1]), comparison);
        }

        public override INode VisitNotExpression(ITVScriptingParser.NotExpressionContext context)
        {
            return new UnaryOpNode(Position(context), Expression(context.singleExpression()), UnaryOperator.Not);
        }

        public override INode VisitUnaryMinusExpression(ITVScriptingParser.UnaryMinusExpressionContext context)
        {
            return new UnaryOpNode(Position(context), Expression(context.singleExpression()), UnaryOperator.Minus);
        }

        public override INode VisitUnaryPlusExpression(ITVScriptingParser.UnaryPlusExpressionContext context)
        {
            return new UnaryOpNode(Position(context), Expression(context.singleExpression()), UnaryOperator.Plus);
        }

        public override INode VisitBitNotExpression(ITVScriptingParser.BitNotExpressionContext context)
        {
            return new UnaryOpNode(Position(context), Expression(context.singleExpression()), UnaryOperator.Invert);
        }

        #endregion

        #region Zuweisungen

        public override INode VisitAssignmentExpression(ITVScriptingParser.AssignmentExpressionContext context)
        {
            var operands = context.singleExpression();
            return new AssignNode(Position(context), Expression(operands[0]), Expression(operands[1]));
        }

        public override INode VisitAssignmentOperatorExpression(
            ITVScriptingParser.AssignmentOperatorExpressionContext context)
        {
            string op = context.assignmentOperator().GetText();
            BinaryOperator compound;
            switch (op)
            {
                case "*=":
                    compound = BinaryOperator.Multiply;
                    break;
                case "/=":
                    compound = BinaryOperator.Divide;
                    break;
                case "%=":
                    compound = BinaryOperator.Modulus;
                    break;
                case "+=":
                    compound = BinaryOperator.Add;
                    break;
                case "-=":
                    compound = BinaryOperator.Subtract;
                    break;
                case "<<=":
                    compound = BinaryOperator.LeftShift;
                    break;
                case ">>=":
                    compound = BinaryOperator.RightShift;
                    break;
                case "&=":
                    compound = BinaryOperator.And;
                    break;
                case "^=":
                    compound = BinaryOperator.Xor;
                    break;
                case "|=":
                    compound = BinaryOperator.Or;
                    break;
                default:
                    // Der abgeloeste Builder liess einen unbekannten Operator still als
                    // BaseOperations.None durchgehen.
                    throw new ScriptException(
                        $"Unknown assignment-operator '{op}' at {Position(context).Line}/{Position(context).Column}");
            }

            var operands = context.singleExpression();
            return new AssignNode(Position(context), Expression(operands[0]), Expression(operands[1]), compound);
        }

        #endregion

        #region Zugriffe

        public override INode VisitIdentifierExpression(ITVScriptingParser.IdentifierExpressionContext context)
        {
            return new VariableNode(Position(context), context.Identifier().GetText());
        }

        public override INode VisitMemberDotExpression(ITVScriptingParser.MemberDotExpressionContext context)
        {
            return new MemberAccessNode(Position(context), Expression(context.singleExpression()),
                context.identifierName().GetText(), TypeHint(context.explicitTypeHint()));
        }

        public override INode VisitMemberDotQExpression(ITVScriptingParser.MemberDotQExpressionContext context)
        {
            return new MemberAccessNode(Position(context), Expression(context.singleExpression()),
                context.identifierName().GetText(), TypeHint(context.explicitTypeHint()), true);
        }

        public override INode VisitMemberIndexExpression(ITVScriptingParser.MemberIndexExpressionContext context)
        {
            return new IndexerNode(Position(context), Expression(context.singleExpression()),
                Expressions(context.expressionSequence()?.singleExpression()),
                TypeHint(context.explicitTypeHint()));
        }

        public override INode VisitArgumentsExpression(ITVScriptingParser.ArgumentsExpressionContext context)
        {
            return new CallNode(Position(context), Expression(context.singleExpression()),
                ArgumentNodes(context.arguments()), GenericArguments(context.typeArguments(), context),
                TypeHint(context.explicitTypeHint()));
        }

        public override INode VisitHasMemberExpression(ITVScriptingParser.HasMemberExpressionContext context)
        {
            var argumentContext = context.arguments();
            return new HasMemberNode(Position(context), Expression(context.singleExpression()),
                context.identifierName().GetText(),
                argumentContext == null ? null : ArgumentNodes(argumentContext),
                argumentContext == null ? null : GenericArguments(context.typeArguments(), context),
                TypeHint(context.explicitTypeHint()));
        }

        public override INode VisitMemberIsExpression(ITVScriptingParser.MemberIsExpressionContext context)
        {
            var operands = context.singleExpression();
            return new IsTypeNode(Position(context), Expression(operands[0]), Expression(operands[1]));
        }

        #endregion

        #region Bedingte Ausdruecke

        public override INode VisitTernaryExpression(ITVScriptingParser.TernaryExpressionContext context)
        {
            var operands = context.singleExpression();
            return new TernaryNode(Position(context), Expression(operands[0]), Expression(operands[1]),
                Expression(operands[2]));
        }

        public override INode VisitInstanceIsNullExpression(
            ITVScriptingParser.InstanceIsNullExpressionContext context)
        {
            var operands = context.singleExpression();
            return new CoalesceNode(Position(context), Expression(operands[0]), Expression(operands[1]));
        }

        public override INode VisitParenthesizedExpression(
            ITVScriptingParser.ParenthesizedExpressionContext context)
        {
            // Klammern beeinflussen nur die Struktur des Parsebaums, nicht die Auswertung.
            return Visit(context.singleExpression());
        }

        #endregion

        #region Literale

        public override INode VisitLiteralExpression(ITVScriptingParser.LiteralExpressionContext context)
        {
            return Visit(context.literal());
        }

        public override INode VisitLiteral(ITVScriptingParser.LiteralContext context)
        {
            IParseTree child = context.GetChild(0);
            switch (child)
            {
                case ITVScriptingParser.NumericLiteralContext numeric:
                    return Visit(numeric);
                case ITVScriptingParser.TypeLiteralContext typeLiteral:
                    return Visit(typeLiteral);
                case ITVScriptingParser.NullLiteralContext nullLiteral:
                    return Visit(nullLiteral);
                case ITVScriptingParser.BooleanLiteralContext boolean:
                    // Der abgeloeste Builder hatte hier kein return und ueberschrieb den
                    // Wahrheitswert anschliessend mit seiner Textform.
                    return new LiteralNode(Position(context),
                        boolean.GetText().Equals("true", StringComparison.OrdinalIgnoreCase));
                default:
                    return new LiteralNode(Position(context), StringHelper.Parse(child.GetText()));
            }
        }

        public override INode VisitNumericLiteral(ITVScriptingParser.NumericLiteralContext context)
        {
            if (context.DecimalLiteral() != null)
            {
                return new LiteralNode(Position(context),
                    OperationsHelper.ParseDecimalValue(context.DecimalLiteral().GetText()));
            }

            if (context.OctalIntegerLiteral() != null)
            {
                return new LiteralNode(Position(context),
                    Convert.ToInt32(context.OctalIntegerLiteral().GetText(), 8));
            }

            if (context.HexIntegerLiteral() != null)
            {
                return new LiteralNode(Position(context),
                    Convert.ToInt32(context.HexIntegerLiteral().GetText().Substring(2), 16));
            }

            throw Unexpected(context);
        }

        public override INode VisitNullLiteral(ITVScriptingParser.NullLiteralContext context)
        {
            var typeLiteral = context.typeLiteral();
            if (typeLiteral == null)
            {
                return new LiteralNode(Position(context), null);
            }

            return new TypedNullNode(Position(context), Expression(typeLiteral));
        }

        public override INode VisitTypeLiteral(ITVScriptingParser.TypeLiteralContext context)
        {
            string assembly = null;
            ITerminalNode path = context.StringLiteral();
            if (path != null)
            {
                assembly = StringHelper.Parse(path.GetText());
            }

            return TypeLiteralNode.Resolve(Position(context), context.typeLiteralIdentifier().GetText(), assembly,
                GenericArguments(context.typeArguments(), context));
        }

        public override INode VisitArrayLiteralExpression(
            ITVScriptingParser.ArrayLiteralExpressionContext context)
        {
            return Visit(context.arrayLiteral());
        }

        public override INode VisitArrayLiteral(ITVScriptingParser.ArrayLiteralContext context)
        {
            return new ArrayLiteralNode(Position(context),
                Expressions(context.elementList()?.singleExpression()));
        }

        #endregion

        #region Helfer

        private IExpressionNode Expression(IParseTree tree)
        {
            INode node = Visit(tree);
            if (node is IExpressionNode expression)
            {
                return expression;
            }

            throw new ScriptException($"Expression expected, got {node?.GetType().Name ?? "nothing"}");
        }

        private IReadOnlyList<IExpressionNode> Expressions(
            IEnumerable<ITVScriptingParser.SingleExpressionContext> contexts)
        {
            if (contexts == null)
            {
                return Array.Empty<IExpressionNode>();
            }

            return contexts.Select(Expression).ToArray();
        }

        private IExpressionNode Binary(ParserRuleContext context,
            ITVScriptingParser.SingleExpressionContext[] operands, BinaryOperator op)
        {
            return new BinaryOpNode(Position(context), Expression(operands[0]), Expression(operands[1]), op);
        }

        private IExpressionNode TypeHint(ITVScriptingParser.ExplicitTypeHintContext context)
        {
            if (context == null)
            {
                return null;
            }

            return Expression(context.typeIdentifier());
        }

        private IReadOnlyList<IExpressionNode> ArgumentNodes(ITVScriptingParser.ArgumentsContext context)
        {
            return Expressions(context?.argumentList()?.singleExpression());
        }

        /// <summary>
        /// Liest die generischen Argumente. Nur geschlossene Generics sind erlaubt.
        /// </summary>
        private IReadOnlyList<IExpressionNode> GenericArguments(
            ITVScriptingParser.TypeArgumentsContext context, ParserRuleContext owner)
        {
            if (context == null)
            {
                return null;
            }

            if (!(context is ITVScriptingParser.FinalGenericsContext finalGenerics))
            {
                throw new ScriptException(
                    $"Open Generic Arguments are not supported at {Position(owner).Line}/{Position(owner).Column}");
            }

            var typed = finalGenerics.typedArguments();
            if (typed == null)
            {
                return Array.Empty<IExpressionNode>();
            }

            return typed.typeIdentifier().Select(Expression).ToArray();
        }

        private static SourcePosition Position(ParserRuleContext context)
        {
            return SourcePosition.FromContext(context);
        }

        private ScriptException Unexpected(ParserRuleContext context)
        {
            SourcePosition position = Position(context);
            return new ScriptException(
                $"Unexpected expression '{context.GetText()}' at {position.Line}/{position.Column}");
        }

        #endregion
    }
}
