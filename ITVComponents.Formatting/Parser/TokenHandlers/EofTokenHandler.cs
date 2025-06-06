using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.StateMachine;
using ITVComponents.StateMachine.Models;

namespace ITVComponents.Formatting.Parser.TokenHandlers
{
    internal class EofTokenHandler: ParserStateHandler
    {
        private bool throwOnEnter = false;
        public EofTokenHandler(StringFormatParser target) : base(target)
        {
        }

        public EofTokenHandler(StringFormatParser target, bool unexpected):this(target)
        {
            throwOnEnter = unexpected;
            ;
        }

        public override Task EnterAsync()
        {
            if (throwOnEnter)
            {
                throw new InvalidOperationException("Unexpected end of string!");
            }
            
            return Task.CompletedTask;
        }

        public override Task ExitAsync()
        {
            return Task.CompletedTask;
        }

        public override Task<RunResult<ParserStateHandler, StringFormatParser>> RunAsync(RunArguments arguments, TransducerMachine<ParserStateHandler, StringFormatParser> stateMachine)
        {
            return Task.FromResult(new RunResult<ParserStateHandler, StringFormatParser>(true, null, null));
        }
    }
}
