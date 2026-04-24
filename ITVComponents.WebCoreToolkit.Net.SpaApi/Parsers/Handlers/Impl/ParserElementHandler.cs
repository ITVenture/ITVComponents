using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.StateMachine.Models;
using ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Elements;
using ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Helpers;
using ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Model;

namespace ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Handlers.Impl
{
    internal class ParserElementHandler:FilterParserStateHandler
    {
        private readonly FilterParser target;
        private readonly FilterParserStateHandler previousStateHandler;
        private readonly RunArguments arg;

        public ParserElementHandler(FilterParser target, FilterParserStateHandler previousStateHandler, RunArguments arg) : base(target, previousStateHandler)
        {
            this.target = target;
            this.previousStateHandler = previousStateHandler;
            this.arg = arg;
        }

        public override Task EnterAsync()
        {
            var shifter = arg.Argument<ArrayShifter<IFilterElement>>("shifter");
            var parsedElement = new WrappedElement{LexerToken=shifter.Current};
            if (previousStateHandler is ParserLinkHandler { LinkNext: false })
            {
                target.FlushFilter(parsedElement);
            }
            else
            {
                target.AddElement(parsedElement);
            }

            return Task.CompletedTask;
        }

        public override Task ExitAsync()
        {
            return Task.CompletedTask;
        }
    }
}
