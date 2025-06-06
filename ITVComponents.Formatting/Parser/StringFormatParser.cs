using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Formatting.Elements;
using ITVComponents.Formatting.Parser.Model;
using ITVComponents.Formatting.Parser.TokenHandlers;
using ITVComponents.Helpers;
using ITVComponents.Scripting.CScript.Security;
using ITVComponents.StateMachine;
using ITVComponents.StateMachine.BaseTypes;
using ITVComponents.StateMachine.DefaultImplementations;
using ITVComponents.StateMachine.Extensions;
using ITVComponents.StateMachine.Models;
using ITVComponents.StateMachine.StatusTransit;
using FormattableString = ITVComponents.Formatting.Parser.Model.FormattableString;

namespace ITVComponents.Formatting.Parser
{
    public class StringFormatParser
    {
        private static TransducerMachine<ParserStateHandler, StringFormatParser> stateMachine;

        private IFormatElement currentElement;

        private List<IFormatElement> elements;

        private FormatElementAppendMode formatElementMode = FormatElementAppendMode.Content;

        private string[] doubles = ["[[", "]]", "$$", "££"];

        public StringFormatParser()
        {

            TransitionCollection<ParserStateHandler, StringFormatParser> statusCollection =
                new TransitionCollection<ParserStateHandler, StringFormatParser>()
                    .Transition("StringTokenHandler", "DefaultFormatExpressionTokenHandler", MakeStringTransitCallback(shifter => (shifter.Current == "[" && shifter.Pre1 != "[[")?1:-1), NextToken, "Default Expression-Start ('[')")
                    .Transition("StringTokenHandler", "BlockFormatExpressionTokenHandler", MakeStringTransitCallback(shifter => (shifter.Current == "$" && shifter.Pre1 == "$[" && shifter.Pre2 != "$[[")?2:-1), NextToken, "Block Expression ('$[')")
                    .Transition("StringTokenHandler", "RecursiveFormatExpressionTokenHandler", MakeStringTransitCallback(shifter => (shifter.Current == "£" && shifter.Pre1 == "£[" && shifter.Pre2 != "£[[")?2:-1), NextToken, "Recursive Expression ('£[')")
                    .Transition("StringTokenHandler", "RecursiveBlockFormatExpressionHandler", MakeStringTransitCallback(shifter => (shifter.Current == "$" && shifter.Pre1 == "$£" && shifter.Pre2 == "$£[" && shifter.Pre3 != "$£[[")?3:-1), NextToken, "Recursive Block Expression('$£[')")
                    .SubTransition("FormatExpressionTokenHandler", "ParenthesisBalanceTokenHandler", (args, handler, me, machine) => WithShifter(args, shifter => !shifter.Eof && shifter.Current == "("), description: "Opening Parenthesis('(')")
                    .SubTransition("FormatExpressionTokenHandler", "IndexerBalanceTokenHandler", (args, handler, me, machine) => WithShifter(args, shifter => !shifter.Eof && shifter.Current == "["), description: "Opening Indexer-Bracket('[')")
                    .SubTransition("FormatExpressionTokenHandler", "BracketBalanceTokenHandler", (args, handler, me, machine) => WithShifter(args, shifter => !shifter.Eof && shifter.Current == "{"), description: "Opening Curly-Bracket('{')")
                    .SubTransition("FormatExpressionTokenHandler", "TernaryBalanceTokenHandler", (args, handler, me, machine) => WithShifter(args, shifter => !shifter.Eof && shifter.Current == "?" && shifter.Pre1 != "??" && shifter.Pre1 != "?." && shifter.Pre1 != "?["), description: "Ternary Expression('?'...':')")
                    .SubTransition("FormatExpressionTokenHandler", "StringBalanceTokenHandler", (args, handler, me, machine) => WithShifter(args, shifter => !shifter.Eof && shifter.Current == "\""), description: "String literal('\"...\"')")
                    .SubTransition("FormatExpressionTokenHandler", "VerbatimStringBalanceTokenHandler", (args, handler, me, machine) => WithShifter(args, shifter => !shifter.Eof && shifter.Current == "@" && shifter.Pre1 == "@\""), async (a, b, c) => WithShifter(c, shifter => shifter.MoveNext()), description: "String literal('\"...\"')")
                    .SubTransition("FormatExpressionTokenHandler", "EofTokenHandler", (args, handler, me, machine) => WithShifter(args, shifter =>
                    {
                        var retVal = shifter.Eof;
                        if (retVal)
                        {
                            args.Set("UnExpected", true);
                        }

                        return retVal;
                    }), async (a, b, c) => WithShifter(c, shifter => shifter.MoveNext()), description: "String literal('\"...\"')")
                    .SubTransition("DefaultFormatExpressionTokenHandler", "FormatHintHandler", (args, handler, me, machine) => WithShifter(args, shifter =>
                    {
                        var retVal = !shifter.Eof && shifter.Current == ":";
                        if (retVal)
                        {
                            args.Set("AppendMode", FormatElementAppendMode.Format);
                        }

                        return retVal;
                    }), description: "Format hint(':...')")
                    .SubTransition("DefaultFormatExpressionTokenHandler", "FormatLengthHandler", (args, handler, me, machine) => WithShifter(args, shifter =>
                    {
                        var retVal = !shifter.Eof && shifter.Current == ",";
                        if (retVal)
                        {
                            args.Set("AppendMode", FormatElementAppendMode.Length);
                        }

                        return retVal;
                    }), description: "Format Length(',...')")
                    .SubTransition("FormatHintHandler", "StringBalanceTokenHandler", (args, handler, me, machine) => WithShifter(args, shifter => !shifter.Eof && shifter.Current == "\""), description: "String literal('\"...\"')")
                    .Transition("FormatLengthHandler", "FormatHintHandler", (args, handler, me, machine) => WithShifter(args, shifter =>
                    {
                        var retVal = !shifter.Eof && shifter.Current == ":";
                        if (retVal)
                        {
                            args.Set("AppendMode", FormatElementAppendMode.Format);
                        }

                        return retVal;
                    }), description: "Format hint(':...')")
                    .Transition("StringTokenHandler", "EofTokenHandler", (args, handler, me, machine) => WithShifter(args, shifter => shifter.Eof), NextToken, "<<end of string>>")
                    .Transition("RecursiveFormatExpressionTokenHandlerGroup", "StringTokenHandler", (args, handler, me, machine) => WithShifter(args, shifter => !shifter.Eof && shifter.Current == "]" && shifter.Pre1 != "]{"), NextToken, "Closing format-Tag")
                    .Transition("RecursiveFormatExpressionTokenHandlerGroup", "RecursionDepthTokenHandler", (args, handler, me, machine) => WithShifter(args, shifter =>
                    {
                        var retVal = !shifter.Eof && shifter.Current == "]" && shifter.Pre1 == "]{";
                        if (retVal)
                        {
                            shifter.MoveNext();
                        }

                        return retVal;
                    }), description:"Recursion-Depth")
                    .Transition("RecursionDepthTokenHandler", "StringTokenHandler", (args, handler, me, machine) => WithShifter(args, shifter => !shifter.Eof && shifter.Current == "}"), NextToken, "Closing format-Tag")
                    .Transition("NormalFormatExpressionTokenHandlerGroup", "StringTokenHandler", (args, handler, me, machine) => WithShifter(args, shifter => !shifter.Eof && shifter.Current == "]"), NextToken, "Closing format-Tag")
                    .GroupMember("FormatExpressionTokenHandler", "DefaultFormatExpressionTokenHandler")
                    .GroupMember("FormatExpressionTokenHandler", "BlockFormatExpressionTokenHandler")
                    .GroupMember("FormatExpressionTokenHandler", "RecursiveFormatExpressionTokenHandler")
                    .GroupMember("FormatExpressionTokenHandler", "RecursiveBlockFormatExpressionHandler")
                    .GroupMember("FormatExpressionTokenHandler", "ParenthesisBalanceTokenHandler")
                    .GroupMember("FormatExpressionTokenHandler", "IndexerBalanceTokenHandler")
                    .GroupMember("FormatExpressionTokenHandler", "BracketBalanceTokenHandler")
                    .GroupMember("FormatExpressionTokenHandler", "TernaryBalanceTokenHandler")
                    .GroupMember("FormatExpressionTokenHandler", "StringBalanceTokenHandler")
                    .GroupMember("FormatExpressionTokenHandler", "VerbatimStringBalanceTokenHandler")
                    .GroupMember("DefaultFormatExpressionTokenHandler", "BlockFormatExpressionTokenHandler")
                    .GroupMember("DefaultFormatExpressionTokenHandler", "RecursiveFormatExpressionTokenHandler")
                    .GroupMember("DefaultFormatExpressionTokenHandler", "RecursiveBlockFormatExpressionHandler")
                    .GroupMember("RecursiveFormatExpressionTokenHandlerGroup", "RecursiveFormatExpressionTokenHandler")
                    .GroupMember("RecursiveFormatExpressionTokenHandlerGroup", "RecursiveBlockFormatExpressionHandler")
                    .GroupMember("NormalFormatExpressionTokenHandlerGroup", "DefaultFormatExpressionTokenHandler")
                    .GroupMember("NormalFormatExpressionTokenHandlerGroup", "BlockFormatExpressionTokenHandler")
                ;

            stateMachine = new TransducerMachine<ParserStateHandler, StringFormatParser>(
                new DefaultStatusFactory<ParserStateHandler, StringFormatParser>(new Dictionary<string, Func<StringFormatParser, TransducerMachine<ParserStateHandler, StringFormatParser>, RunArguments, ParserStateHandler, ParserStateHandler>>
                {
                    {"StringTokenHandler", (parser, machine, args, prev) => new StringTokenHandler(parser)},
                    {"DefaultFormatExpressionTokenHandler", (parser, machine, args, prev) => new DefaultFormatExpressionTokenHandler(parser)},
                    {"BlockFormatExpressionTokenHandler", (parser, machine, args, prev) => new BlockFormatExpressionTokenHandler(parser)},
                    {"RecursiveFormatExpressionTokenHandler", (parser, machine, args, prev) => new RecursiveFormatExpressionTokenHandler(parser)},
                    {"RecursiveBlockFormatExpressionHandler", (parser, machine, args, prev) => new RecursiveBlockFormatExpressionHandler(parser)},
                    {"ParenthesisBalanceTokenHandler", (parser, machine, args, prev) => new BalanceTokenHandler(parser, prev,"(",")")},
                    {"IndexerBalanceTokenHandler", (parser, machine, args, prev) => new BalanceTokenHandler(parser, prev,"[","]")},
                    {"BracketBalanceTokenHandler", (parser, machine, args, prev) => new BalanceTokenHandler(parser, prev, "{", "}")},
                    {"TernaryBalanceTokenHandler", (parser, machine, args, prev) => new BalanceTokenHandler(parser, prev, "?", ":")},
                    {"StringBalanceTokenHandler", (parser, machine, args, prev) => new StringBalanceTokenHandler(parser, prev)},
                    {"VerbatimStringBalanceTokenHandler", (parser, machine, args, prev) => new VerbatimStringBalanceTokenHandler(parser, prev)},
                    {"FormatHintHandler", (parser, machine, args, prev) => new FormatHintHandler(parser, prev, args.Argument<FormatElementAppendMode>("AppendMode",true))},
                    {"FormatLengthHandler", (parser, machine, args, prev) => new FormatHintHandler(parser, prev, args.Argument<FormatElementAppendMode>("AppendMode",true))},
                    {"RecursionDepthTokenHandler", (parser,machine, args, prev) => new RecursionDepthHandler(parser)},
                    {"EofTokenHandler", (parser, machine, args, prev) => new EofTokenHandler(parser, args.Argument<bool>("UnExpected",true)) }
                }), statusCollection, "StringTokenHandler", this);
            currentElement = new StringElement();
            elements = new List<IFormatElement>();
        }

