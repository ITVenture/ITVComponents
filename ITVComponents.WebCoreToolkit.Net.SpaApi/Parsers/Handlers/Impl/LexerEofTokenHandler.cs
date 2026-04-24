using ITVComponents.Formatting.Parser;
using ITVComponents.StateMachine;
using ITVComponents.StateMachine.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Handlers.Impl
{
    internal class LexerEofTokenHandler : FilterLexerStateHandler
    {
        private bool throwOnEnter = false;
        public LexerEofTokenHandler(FilterLexer target) : base(target)
        {
        }

        public LexerEofTokenHandler(FilterLexer target, bool unexpected) : this(target)
        {
            throwOnEnter = unexpected;
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

        public override Task<RunResult<FilterLexerStateHandler, FilterLexer>> RunAsync(RunArguments arguments, TransducerMachine<FilterLexerStateHandler, FilterLexer> stateMachine)
        {
            return Task.FromResult(new RunResult<FilterLexerStateHandler, FilterLexer>(true, null, null));
        }
    }
}
