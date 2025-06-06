using ITVComponents.Formatting.Parser.Model;
using ITVComponents.StateMachine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.StateMachine.Interfaces;
using ITVComponents.StateMachine.Models;

namespace ITVComponents.Formatting.Parser.TokenHandlers
{
    internal class BalanceTokenHandler : FormatExpressionTokenHandler
    {
        protected readonly string StartCharacter;
        protected readonly string BalanceCharacter;
        private bool leaveToParent = false;

        public override Task EnterAsync()
        {
            Target.Append(StartCharacter);
            return base.EnterAsync();
        }

        public override Task ExitAsync()
        {
            Target.Append(BalanceCharacter);
            return base.ExitAsync();
        }

        public BalanceTokenHandler(StringFormatParser target, ParserStateHandler prevState, string startCharacter,
            string balanceCharacter) : base(target, prevState)
        {
            this.StartCharacter = startCharacter;
            this.BalanceCharacter = balanceCharacter;
        }

        protected override bool LeaveToParent => leaveToParent;

        protected override async Task<ITransition<ParserStateHandler, StringFormatParser>> ExecuteInternalAndAsync(RunArguments arguments,
            TransducerMachine<ParserStateHandler, StringFormatParser> stateMachine)
        {
            var transition = await base.ExecuteInternalAndAsync(arguments, stateMachine);
            var currentLeaveState = transition != null;
            var shifter = arguments.Argument<StringShifter>("shifter");
            leaveToParent = shifter.Current == BalanceCharacter;
            var retVal = currentLeaveState || leaveToParent;
            if (!retVal)
            {
                Target.Append(shifter.Current);
            }

            return transition;
        }
    }

    /*internal class ParenthesisBalanceTokenHandler : BalanceTokenHandler
    {
        public ParenthesisBalanceTokenHandler(StringFormatParser target, ParserStateHandler prevState) : base(target,
            prevState, "(", ")")
        {
        }
    }

    internal class IndexerBalanceTokenHandler : BalanceTokenHandler
    {
        public IndexerBalanceTokenHandler(StringFormatParser target, ParserStateHandler prevState) : base(target,
            prevState, "[", "]")
        {
        }
    }

    internal class BracketBalanceTokenHandler : BalanceTokenHandler
    {
        public BracketBalanceTokenHandler(StringFormatParser target, ParserStateHandler prevState) : base(target,
            prevState, "{", "}")
        {
        }
    }

    internal class TernaryBalanceTokenHandler : BalanceTokenHandler
    {
        public TernaryBalanceTokenHandler(StringFormatParser target, ParserStateHandler prevState) : base(target,
            prevState, "?", ":")
        {
        }
    }*/

    internal class StringBalanceTokenHandler : BalanceTokenHandler
    {
        private bool leaveToParent;
        public StringBalanceTokenHandler(StringFormatParser target, ParserStateHandler prevState) : base(target,
            prevState, "\"", "\"")
        {
        }

        protected override bool LeaveToParent => leaveToParent;

        protected override Task<ITransition<ParserStateHandler, StringFormatParser>> ExecuteInternalAndAsync(RunArguments arguments,
            TransducerMachine<ParserStateHandler, StringFormatParser> stateMachine)
        {
            var shifter = arguments.Argument<StringShifter>("shifter");
            leaveToParent = shifter.Current == BalanceCharacter;
            if (!leaveToParent)
            {
                if (shifter.Current != "\\")
                {
                    Target.Append(shifter.Current);
                }
                else
                {
                    Target.Append(shifter.Pre1);
                    shifter.MoveNext();
                }
            }

            return Task.FromResult<ITransition<ParserStateHandler,StringFormatParser>>(null);
        }
    }

    internal class VerbatimStringBalanceTokenHandler : BalanceTokenHandler
    {
        private bool leaveToParent;

        public VerbatimStringBalanceTokenHandler(StringFormatParser target, ParserStateHandler prevState) : base(
            target, prevState, "@\"", "\"")
        {
        }

        protected override bool LeaveToParent => leaveToParent;

        protected override Task<ITransition<ParserStateHandler, StringFormatParser>> ExecuteInternalAndAsync(RunArguments arguments,
            TransducerMachine<ParserStateHandler, StringFormatParser> stateMachine)
        {
            var shifter = arguments.Argument<StringShifter>("shifter");
            leaveToParent = shifter.Current == BalanceCharacter && shifter.Pre1 != "\"\"";
            if (!leaveToParent)
            {
                if (shifter.Current != "\"")
                {
                    Target.Append(shifter.Current);
                }
                else if (shifter.Pre1 == "\"\"")
                {
                    Target.Append(shifter.Pre1);
                    shifter.MoveNext();
                }
            }

            return Task.FromResult<ITransition<ParserStateHandler, StringFormatParser>>(null);
        }
    }
}
