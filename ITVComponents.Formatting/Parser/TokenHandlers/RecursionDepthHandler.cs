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
    internal class RecursionDepthHandler:ParserStateHandler
    {
        public RecursionDepthHandler(StringFormatParser target) : base(target)
        {
        }

        public override Task EnterAsync()
        {
            Target.SwitchState(FormatElementAppendMode.RecursionDepth);
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
            bool leaveState = transition != null;
            if (!leaveState)
            {
                Target.Append(shifter.Current);
            }

            return Task.FromResult(new RunResult<ParserStateHandler, StringFormatParser>(!leaveState, transition, null));
        }
    }
}