        public FormattableString FormatString(object target, string format,
            CustomExpressionParse customExpressionParser, Func<string, string, string, object> argumentsCallback,
            ScriptingPolicy policy = null)
        {
            var tokens = TokenizeString(format);
            StringBuilder rst = new StringBuilder();
            List<CodeElement> codes = new();
            var i = 0;
            foreach (var elem in elements)
            {
                if (elem is FormatElement fem)
                {
                    if (fem.Content.Length != 0)
                    {
                        bool keep = TryCustomParse(fem.Content.ToString(), customExpressionParser, out var tx);
                        if (keep)
                        {
                            var custFormat = fem.FormatHint.Length != 0
                                ? TextFormat.GetCustomFormatter(fem.FormatHint.ToString())
                                : null;
                            rst.AppendFormat("{{{0}", i);
                            if (fem.FormatLength.Length != 0)
                            {
                                rst.AppendFormat(",{0}", fem.FormatLength);
                            }

                            if (fem.FormatHint.Length != 0 && custFormat == null)
                            {
                                rst.AppendFormat(":{0}", fem.FormatHint);
                            }

                            int dp = fem.RecursionDepth;
                            if (fem.RecursionDepthString.Length != 0)
                            {
                                dp = int.Parse(fem.RecursionDepthString.ToString());
                            }
                            rst.Append("}");
                            i++;
                            codes.Add(new CodeElement
                            {
                                Code = tx,
                                IsBlock = fem.CodeType == CodeType.Block || fem.CodeType == CodeType.RecursiveBlock,
                                RecursionDepth = fem.IsRecursive ? dp : 0,
                                CustomFormatter = custFormat
                            });
                        }
                        else
                        {
                            string pref = "";
                            switch (fem.CodeType)
                            {
                                case CodeType.Block:
                                    pref = "$";
                                    break;
                                case CodeType.RecursiveExpression:
                                    pref = "£";
                                    break;
                                case CodeType.RecursiveBlock:
                                    pref = "$£";
                                    break;
                            }

                            rst.AppendFormat($"{pref}[{fem.Content}]");
                            if (fem.IsRecursive && fem.RecursionDepth != 1)
                            {
                                rst.Append($"{{{fem.RecursionDepth}}}");
                            }
                        }
                    }
                }
                else
                {
                    rst.Append(elem.Content);
                }
            }

            var retVal= new FormattableString(rst.ToString(), codes.ToArray(), policy, argumentsCallback);
            retVal.Bind(target);
            return retVal;
        }

