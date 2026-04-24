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
        }
    }
}
