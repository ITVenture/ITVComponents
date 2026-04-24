using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Handlers.Impl
{
    internal class ParserEoSHandler:FilterParserStateHandler
    {
        public ParserEoSHandler(FilterParser target, FilterParserStateHandler prevState) : base(target, prevState)
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
    }
}
