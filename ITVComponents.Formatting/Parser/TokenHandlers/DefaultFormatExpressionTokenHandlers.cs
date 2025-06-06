using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Formatting.Parser.Model;
using ITVComponents.StateMachine;
using ITVComponents.StateMachine.Interfaces;
using ITVComponents.StateMachine.Models;

namespace ITVComponents.Formatting.Parser.TokenHandlers
{
    internal class DefaultFormatExpressionTokenHandler:FormatExpressionTokenHandler
    {
        protected override async Task<ITransition<ParserStateHandler, StringFormatParser>> ExecuteInternalAndAsync(RunArguments arguments, TransducerMachine<ParserStateHandler, StringFormatParser> stateMachine)
        {
            var transition = await base.ExecuteInternalAndAsync(arguments, stateMachine);
            var currentLeaveState = transition != null;
            var shifter = arguments.Argument<StringShifter>("shifter");
            /*var tmp = currentLeaveState || IsBaseKeyCharacter(shifter) || shifter.Current == "]"
                                        || shifter.Current == ":"
                                        || shifter.Current == ",";*/
            if (!currentLeaveState)
            {
                Target.Append(shifter.Current);
            }

            return transition;
        }

        public DefaultFormatExpressionTokenHandler(StringFormatParser target) : base(target, null)
        {
        }
    }

    internal class BlockFormatExpressionTokenHandler : DefaultFormatExpressionTokenHandler
    {
        public BlockFormatExpressionTokenHandler(StringFormatParser target) : base(target)
        {
        }
    }

    internal class RecursiveFormatExpressionTokenHandler : DefaultFormatExpressionTokenHandler
    {
        public RecursiveFormatExpressionTokenHandler(StringFormatParser target) : base(target)
        {
        }
    }

    internal class RecursiveBlockFormatExpressionHandler : DefaultFormatExpressionTokenHandler
    {
        public RecursiveBlockFormatExpressionHandler(StringFormatParser target) : base(target)
        {
        }
    }
}
