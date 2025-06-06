using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.StateMachine.BaseTypes;

namespace ITVComponents.Formatting.Parser
{
    public abstract class ParserStateHandler:Status<ParserStateHandler,StringFormatParser>
    {
        protected ParserStateHandler(StringFormatParser target) : base(target)
        {
        }
    }
}
