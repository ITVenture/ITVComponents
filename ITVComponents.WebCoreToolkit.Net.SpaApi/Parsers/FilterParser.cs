using Antlr4.Runtime;
using ITVComponents.EFRepo.Expressions.Models;
using ITVComponents.StateMachine;
using ITVComponents.StateMachine.BaseTypes;
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
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using ITVComponents.StateMachine.DefaultImplementations;

namespace ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers
{
    public class FilterParser
    {
        private FilterLexer lexer;
        private TransducerMachine<FilterParserStateHandler, FilterParser> stateMachine;
        private List<IParsedElement> parsedElements = new List<IParsedElement>();
        private IParsedElement currentParsedElement;
        private Stack<List<IParsedElement>> parserStack = new Stack<List<IParsedElement>>();
        private Stack<IParsedElement> groupStack = new Stack<IParsedElement>();
        public FilterParser()
        {
            lexer = new FilterLexer();
            TransitionCollection<FilterParserStateHandler, FilterParser> statusCollection =
                    new TransitionCollection<FilterParserStateHandler, FilterParser>()
                        .Transition("Initial","Member",(arguments, status, parser, machine) => arguments.Argument<ArrayShifter<IFilterElement>>("shifter") is {Eof:false, Current: MemberElement},
                            null, "Member-Expression")
                        .Transition("Initial", "Parenthesis",(arguments, status, parser, machine) => arguments.Argument<ArrayShifter<IFilterElement>>("shifter") is {Eof:false, Current:ParenthesisElement{Opening:true}},
                            null, "Parenthesis-Expression")
                        .SubTransition("Parenthesis", "Member", (arguments, status, parser, machine) => arguments.Argument<ArrayShifter<IFilterElement>>("shifter") is {Eof:false, Current:MemberElement},
                            null, "Member-Expression")
                        .SubTransition("Parenthesis", "Parenthesis",(arguments, status, parser, machine) => arguments.Argument<ArrayShifter<IFilterElement>>("shifter") is { Eof: false, Current: ParenthesisElement { Opening: true } },
                            null, "Parenthesis-Expression")
                        .Transition("FunctionalGroup", "Link", (arguments, status, parser, machine) => arguments.Argument<ArrayShifter<IFilterElement>>("shifter") is { Eof: false, Current: BooleanLinkElement },
                            null, "Parenthesis-Expression")
                        .Transition("Link", "Member", (arguments, status, parser, machine) => arguments.Argument<ArrayShifter<IFilterElement>>("shifter") is { Eof: false, Current: MemberElement },
                            null, "Member-Expression")
                        .Transition("Link", "Parenthesis", (arguments, status, parser, machine) => arguments.Argument<ArrayShifter<IFilterElement>>("shifter") is { Eof: false, Current: ParenthesisElement { Opening: true } },
                            null, "Parenthesis-Expression")
                        .Transition("FunctionalGroup", "EOS", (arguments, status, parser, machine) => arguments.Argument<ArrayShifter<IFilterElement>>("shifter").Eof,
                            null, "<<End of Statement>>")
                        .GroupMember("FunctionalGroup","Parenthesis")
                        .GroupMember("FunctionalGroup", "Member")
                ;

            stateMachine = new TransducerMachine<FilterParserStateHandler, FilterParser>(
                new DefaultStatusFactory<FilterParserStateHandler, FilterParser>(
                    new Dictionary<string, Func<FilterParser, TransducerMachine<FilterParserStateHandler, FilterParser>,
                        RunArguments, FilterParserStateHandler, FilterParserStateHandler>>
                    {
                        { "Initial", (parser, machine, args, prev) => new ParserBlankHandler(parser, prev) },
                        {"Member", (parser, machine, args, prev) => new ParserElementHandler(parser, prev, args) },
                        {"Parenthesis", (parser, machine, args, prev) => new ParserParenthesisHandler(parser, prev)},
                        {"Link", (parser, machine, args, prev) => new ParserLinkHandler(parser, prev, args)},
                        {"EOS", (parser, machine, args, prev) => new ParserEoSHandler(parser, prev)}
                    }), true,
                statusCollection, "Initial", this);
        }

        public IParsedElement[] GetFilter(string filterExpression)
        {
            var preParsed = lexer.TokenizeString(filterExpression);
            var shifter = new ArrayShifter<IFilterElement>(preParsed);
            var arguments = new RunArguments(d => d.TryAdd("shifter", shifter));
            currentParsedElement = null;
            parsedElements.Clear();
            //var postProcessDone = false;
            while (stateMachine.Status is not ParserEoSHandler)
            {
                stateMachine.Execute(arguments);
                if (stateMachine.Status is not ParserEoSHandler && stateMachine.Status is not ParserBlankHandler && !shifter.Eof)
                {
                    shifter.MoveNext();
                }
            }

            return parsedElements.ToArray();
        }

        private void Reset()
        {
            
        }

        public void FlushFilter(IParsedElement nextElement)
        {
            //parsedElements.Add(currentParsedElement);
            currentParsedElement = null;
            AddElement(nextElement);
        }

        public void AddElement(IParsedElement nextElement)
        {
            if (currentParsedElement == null)
            {
                currentParsedElement = new MemberGroup(false);
                parsedElements.Add(currentParsedElement);
            }

            currentParsedElement.Elements.Add(nextElement);
        }

        public void LayerDown(IParsedElement nextParent)
        {
            parserStack.Push(parsedElements);
            groupStack.Push(currentParsedElement);
            parsedElements = (List<IParsedElement>)nextParent.Elements;
            currentParsedElement = null;
        }

        public void LayerUp()
        {
            parsedElements = parserStack.Pop();
            currentParsedElement = groupStack.Pop();
        }
    }
}
