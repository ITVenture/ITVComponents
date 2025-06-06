using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Formatting.Parser.Model;
using ITVComponents.StateMachine;
using ITVComponents.StateMachine.Models;

namespace ITVComponents.Formatting.Parser.TokenHandlers
{
    internal class FormatHintHandler : ParserStateHandler
    {
        private readonly ParserStateHandler parentState;
        private readonly FormatElementAppendMode appendMode;

        public FormatHintHandler(StringFormatParser target, ParserStateHandler parentState, FormatElementAppendMode appendMode):base(target)
        {
            this.parentState = parentState;
            this.appendMode = appendMode;
        }
        public override Task EnterAsync()
        {
            Target.SwitchState(appendMode);
            return Task.CompletedTask;
        }

        public override Task ExitAsync()
        {
            return Task.CompletedTask;
        }

        public override Task<RunResult<ParserStateHandler, StringFormatParser>> RunAsync(RunArguments arguments, TransducerMachine<ParserStateHandler, StringFormatParser> stateMachine)
        {
            var shifter = arguments.Argument<StringShifter>("shifter");
            var transition = GetNextStatus(arguments, false);
            var leaveState = transition != null;
            var stateBack = shifter.Current == "]";
            if (!stateBack)
            {
                if (!leaveState)
                {
                    Target.Append(shifter.Current);
                }

                return Task.FromResult(new RunResult<ParserStateHandler, StringFormatParser>(!leaveState, transition, parentState));
            }

            shifter.Skip();
            return Task.FromResult(new RunResult<ParserStateHandler, StringFormatParser>(parentState));
        }
    }
}
