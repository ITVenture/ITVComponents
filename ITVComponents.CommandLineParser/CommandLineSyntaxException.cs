using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace ITVComponents.CommandLineParser
{
    [Serializable]
    public class CommandLineSyntaxException : Exception
    {
        //
        // For guidelines regarding the creation of new exception types, see
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/cpgenref/html/cpconerrorraisinghandlingguidelines.asp
        // and
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dncscol/html/csharp07192001.asp
        //

        public CommandLineSyntaxException():base("The Provided command contains invalid arguments")
        {
        }

        public CommandLineSyntaxException(string argumentName) : base(string.Format("The Argument {0} is missing or has an invalid value", argumentName))
        {
        }

        protected CommandLineSyntaxException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}