        private bool TryCustomParse(string orig, CustomExpressionParse customExpressionParser, out string ret)
        {
            if (customExpressionParser == null)
            {
                ret = orig;
                return true;
            }

            return customExpressionParser(orig, out ret);
        }

        /*
         bool keep = customExpressionParser(fmt.Content.ToString(), out var newContent);
           if (!keep)
           {
               string pref = "";
               switch (fmt.CodeType)
               {
                   case CodeType.Block:
                       pref = "$";
                       break;
                   case CodeType.RecursiveExpression:
                       pref = "£";
                       break;
                   case CodeType.RecursiveBlock:
                       pref = "$£";
                       break;
               }
           
               tmp[i] = new StringElement { Length = fmt.Length, Start = fmt.Start };
               tmp[i].Content.Append($"{pref}[{fmt.Content}]");
               if (fmt.IsRecursive && fmt.RecursionDepth != 1)
               {
                   tmp[1].Content.Append($"{{{fmt.RecursionDepth}}}");
               }
           }
           else
           {
               fmt.Content.Clear();
               fmt.Content.Append(newContent);
           }
         */

        public IFormatElement[] TokenizeString(string source)
        {
            stateMachine.Reset("StringTokenHandler");
            var shifter = new StringShifter(source);
            var arguments = new RunArguments(d => d["shifter"] = shifter);
            currentElement = new StringElement();
            elements.Clear();
            while (stateMachine.Status is not EofTokenHandler)
            {
                stateMachine.Execute(arguments);
                if (stateMachine.Status is not EofTokenHandler)
                {
                    shifter.MoveNext();
                }
            }

            return elements.ToArray();
        }

