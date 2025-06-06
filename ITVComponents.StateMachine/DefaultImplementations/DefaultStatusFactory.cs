using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.StateMachine.BaseTypes;
using ITVComponents.StateMachine.Interfaces;
using ITVComponents.StateMachine.Models;

namespace ITVComponents.StateMachine.DefaultImplementations
{
    public class DefaultStatusFactory<TStatus,TStatusTarget>:IStatusFactory<TStatus,TStatusTarget> where TStatus : Status<TStatus, TStatusTarget> where TStatusTarget : class
    {
        private readonly Dictionary<string, Func<TStatusTarget, TransducerMachine<TStatus, TStatusTarget>, RunArguments, TStatus, TStatus>> statusCreateCallbacks;

        public DefaultStatusFactory(
            Dictionary<string, Func<TStatusTarget, TransducerMachine<TStatus, TStatusTarget>, RunArguments, TStatus,
                TStatus>> statusCreateCallbacks)
        {
            this.statusCreateCallbacks = statusCreateCallbacks;
        }

        public IEnumerable<string> KnownStates => statusCreateCallbacks.Keys;

        public TStatus ConstructStatus(string statusType, TStatusTarget target, TransducerMachine<TStatus, TStatusTarget> machine,
            RunArguments arguments, TStatus fromStatus)
        {
            if (!statusCreateCallbacks.ContainsKey(statusType))
            {
                throw new InvalidOperationException($"{statusType} status is unknown!");
            }

            return statusCreateCallbacks[statusType](target, machine, arguments, fromStatus);
        }
    }
}
