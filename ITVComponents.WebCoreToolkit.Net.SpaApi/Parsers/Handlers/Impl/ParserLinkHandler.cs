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
    internal class ParserLinkHandler:FilterParserStateHandler
    {
        private readonly RunArguments arguments;

        public ParserLinkHandler(FilterParser target, FilterParserStateHandler prevState, RunArguments arguments) : base(target, prevState)
        {
            this.arguments = arguments;
        }

        public bool LinkNext { get; private set; }

        public override Task EnterAsync()
        {
            var shifter = arguments.Argument<ArrayShifter<IFilterElement>>("shifter");
            LinkNext = shifter.Current is BooleanLinkElement { And: true };
            return Task.CompletedTask;
        }

        public override Task ExitAsync()
        {
            return Task.CompletedTask;
        }
    }
}
