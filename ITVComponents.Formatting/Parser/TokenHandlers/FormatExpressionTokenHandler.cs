using ITVComponents.Formatting.Parser.Model;
using ITVComponents.StateMachine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.StateMachine.Interfaces;
using ITVComponents.StateMachine.Models;

namespace ITVComponents.Formatting.Parser.TokenHandlers
{
    internal abstract class FormatExpressionTokenHandler : ParserStateHandler
    {
        private readonly ParserStateHandler prevState;

        protected FormatExpressionTokenHandler(StringFormatParser target, ParserStateHandler prevState):base(target)
        {
            this.prevState = prevState;
        }

        protected virtual bool LeaveToParent { get; } = false;

        protected virtual string AppendStr { get; } = "";
        public override Task EnterAsync()
        {
            return Task.CompletedTask;
        }

        public override Task ExitAsync()
        {
            return Task.CompletedTask;
        }

        protected virtual Task<ITransition<ParserStateHandler,StringFormatParser>> ExecuteInternalAndAsync(RunArguments arguments,
            TransducerMachine<ParserStateHandler, StringFormatParser> stateMachine)
        {
            var value = this.GetNextStatus(arguments, false);
            return Task.FromResult(value);
        }

        public override async Task<RunResult<ParserStateHandler, StringFormatParser>> RunAsync(RunArguments arguments, TransducerMachine<ParserStateHandler, StringFormatParser> stateMachine)
        {
            var transition = await ExecuteInternalAndAsync(arguments, stateMachine);
            bool leaveState = transition != null;
            if (!leaveState || LeaveToParent)
            {
                Target.Append(AppendStr);
            }

            if (!LeaveToParent)
                return new RunResult<ParserStateHandler, StringFormatParser>(!leaveState, transition, prevState);
            return new RunResult<ParserStateHandler, StringFormatParser>(prevState);
            /*
             if (o == "(")
               {
                   currentElement.Length++;
                   currentElement.Content.Append(o);
                   parserStack.Push(currentState);
                   currentState = ParseState.FormatParenthesis;
               }
               else if (o == "[")
               {
                   currentElement.Length++;
                   currentElement.Content.Append(o);
                   parserStack.Push(currentState);
                   currentState = ParseState.FormatIndexer;
               }
               else if (o == "{")
               {
                   currentElement.Length++;
                   currentElement.Content.Append(o);
                   parserStack.Push(currentState);
                   currentState = ParseState.FormatBracket;
               }
               else if (o == "@" && t == "@\"")
               {
                   currentElement.Length += 2;
                   currentElement.Content.Append(t);
                   parserStack.Push(currentState);
                   i++;
                   currentState = ParseState.FormatVerbatimString;
               }
               else if (o == "\"")
               {
                   currentElement.Length++;
                   currentElement.Content.Append(o);
                   parserStack.Push(currentState);
                   currentState = ParseState.FormatString;
               }
               else if (o == "?" && t != "?." && t != "?[") //Exclude Null-Propagations
               {
                   currentElement.Length++;
                   currentElement.Content.Append(o);
                   parserStack.Push(currentState);
                   currentState = ParseState.FormatTerentary;
               }
               else if (o == ":")
               {
                   currentElement.Length++;
                   parserStack.Push(currentState);
                   currentState = ParseState.FormatterHint;
               }
               else if (o == ",")
               {
                   currentElement.Length++;
                   parserStack.Push(currentState);
                   currentState = ParseState.FormatterLength;
               }
               else if (o == "]" ||
                        (o == "}" && ((FormatElement)currentElement).IsRecursive))
               {
                   if (t != "]{")
                   {
                       if (((FormatElement)currentElement).RecursionDepthString.Length != 0)
                       {
                           ((FormatElement)currentElement).RecursionDepth = int.Parse(((FormatElement)currentElement).RecursionDepthString.ToString());
                       }

                       if (currentElement.Length != 0)
                       {
                           elements.Add(currentElement);
                       }

                       currentElement = new StringElement();
                       currentElement.Start = i + 1;
                       currentState = parserStack.Pop();
                       if (currentState != ParseState.String)
                       {
                           throw new FormatException($"Unexpected Token @{i}!");
                       }

                       if (t == "]#")
                       {
                           i++;
                       }
                   }
                   else
                   {
                       parserStack.Push(currentState);
                       ((FormatElement)currentElement).IsRecursive = true;
                       if (!recursiveBlocks.Contains(((FormatElement)currentElement).CodeType))
                       {
                           throw new FormatException(
                               "RecursionDepth is not supported for non-recursive elements!");
                       }

                       currentState = ParseState.RecursionDepth;
                       i++;
                   }
               }
               else
               {
                   currentElement.Length++;
                   currentElement.Content.Append(o);
               }
             */
        }
    }
}