        public void Append(string currentShifterCharacter)
        {
            if (formatElementMode == FormatElementAppendMode.Content)
            {
                currentElement.Content.Append(currentShifterCharacter);
            }
            else if (formatElementMode == FormatElementAppendMode.Format)
            {
                ((FormatElement)currentElement).FormatHint.Append(currentShifterCharacter);
            }
            else if (formatElementMode == FormatElementAppendMode.Length)
            {
                ((FormatElement)currentElement).FormatLength.Append(currentShifterCharacter);
            }
            else if (formatElementMode == FormatElementAppendMode.RecursionDepth)
            {
                ((FormatElement)currentElement).RecursionDepthString.Append(currentShifterCharacter);
            }
        }

        private static Task NextToken(ParserStateHandler status, StringFormatParser t, RunArguments arguments)
        {
            if (t.currentElement.Content.Length != 0 && stateMachine.CurrentDepth == 0)
            {
                t.elements.Add(t.currentElement);
            }

            t.NextElement(status);
            return Task.CompletedTask;
        }

        private Func<RunArguments, Status<ParserStateHandler, StringFormatParser>, StringFormatParser, TransducerMachine<ParserStateHandler, StringFormatParser>, bool> MakeStringTransitCallback(Func<StringShifter, int> charSearch)
        {
            var stringTransCondition =
            (RunArguments args, Status<ParserStateHandler, StringFormatParser> handler, StringFormatParser me,
                TransducerMachine<ParserStateHandler, StringFormatParser> machine) => WithShifter(args, shifter =>
            {
                var ln = charSearch(shifter);
                var retVal = !shifter.Eof && ln != -1 && !doubles.Contains(shifter.Pre1);
                if (!retVal)
                {
                    args.Set("HasDoubles", doubles.Contains(shifter.Pre1));
                }
                else
                {
                    args.Set("ShiftLn", ln - 1);
                }

                return retVal;
            });
            return stringTransCondition;
        }

