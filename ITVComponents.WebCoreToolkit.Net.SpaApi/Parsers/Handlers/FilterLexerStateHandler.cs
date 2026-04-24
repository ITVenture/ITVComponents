using ITVComponents.Formatting.Parser;
using ITVComponents.StateMachine;
using ITVComponents.StateMachine.BaseTypes;
using ITVComponents.StateMachine.Models;
using ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Handlers
{
    internal abstract class FilterLexerStateHandler : Status<FilterLexerStateHandler, FilterLexer>
    {
        protected FilterLexerStateHandler(FilterLexer target) : base(target)
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

        public override Task<RunResult<FilterLexerStateHandler, FilterLexer>> RunAsync(RunArguments arguments, TransducerMachine<FilterLexerStateHandler, FilterLexer> stateMachine)
        {
            var shifter = arguments.Argument<StringShifter>("shifter");
            var transition = GetNextStatus(arguments, false);
            bool leaveState = transition != null;
            if (!leaveState)
            {
                if (arguments.Argument<bool>("HasDoubles", true))
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

            return Task.FromResult(new RunResult<FilterLexerStateHandler, FilterLexer>(!leaveState, transition, null));
        }
    }
}
