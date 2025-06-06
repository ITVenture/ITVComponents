using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Formatting.Elements;
using ITVComponents.Formatting.Parser.Model;
using ITVComponents.StateMachine;
using ITVComponents.StateMachine.Models;

namespace ITVComponents.Formatting.Parser.TokenHandlers
{
    internal class StringTokenHandler:ParserStateHandler
    {
        public StringTokenHandler(StringFormatParser target):base(target)
        {
        }

        public override Task EnterAsync()
        {
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
                if (arguments.Argument<bool>("HasDoubles",true))
                {
                    shifter.MoveNext(1);
                }

                Target.Append(shifter.Current);
            }
            else if (arguments.Argument<int>("ShiftLn") != 0)
            {
                int stepOver = arguments.Argument<int>("ShiftLn", true);
                shifter.MoveNext(stepOver);
            }

            arguments.Remove("ShiftLn");
            arguments.Remove("HasDoubles");

            return Task.FromResult(new RunResult<ParserStateHandler, StringFormatParser>(!leaveState, transition, null));
        }
    }
}
