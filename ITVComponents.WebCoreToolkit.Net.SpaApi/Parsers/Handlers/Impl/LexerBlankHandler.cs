using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers.Handlers.Impl
{
    internal class LexerBlankHandler:FilterLexerStateHandler
    {
        public BlankStatusType StatusType { get; }

        public LexerBlankHandler(FilterLexer target, BlankStatusType statusType) : base(target)
        {
            StatusType = statusType;
        }
    }

    public enum BlankStatusType
    {
        Initial,
        Member,
        Value,
        Value2,
        ParenthesisOpen,
        ParenthesisClose,
        LinkOp,
        CompOp
    }
}
