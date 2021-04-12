using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
//using System.Runtime.Remoting.Messaging;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.PowerShell
{
    [Serializable]
    public class PowerShellApiException<T> : Exception
    {
        //
        // For guidelines regarding the creation of new exception types, see
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/cpgenref/html/cpconerrorraisinghandlingguidelines.asp
        // and
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dncscol/html/csharp07192001.asp
        //

        public ErrorRecord[] Errors { get; }

        public T[] Results { get; }

        public PowerShellApiException(string message, Exception innerException, ErrorRecord[] errors, T[] results) : base(message, innerException)
        {
            Errors = errors;
            Results = results;
        }

        public PowerShellApiException(string message, ErrorRecord[] errors, T[] results):base(message)
        {
            Errors = errors;
            Results = results;
        }

        protected PowerShellApiException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}
