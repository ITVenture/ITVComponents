using ITVComponents.StateMachine;
using ITVComponents.StateMachine.BaseTypes;
using ITVComponents.StateMachine.Models;
using ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Elements;
using ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Model;

namespace ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Handlers
{
    internal abstract class FilterParserStateHandler: Status<FilterParserStateHandler, FilterParser>
    {
        private readonly FilterParserStateHandler prevState;

        protected internal virtual FilterParserStateHandler ParentState => prevState?.ParentState;

        protected virtual FilterParserStateHandler ToParentState => ParentState;

        public FilterParserStateHandler(FilterParser target, FilterParserStateHandler prevState) : base(target)
        {
            this.prevState = prevState;
        }

        

        public override Task<RunResult<FilterParserStateHandler, FilterParser>> RunAsync(RunArguments arguments, TransducerMachine<FilterParserStateHandler, FilterParser> stateMachine)
        {
            var shifter = arguments.Argument<ArrayShifter<IFilterElement>>("shifter");
            var transition = GetNextStatus(arguments, false);
            bool leaveState = transition != null;
            bool stateUp = shifter.Current is ParenthesisElement { Opening: false };
            if (!stateUp)
                return Task.FromResult(
                new RunResult<FilterParserStateHandler, FilterParser>(!leaveState, transition, prevState));

            return Task.FromResult(new RunResult<FilterParserStateHandler, FilterParser>(ToParentState));
        }
    }
}
