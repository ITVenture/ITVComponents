using ITVComponents.Formatting;
using ITVComponents.Formatting.Parser;
using ITVComponents.StateMachine;
using ITVComponents.StateMachine.BaseTypes;
using ITVComponents.StateMachine.DefaultImplementations;
using ITVComponents.StateMachine.Extensions;
using ITVComponents.StateMachine.Models;
using ITVComponents.StateMachine.StatusTransit;
using ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Elements;
using ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Handlers;
using ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Handlers.Impl;
using ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Helpers;
using ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Model;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers
{
    internal class FilterLexer
    {
        private static readonly string[] JsOperatorsInt =
        [
            "eq", "neq", "gt", "gteq", "lt", "lteq", "ct",
            "ctn", "sw", "snw", "ew", "enw", "bt", "nbt", "inl",
            "innl", "iey", "iney"
        ];

        public static IReadOnlyCollection<string> JsOperators { get; } = JsOperatorsInt.AsReadOnly();

        public static IReadOnlyCollection<string> JsOneOpOperators { get; } = [.. JsOperatorsInt.Take(12)];

        public static IReadOnlyCollection<string> JsTwoOpOperators { get; } = [.. JsOperatorsInt.Skip(12).Take(2)];

        public static IReadOnlyCollection<string> JsNoOpPerators { get; } = [.. JsOperators.Skip(14).Take(4)];


        private TransducerMachine<FilterLexerStateHandler, FilterLexer> stateMachine;

        private IFilterElement currentElement;

        private List<IFilterElement> elements = new List<IFilterElement>();

        private FilterElementMode formatElementMode = FilterElementMode.Content;

        private string[] doubles = ["~~", "((", "))"];

        //private Stack<>

        private bool isLocked = false;


        public FilterLexer()
        {
            TransitionCollection<FilterLexerStateHandler, FilterLexer> statusCollection =
                    new TransitionCollection<FilterLexerStateHandler, FilterLexer>()
                        .Transition("MemberHandler", "OperationHandler",
                            MakeStringTransitCallback(shifter =>
                                (shifter.Current == "~" && shifter.Pre1 != "~~") ? 1 : -1),
                            NextToken, "Property-Operator")
                        .Transition("OperationHandler", "ValueHandler",
                            MakeStringTransitCallback((rv,shifter) =>
                                ((currentElement is MemberElement{IsFinal:false}) && shifter.Current == "~" && shifter.Pre1 != "~~") ? 1 : -1),
                            NextToken, "Property-Search-Value")
                        .Transition("ValueHandler", "Value2Handler",
                            MakeStringTransitCallback((rv, shifter) =>
                                ((currentElement is MemberElement { IsFinal: false }) && shifter.Current == "~" && shifter.Pre1 != "~~") ? 1 : -1),
                            NextToken, "Property-Search-Value2")
                        .Transition("FinalNoInitHandler", "LinkOpHandler",
                            MakeStringTransitCallback((rv, shifter) =>
                                (currentElement is {IsFinal:true} && shifter.Current == "~" && shifter.Pre1 != "~~" && shifter.Pre1 != "~(" && shifter.Pre1 != "~)") ? 1 : -1),
                            NextToken, "Boolean Operation Link")
                        .Transition("FinalSubXHandler", "ParenthesisOpenHandler",
                            MakeStringTransitCallback((rv, shifter) =>
                                (currentElement is {IsFinal:true} && shifter.Current == "~" && shifter.Pre1 == "~(" && shifter.Pre2 == "~(~")
                                    ? 1
                                    : -1),
                            NextToken, "Parenthesis expression")
                        .Transition("FinalXHandler", "ParenthesisCloseHandler",
                            MakeStringTransitCallback((rv, shifter) =>
                                (currentElement is { IsFinal: true } && shifter.Current == "~" && shifter.Pre1 == "~)" && shifter.Pre2 is "~)~" or null)
                                    ? 1
                                    : -1),
                            NextToken, "Parenthesis expression")
                        .Transition("FinalSubXHandler", "MemberHandler",
                            MakeStringTransitCallback((rv, shifter) =>
                                (currentElement is { IsFinal: true } && shifter.Current == "~" && (shifter.Pre1 != "~(" || shifter.Pre2 == "~((") &&
                                    (shifter.Pre1 != "~)" || shifter.Pre2 == "~))"))
                                    ? 1
                                    : -1),
                            NextToken, "Next Member-Fragment")
                        .Transition("StringInitHandler", "ParenthesisOpenHandler",
                            MakeStringTransitCallback(shifter =>
                                (shifter.Current == "(" && shifter.Pre1 != "((" ||
                                 shifter.Current == ")" && shifter.Pre1 != "))")
                                    ? 0
                                    : -1),
                            NextToken, "Initialize")
                        .Transition("StringInitHandler", "MemberHandler",
                            MakeStringTransitCallback(shifter =>
                                ((shifter.Current != "(" || shifter.Pre1 == "((") &&
                                 (shifter.Current != ")" || shifter.Pre1 == "))"))
                                    ? 0
                                    : -1),
                            NextToken, "Initialize")
                        .Transition("FinalXHandler", "EofHandler", (args, handler, me, machine) => WithShifter(args, shifter => shifter.Eof && currentElement is null or {IsFinal:true}), NextToken, "<<end of string>>")
                        .GroupMember("FinalSubXHandler", "LinkOpHandler")
                        .GroupMember("FinalSubXHandler", "StringInitHandler")
                        .GroupMember("FinalSubXHandler", "ParenthesisOpenHandler")
                        .GroupMember("FinalXHandler", "ParenthesisCloseHandler")
                        .GroupMember("FinalXHandler", "StringInitHandler")
                        .GroupMember("FinalXHandler", "OperationHandler")
                        .GroupMember("FinalXHandler", "ValueHandler")
                        .GroupMember("FinalXHandler", "Value2Handler")
                        .GroupMember("FinalNoInitHandler", "ParenthesisCloseHandler")
                        .GroupMember("FinalNoInitHandler", "OperationHandler")
                        .GroupMember("FinalNoInitHandler", "ValueHandler")
                        .GroupMember("FinalNoInitHandler", "Value2Handler")
                ;

            stateMachine = new TransducerMachine<FilterLexerStateHandler, FilterLexer>(
                new DefaultStatusFactory<FilterLexerStateHandler, FilterLexer>(
                    new Dictionary<string, Func<FilterLexer, TransducerMachine<FilterLexerStateHandler, FilterLexer>,
                        RunArguments, FilterLexerStateHandler, FilterLexerStateHandler>>
                    {
                        { "MemberHandler", (parser, machine, args, prev) => new LexerBlankHandler(parser, BlankStatusType.Member) },
                        {"OperationHandler", (parser, machine, args, prev) => new LexerBlankHandler(parser, BlankStatusType.CompOp)},
                        {"ValueHandler", (parser, machine, args, prev) => new LexerBlankHandler(parser, BlankStatusType.Value)},
                        {"Value2Handler", (parser, machine, args, prev) => new LexerBlankHandler(parser, BlankStatusType.Value2)},
                        {"StringInitHandler",(parser, machine, args, prev) => new LexerBlankHandler(parser, BlankStatusType.Initial)},
                        {"LinkOpHandler", (parser, machine, args, prev) => new LexerBlankHandler(parser, BlankStatusType.LinkOp)},
                        {"ParenthesisOpenHandler", (parser, machine, args, prev) => new LexerBlankHandler(parser, BlankStatusType.ParenthesisOpen)},
                        {"ParenthesisCloseHandler", (parser, machine, args, prev) => new LexerBlankHandler(parser, BlankStatusType.ParenthesisClose)},
                        {"EofHandler", (parser, machine, args, prev) => new LexerEofTokenHandler(parser, !args.Argument<bool>("ExpectEoF"))}
                    }), true, statusCollection, "StringInitHandler", this);
        }

        public string CurrentStringValue
        {
            get
            {
                var retVal = string.Empty;
                if (currentElement != null)
                {
                    if (formatElementMode == FilterElementMode.Content)
                    {
                        return currentElement.Content.ToString();
                    }
                    else if (formatElementMode == FilterElementMode.Operator && currentElement is MemberElement mem)
                    {
                        return mem.Operator.ToString();
                    }
                    else if (formatElementMode == FilterElementMode.Value && currentElement is MemberElement memv)
                    {
                        return memv.Value.ToString();
                    }
                }

                throw new Exception("Unexpected String-Value request!");
            }
        }

        public IFilterElement[] TokenizeString(string source)
        {
            stateMachine.Reset("StringInitHandler");
            var shifter = new StringShifter(source);
            var arguments = new RunArguments(d => d.TryAdd("shifter", shifter));
            currentElement = null;
            elements.Clear();
            while (stateMachine.Status is not LexerEofTokenHandler)
            {
                bool doMove = stateMachine.Status is not LexerBlankHandler{StatusType:BlankStatusType.Initial} /*&&
                              stateMachine.Status is not OperationHandler { IsLinkOp: true }*/;
                //var prevState = stateMachine.Status;
                stateMachine.Execute(arguments);
                if (stateMachine.Status is not LexerEofTokenHandler && (doMove/* || stateMachine.Status == prevState*/))
                {
                    shifter.MoveNext();
                }
            }

            return elements.ToArray();
        }

        private Task NextToken(FilterLexerStateHandler status, FilterLexer t, RunArguments arguments)
        {
            if (t.currentElement != null && t.currentElement.Content.Length != 0 && stateMachine.CurrentDepth == 0)
            {
                if (status is LexerBlankHandler { StatusType: BlankStatusType.ParenthesisClose } ||
                         status is LexerBlankHandler { StatusType: BlankStatusType.Value } ||
                         status is LexerBlankHandler { StatusType: BlankStatusType.CompOp})
                {
                    arguments.Set("ExpectEoF", true);
                    //Console.WriteLine($"Type is {status}.");
                    /*t.elements.Add(t.currentElement);
                    t.currentElement = null;*/
                }
                /*else
                {
                    Console.WriteLine($"Type is {status}.");
                }*/

                if (status is LexerBlankHandler { StatusType: BlankStatusType.LinkOp} || 
                    status is not LexerBlankHandler{StatusType:BlankStatusType.CompOp} && t.currentElement is BooleanLinkElement ||
                    status is LexerBlankHandler { StatusType: BlankStatusType.ParenthesisOpen } && t.currentElement is not ParenthesisElement or ParenthesisElement { IsFinal: true } ||
                    status is LexerBlankHandler { StatusType: BlankStatusType.ParenthesisClose } && t.currentElement is not ParenthesisElement or ParenthesisElement { IsFinal: true } ||
                    status is LexerBlankHandler { StatusType: BlankStatusType.Member } && t.currentElement is not MemberElement ||
                    status is LexerEofTokenHandler)
                {
                    t.elements.Add(t.currentElement);
                    currentElement = null;
                }
            }

            t.NextElement(status);
            return Task.CompletedTask;
        }

        private void NextElement(FilterLexerStateHandler status)
        {
            if (status is LexerBlankHandler{StatusType:BlankStatusType.Member} && currentElement == null)
            {
                currentElement = new MemberElement();
            }
            else if (status is LexerBlankHandler { StatusType:BlankStatusType.CompOp})
            {
                formatElementMode = FilterElementMode.Operator;
            }
            else if (status is LexerBlankHandler{ StatusType: BlankStatusType.LinkOp})
            {
                currentElement = new BooleanLinkElement();
                formatElementMode = FilterElementMode.Content;
            }
            else if (status is LexerBlankHandler{StatusType:BlankStatusType.Value})
            {
                formatElementMode = FilterElementMode.Value;
            }
            else if (status is LexerBlankHandler { StatusType: BlankStatusType.Value2 })
            {
                formatElementMode = FilterElementMode.Value2;
            }
            else if (status is LexerBlankHandler { StatusType: BlankStatusType.ParenthesisOpen } && currentElement == null)
            {
                currentElement = new ParenthesisElement();
                formatElementMode = FilterElementMode.Content;
            }
            else if (status is LexerBlankHandler { StatusType: BlankStatusType.ParenthesisClose } && currentElement == null)
            {
                currentElement = new ParenthesisElement();
                formatElementMode = FilterElementMode.Content;
            }
            else if (status is not LexerEofTokenHandler)
            {
                throw new InvalidOperationException($"Unexpected state: {status}");
            }
        }

        private Func<RunArguments, Status<FilterLexerStateHandler, FilterLexer>, FilterLexer,
            TransducerMachine<FilterLexerStateHandler, FilterLexer>, bool> MakeStringTransitCallback(
            Func<StringShifter, int> charSearch)
        {
            return MakeStringTransitCallback((a, s) => charSearch(s));
        }

        private Func<RunArguments, Status<FilterLexerStateHandler, FilterLexer>, FilterLexer,
            TransducerMachine<FilterLexerStateHandler, FilterLexer>, bool> MakeStringTransitCallback(
            Func<RunArguments, StringShifter, int> charSearch)
        {
            var stringTransCondition =
            (RunArguments args, Status<FilterLexerStateHandler, FilterLexer> handler, FilterLexer me,
                TransducerMachine<FilterLexerStateHandler, FilterLexer> machine) => WithShifter(args, shifter =>
            {
                var ln = charSearch(args, shifter);
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

        internal void SwitchState(FilterElementMode newElementMode)
        {
            formatElementMode = newElementMode;
        }

        public void Append(string currentShifterCharacter)
        {
            if (formatElementMode == FilterElementMode.Content)
            {
                currentElement.Content.Append(currentShifterCharacter);
            }
            else if (formatElementMode == FilterElementMode.Operator)
            {
                ((MemberElement)currentElement).Operator.Append(currentShifterCharacter);
            }
            else if (formatElementMode == FilterElementMode.Value)
            {
                ((MemberElement)currentElement).Value.Append(currentShifterCharacter);
            }
            else if (formatElementMode == FilterElementMode.Value2)
            {
                ((MemberElement)currentElement).Value2.Append(currentShifterCharacter);
            }
        }
    }

}
