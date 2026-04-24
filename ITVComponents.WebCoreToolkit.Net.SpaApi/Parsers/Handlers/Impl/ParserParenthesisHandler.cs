using Antlr4.Runtime.Misc;
using ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Elements;
using ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Helpers;
using ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.StateMachine;
using ITVComponents.StateMachine.Models;

namespace ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Handlers.Impl
{
    internal class ParserParenthesisHandler:FilterParserStateHandler
    {
        private readonly FilterParser target;
        private readonly FilterParserStateHandler prevState;

        protected internal override FilterParserStateHandler ParentState => this;

        protected override FilterParserStateHandler ToParentState => prevState?.ParentState;

        public ParserParenthesisHandler(FilterParser target, FilterParserStateHandler prevState) : base(target, prevState)
        {
            this.target = target;
            this.prevState = prevState;
        }

        public override Task EnterAsync()
        {

            var nextGroupElement = new MemberGroup(true);
            if (prevState is ParserLinkHandler { LinkNext: false })
            {
                target.FlushFilter(nextGroupElement);
            }
            else
            {
                target.AddElement(nextGroupElement);
            }

            Target.LayerDown(nextGroupElement);
            return Task.CompletedTask;
        }

        public override Task<RunResult<FilterParserStateHandler, FilterParser>> RunAsync(RunArguments arguments, TransducerMachine<FilterParserStateHandler, FilterParser> stateMachine)
        {
            var shifter = arguments.Argument<ArrayShifter<IFilterElement>>("shifter");
            if (shifter.Eof)
            {
                var transition = GetNextStatus(arguments, false);
                bool leaveState = transition != null;
                return Task.FromResult(
                    new RunResult<FilterParserStateHandler, FilterParser>(!leaveState, transition, prevState));
            }
            //Console.WriteLine("gugus!");
            return base.RunAsync(arguments, stateMachine);
        }

        public override Task ExitAsync()
        {
            Target.LayerUp();
            return Task.CompletedTask;
        }
    }
}