        private T WithShifter<T>(RunArguments arguments, Func<StringShifter, T> fx)
        {
            var shifter = arguments.Argument<StringShifter>("shifter");
            return fx(shifter);
        }

        private void NextElement(ParserStateHandler status)
        {
            if (status is StringTokenHandler)
            {
                formatElementMode = FormatElementAppendMode.Content;
                currentElement = new StringElement();
            }
            else if (status is FormatExpressionTokenHandler)
            {
                formatElementMode = FormatElementAppendMode.Content;
                var fom = new FormatElement(); 
                currentElement =fom;
                if (status is BlockFormatExpressionTokenHandler || status is RecursiveBlockFormatExpressionHandler)
                {
                    fom.CodeType = CodeType.Block;
                }

                if (status is RecursiveBlockFormatExpressionHandler || status is RecursiveFormatExpressionTokenHandler)
                {
                    fom.CodeType |= CodeType.RecursiveExpression;
                    fom.IsRecursive = true;
                }
            }
        }

        internal void SwitchState(FormatElementAppendMode newElementMode)
        {
            if (formatElementMode == FormatElementAppendMode.Format && newElementMode == FormatElementAppendMode.Length)
            {
                throw new InvalidOperationException("Invalid Format-element ordering.");
            }

            formatElementMode = newElementMode;
        }
    }

    internal enum FormatElementAppendMode
    {
        Content,
        Format,
        Length,
        RecursionDepth
    }
}
