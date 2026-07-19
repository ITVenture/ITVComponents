using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Interpreter.Ast.Expressions;
using ITVComponents.Scripting.CScript.Interpreter.Ast.Statements;
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
        /// Wie tief der gerade gebaute Knoten in Schleifen beziehungsweise switch-Bloecken
        /// steckt, und ob return erlaubt ist.
        /// </summary>
        /// <remarks>
        /// Damit wird schon beim Bauen entschieden, ob break, continue und return an ihrer
        /// Stelle zulaessig sind. Der ScriptVisitor musste das zur Laufzeit ueber die
        /// Instanzfelder loopJumpAllowed, catching und returnSupported pruefen, weil er keine
        /// Bauphase hat - ein falsch platziertes break fiel dort erst auf, wenn es ausgefuehrt
        /// wurde, und dann als nicht fangbarer Fehler.
        /// </remarks>
        /// <summary>
        /// Das kuenstliche Member, das den Typ liefert, fuer den ein Ausdruck steht.
        /// </summary>
        private const string TypeMemberName = "$Type";

        private int loopDepth;
        private int switchDepth;
        private bool returnAllowed = true;
        private bool inCatch;

        /// <summary>
        /// Baut den Ausdrucks-Knoten fuer einen Parsebaum.
        /// </summary>
        public IExpressionNode BuildExpression(IParseTree tree)
        {
            // ExpressionParser.GetRawExpressionTree liefert auch fuer einen einzelnen Ausdruck
            // einen Statement-Rahmen; der wird hier ausgepackt.
            if (tree is ITVScriptingParser.ExpressionStatementContext statement)
            {
                return SingleExpression(statement.expressionSequence());
            }

            return Expression(tree);
        }

        /// <summary>
        /// Baut den Anweisungs-Knoten fuer ein ganzes Programm.
        /// </summary>
        public IStatementNode BuildProgram(ITVScriptingParser.ProgramContext tree)
        {
            return Statement(tree.sourceElements());
        }

        #region Programm und Anweisungsfolgen

        public override INode VisitProgram(ITVScriptingParser.ProgramContext context)
        {
            return Visit(context.sourceElements());
        }

        public override INode VisitSourceElements(ITVScriptingParser.SourceElementsContext context)
        {
            // Kein eigener Scope: die Wurzel laeuft im Scope, den der Aufrufer mitgibt.
            return new BlockNode(Position(context), Statements(context.sourceElement()), false);
        }

        public override INode VisitStatementList(ITVScriptingParser.StatementListContext context)
        {
            return new BlockNode(Position(context), Statements(context.statement()), false);
        }

        public override INode VisitBlock(ITVScriptingParser.BlockContext context)
        {
            return BuildBlock(context, true);
        }

        public override INode VisitEmptyStatement(ITVScriptingParser.EmptyStatementContext context)
        {
            return new EmptyStatementNode(Position(context));
        }

        public override INode VisitExpressionStatement(ITVScriptingParser.ExpressionStatementContext context)
        {
            return new ExpressionStatementNode(Position(context),
                SingleExpression(context.expressionSequence()));
        }

        #endregion

        #region Verzweigungen und Schleifen

        public override INode VisitIfStatement(ITVScriptingParser.IfStatementContext context)
        {
            var branches = context.statement();
            return new IfNode(Position(context), Expression(context.singleExpression()),
                Statement(branches[0]), branches.Length > 1 ? Statement(branches[1]) : null);
        }

        public override INode VisitWhileStatement(ITVScriptingParser.WhileStatementContext context)
        {
            IExpressionNode condition = Expression(context.singleExpression());
            return new WhileNode(Position(context), condition, LoopBody(context.statement(), true));
        }

        public override INode VisitDoStatement(ITVScriptingParser.DoStatementContext context)
        {
            IStatementNode body = LoopBody(context.statement(), true);
            return new DoWhileNode(Position(context), Expression(context.singleExpression()), body);
        }

        public override INode VisitForStatement(ITVScriptingParser.ForStatementContext context)
        {
            // Die Grammatik fuehrt alle drei Kopfteile als optional. Der abgeloeste Builder
            // verlangte alle drei und lehnte for(;;) zur Laufzeit ab.
            IReadOnlyList<IExpressionNode> initializer = HeaderPart(context, 0);
            IReadOnlyList<IExpressionNode> condition = HeaderPart(context, 1);
            IReadOnlyList<IExpressionNode> iterator = HeaderPart(context, 2);

            // Kopf und Rumpf teilen den Scope, den ForNode oeffnet.
            return new ForNode(Position(context), initializer, condition, iterator,
                LoopBody(context.statement(), false));
        }

        public override INode VisitForInStatement(ITVScriptingParser.ForInStatementContext context)
        {
            var expressions = context.singleExpression();
            return new ForEachNode(Position(context), Expression(expressions[0]), Expression(expressions[1]),
                LoopBody(context.statement(), false));
        }

        public override INode VisitContinueStatement(ITVScriptingParser.ContinueStatementContext context)
        {
            if (loopDepth == 0 && switchDepth == 0)
            {
                throw new ScriptException(
                    $"Invalid usage of Continue found at {Position(context).Line}/{Position(context).Column}");
            }

            return new ContinueNode(Position(context));
        }

        public override INode VisitBreakStatement(ITVScriptingParser.BreakStatementContext context)
        {
            if (loopDepth == 0 && switchDepth == 0)
            {
                throw new ScriptException(
                    $"Invalid usage of Break found at {Position(context).Line}/{Position(context).Column}");
            }

            return new BreakNode(Position(context));
        }

        public override INode VisitReturnStatement(ITVScriptingParser.ReturnStatementContext context)
        {
            if (!returnAllowed)
            {
                throw new ScriptException(
                    $"Invalid usage of Return found at {Position(context).Line}/{Position(context).Column}");
            }

            var value = context.singleExpression();
            return new ReturnNode(Position(context), value == null ? null : Expression(value));
        }

        #endregion

        #region switch

        public override INode VisitSwitchStatement(ITVScriptingParser.SwitchStatementContext context)
        {
            IExpressionNode value = Expression(context.singleExpression());
            var caseBlock = context.caseBlock();

            switchDepth++;
            try
            {
                var clauses = new List<CaseClause>();
                var caseClauses = caseBlock.caseClauses();
                if (caseClauses != null)
                {
                    foreach (var clause in caseClauses.caseClause())
                    {
                        // Ein leerer Rumpf ist zulaessig und faellt in die naechste Klausel
                        // durch - der uebliche Mehrfach-Label-Fall.
                        var list = clause.statementList();
                        clauses.Add(new CaseClause(Expression(clause.singleExpression()),
                            list == null ? null : Statement(list)));
                    }
                }

                var defaultClause = caseBlock.defaultClause();
                IStatementNode defaultBody = null;
                if (defaultClause != null)
                {
                    var list = defaultClause.statementList();
                    defaultBody = list == null ? null : Statement(list);
                }

                return new SwitchNode(Position(context), value, clauses, defaultBody);
            }
            finally
            {
                switchDepth--;
            }
        }

        #endregion

        #region try

        public override INode VisitTryStatement(ITVScriptingParser.TryStatementContext context)
        {
            IStatementNode tryBlock = BuildBlock(context.block(), true);

            var catchProduction = context.catchProduction();
            string exceptionVariable = null;
            IStatementNode catchBlock = null;
            if (catchProduction != null)
            {
                exceptionVariable = catchProduction.Identifier().GetText();
                bool wasInCatch = inCatch;
                inCatch = true;
                try
                {
                    // Der catch-Rumpf teilt den Scope mit der Ausnahmevariablen, die TryNode
                    // dort ablegt - deshalb ohne eigenen Scope.
                    catchBlock = BuildBlock(catchProduction.block(), false);
                }
                finally
                {
                    inCatch = wasInCatch;
                }
            }

            var finallyProduction = context.finallyProduction();
            IStatementNode finallyBlock = null;
            if (finallyProduction != null)
            {
                // Aus einem finally-Block heraus darf der Kontrollfluss nicht abrupt
                // verlassen werden - sonst verschwaende er das Ergebnis, das er schuetzen soll.
                int loops = loopDepth;
                int switches = switchDepth;
                bool returns = returnAllowed;
                loopDepth = 0;
                switchDepth = 0;
                returnAllowed = false;
                try
                {
                    finallyBlock = BuildBlock(finallyProduction.block(), true);
                }
                finally
                {
                    loopDepth = loops;
                    switchDepth = switches;
                    returnAllowed = returns;
                }
            }

            return new TryNode(Position(context), tryBlock, exceptionVariable, catchBlock, finallyBlock);
        }

        public override INode VisitThrowStatement(ITVScriptingParser.ThrowStatementContext context)
        {
            var value = context.singleExpression();
            if (value == null && !inCatch)
            {
                throw new ScriptException("Illegal Re-Throw statement found!");
            }

            return new ThrowNode(Position(context), value == null ? null : Expression(value));
        }

        #endregion

        /// <summary>
        /// Eine Ausdrucksfolge in Ausdrucksposition. Mehrere durch Komma getrennte Ausdruecke
        /// sind hier nicht sinnvoll - erst als Argumentliste, und die wird anderswo gelesen.
        /// </summary>
        private IExpressionNode SingleExpression(ITVScriptingParser.ExpressionSequenceContext context)
        {
            var expressions = context.singleExpression();
            if (expressions.Length != 1)
            {
                throw new ScriptException(
                    $"Single expression expected at {Position(context).Line}/{Position(context).Column}");
            }

            return Expression(expressions[0]);
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

        public override INode VisitPreIncrementExpression(ITVScriptingParser.PreIncrementExpressionContext context)
        {
            return new IncrementNode(Position(context), Expression(context.singleExpression()),
                IncrementType.PreIncrement);
        }

        public override INode VisitPostIncrementExpression(
            ITVScriptingParser.PostIncrementExpressionContext context)
        {
            return new IncrementNode(Position(context), Expression(context.singleExpression()),
                IncrementType.PostIncrement);
        }

        public override INode VisitPreDecreaseExpression(ITVScriptingParser.PreDecreaseExpressionContext context)
        {
            return new IncrementNode(Position(context), Expression(context.singleExpression()),
                IncrementType.PreDecrement);
        }

        public override INode VisitPostDecreaseExpression(
            ITVScriptingParser.PostDecreaseExpressionContext context)
        {
            return new IncrementNode(Position(context), Expression(context.singleExpression()),
                IncrementType.PostDecrement);
        }

        #endregion

        #region Instanzerzeugung

        public override INode VisitNewExpression(ITVScriptingParser.NewExpressionContext context)
        {
            return new NewNode(Position(context), Expression(context.singleExpression()),
                ArgumentNodes(context.arguments()), GenericArguments(context.typeArguments(), context),
                Initializer(context.objectLiteral()));
        }

        public override INode VisitNewImplicitInit(ITVScriptingParser.NewImplicitInitContext context)
        {
            // Ohne Argumentliste: der Standardkonstruktor, danach der Initialisierer.
            return new NewNode(Position(context), Expression(context.singleExpression()),
                Array.Empty<IExpressionNode>(), GenericArguments(context.typeArguments(), context),
                Initializer(context.objectLiteral()));
        }

        private ObjectLiteralNode Initializer(ITVScriptingParser.ObjectLiteralContext context)
        {
            return context == null ? null : (ObjectLiteralNode)Visit(context);
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
            return MemberAccess(context, context.singleExpression(), context.identifierName().GetText(),
                context.explicitTypeHint(), false);
        }

        public override INode VisitMemberDotQExpression(ITVScriptingParser.MemberDotQExpressionContext context)
        {
            return MemberAccess(context, context.singleExpression(), context.identifierName().GetText(),
                context.explicitTypeHint(), true);
        }

        /// <summary>
        /// Baut einen Memberzugriff und faltet dabei $Type in den folgenden Zugriff ein.
        /// </summary>
        /// <remarks>
        /// $Type ist kein gewoehnliches Member, sondern ein Wechsel der Auswertungsstrategie.
        /// Der Erbauer setzt ihn hier um: "x.$Type" wird zu einem TypeOfNode, und der Zugriff,
        /// der darauf folgt, wird fest auf Instanz-Ebene geschaltet. Damit steht die
        /// Entscheidung im Baum statt in einer Fallunterscheidung zur Laufzeit, und ein
        /// gleichnamiges statisches Member kann nicht mehr dazwischenkommen.
        /// </remarks>
        private INode MemberAccess(ParserRuleContext context,
            ITVScriptingParser.SingleExpressionContext targetContext, string memberName,
            ITVScriptingParser.ExplicitTypeHintContext typeHint, bool nullPropagating)
        {
            IExpressionNode target = Expression(targetContext);
            if (memberName == TypeMemberName)
            {
                return new TypeOfNode(Position(context), target);
            }

            return new MemberAccessNode(Position(context), target, memberName, TypeHint(typeHint),
                nullPropagating, target is TypeOfNode);
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
                case ITVScriptingParser.RefLiteralContext refLiteral:
                    return Visit(refLiteral);
                default:
                    string text = StringHelper.Parse(child.GetText());

                    // Manche Zeichenketten sind Ausfuehrungsschalter. Erkannt wird das hier
                    // einmalig; gesetzt wird der Schalter erst beim Auswerten.
                    return (INode)PragmaNode.TryCreate(Position(context), text)
                           ?? new LiteralNode(Position(context), text);
            }
        }

        public override INode VisitRefLiteral(ITVScriptingParser.RefLiteralContext context)
        {
            return new RefLiteralNode(Position(context), Expression(context.typeLiteral()));
        }

        public override INode VisitTypeIdentifier(ITVScriptingParser.TypeIdentifierContext context)
        {
            string[] path = context.Identifier().Select(i => i.GetText()).ToArray();
            return new TypeIdentifierNode(Position(context), path);
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

        #region Funktionen und Objekt-Literale

        public override INode VisitFunctionDeclaration(ITVScriptingParser.FunctionDeclarationContext context)
        {
            return BuildFunction(context, context.Identifier()?.GetText(), context.formalParameterList(),
                context.functionBody());
        }

        public override INode VisitFunctionExpression(ITVScriptingParser.FunctionExpressionContext context)
        {
            return BuildFunction(context, context.Identifier()?.GetText(), context.formalParameterList(),
                context.functionBody());
        }

        public override INode VisitObjectLiteralExpression(
            ITVScriptingParser.ObjectLiteralExpressionContext context)
        {
            return Visit(context.objectLiteral());
        }

        public override INode VisitObjectLiteral(ITVScriptingParser.ObjectLiteralContext context)
        {
            var properties = new List<KeyValuePair<string, IExpressionNode>>();
            var assignments = context.propertyNameAndValueList()?.propertyAssignment();
            if (assignments != null)
            {
                foreach (var assignment in assignments)
                {
                    // Die Grammatik fuehrt propertyAssignment als eigene Regel mit mehreren
                    // Alternativen. Der abgeloeste Builder typisierte die Schleifenvariable
                    // hart auf die Ausdrucks-Alternative und waere bei jeder anderen mit einer
                    // InvalidCastException gescheitert.
                    if (!(assignment is ITVScriptingParser.PropertyExpressionAssignmentContext property))
                    {
                        throw new ScriptException(
                            $"Unsupported property-assignment '{assignment.GetText()}' at " +
                            $"{Position(assignment).Line}/{Position(assignment).Column}");
                    }

                    properties.Add(new KeyValuePair<string, IExpressionNode>(
                        property.identifierName().GetText(), Expression(property.singleExpression())));
                }
            }

            return new ObjectLiteralNode(Position(context), properties);
        }

        /// <summary>
        /// Baut eine Funktionsdefinition. Der Rumpf bekommt einen frischen Gueltigkeitsrahmen:
        /// return ist darin erlaubt, break und continue der umgebenden Schleife dagegen nicht.
        /// </summary>
        private INode BuildFunction(ParserRuleContext context, string name,
            ITVScriptingParser.FormalParameterListContext parameterList,
            ITVScriptingParser.FunctionBodyContext bodyContext)
        {
            string[] parameters = parameterList?.Identifier()?.Select(p => p.GetText()).ToArray()
                                  ?? Array.Empty<string>();

            int loops = loopDepth;
            int switches = switchDepth;
            bool returns = returnAllowed;
            bool catching = inCatch;
            loopDepth = 0;
            switchDepth = 0;
            returnAllowed = true;
            inCatch = false;
            try
            {
                var elements = bodyContext?.sourceElements();
                IStatementNode body = elements == null
                    ? new BlockNode(Position(context), Array.Empty<IStatementNode>(), false)
                    : Statement(elements);
                return new FunctionNode(Position(context), name, parameters, body);
            }
            finally
            {
                loopDepth = loops;
                switchDepth = switches;
                returnAllowed = returns;
                inCatch = catching;
            }
        }

        #endregion

        #region Native Scripts

        public override INode VisitNativeExpression(ITVScriptingParser.NativeExpressionContext context)
        {
            var expressions = context.singleExpression();
            var identifier = context.Identifier();

            // identifier[0] ist der Name, unter dem das Ziel im C#-Code sichtbar wird
            // ("as hicks"), identifier[1] die native Konfiguration ("-> DUMMY").
            return new NativeExpressionNode(Position(context), Expression(expressions[0]),
                Expression(expressions[1]), Expression(expressions[2]),
                identifier[0].GetText(), identifier[1].GetText());
        }

        public override INode VisitNativeLiteralExpression(
            ITVScriptingParser.NativeLiteralExpressionContext context)
        {
            // Der Codeblock ist von @# und # umschlossen; beides gehoert nicht zum Code.
            string raw = context.NativeCodeLiteral().GetText();
            string code = raw.Substring(2, raw.Length - 3);

            return new NativeLiteralNode(Position(context), code,
                Expression(context.singleExpression()), context.Identifier().GetText());
        }

        public override INode VisitNativeReference(ITVScriptingParser.NativeReferenceContext context)
        {
            return new NativeReferenceNode(Position(context), context.Identifier().GetText(),
                StringHelper.Parse(context.StringLiteral().GetText()));
        }

        public override INode VisitNativeUsing(ITVScriptingParser.NativeUsingContext context)
        {
            return new NativeUsingNode(Position(context), context.Identifier().GetText(),
                StringHelper.Parse(context.StringLiteral().GetText()));
        }

        #endregion

        #region Helfer

        private IStatementNode Statement(IParseTree tree)
        {
            INode node = Visit(tree);
            if (node is IStatementNode statement)
            {
                return statement;
            }

            // Ein Ausdruck in Anweisungsposition, den die Grammatik nicht als
            // expressionStatement fuehrt.
            if (node is IExpressionNode expression)
            {
                return new ExpressionStatementNode(expression.Position, expression);
            }

            throw new ScriptException($"Statement expected, got {node?.GetType().Name ?? "nothing"}");
        }

        private IReadOnlyList<IStatementNode> Statements(IEnumerable<IParseTree> trees)
        {
            return trees.Select(Statement).ToArray();
        }

        private BlockNode BuildBlock(ITVScriptingParser.BlockContext context, bool ownsScope)
        {
            var list = context.statementList();
            IReadOnlyList<IStatementNode> statements = list == null
                ? Array.Empty<IStatementNode>()
                : Statements(list.statement());
            return new BlockNode(Position(context), statements, ownsScope);
        }

        /// <summary>
        /// Baut den Rumpf einer Schleife und zaehlt dabei die Schleifentiefe hoch, damit break
        /// und continue darin zulaessig sind.
        /// </summary>
        /// <param name="ownsScope">
        /// ob der Rumpf einen eigenen Scope oeffnet. Bei for und foreach nicht - dort oeffnet
        /// ihn der Schleifenknoten, damit Kopf und Rumpf sich denselben teilen.
        /// </param>
        private IStatementNode LoopBody(ITVScriptingParser.StatementContext context, bool ownsScope)
        {
            loopDepth++;
            try
            {
                var block = context.block();
                if (block != null)
                {
                    return BuildBlock(block, ownsScope);
                }

                return Statement(context);
            }
            finally
            {
                loopDepth--;
            }
        }

        /// <summary>
        /// Liest einen der drei Teile eines for-Kopfes. Fehlt er, ist die Liste leer.
        /// </summary>
        /// <remarks>
        /// Die Zuordnung laeuft ueber die Position der Semikola, nicht ueber den Index im
        /// Array: expressionSequence() liefert nur die tatsaechlich vorhandenen Teile, sodass
        /// bei "for(;i&lt;n;i++)" das erste Array-Element die Bedingung waere und nicht die
        /// Initialisierung.
        /// </remarks>
        private IReadOnlyList<IExpressionNode> HeaderPart(ITVScriptingParser.ForStatementContext context,
            int slot)
        {
            var sequences = context.expressionSequence();
            var semicolons = context.SemiColon();
            if (sequences == null || sequences.Length == 0 || semicolons == null || semicolons.Length < 2)
            {
                return Array.Empty<IExpressionNode>();
            }

            int firstSemicolon = semicolons[0].Symbol.TokenIndex;
            int secondSemicolon = semicolons[1].Symbol.TokenIndex;

            foreach (var sequence in sequences)
            {
                int token = sequence.Start.TokenIndex;
                int actualSlot = token < firstSemicolon ? 0 : token < secondSemicolon ? 1 : 2;
                if (actualSlot == slot)
                {
                    return Expressions(sequence.singleExpression());
                }
            }

            return Array.Empty<IExpressionNode>();
        }

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
