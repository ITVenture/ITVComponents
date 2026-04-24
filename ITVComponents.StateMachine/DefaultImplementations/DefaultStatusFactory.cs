using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Formats.Tar;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Logging;
using ITVComponents.StateMachine.BaseTypes;
using ITVComponents.StateMachine.Interfaces;
using ITVComponents.StateMachine.Models;

namespace ITVComponents.StateMachine.DefaultImplementations
{
    public class DefaultStatusFactory<TStatus,TStatusTarget>:IStatusFactory<TStatus,TStatusTarget> where TStatus : Status<TStatus, TStatusTarget> where TStatusTarget : class
    {
        private readonly Dictionary<string, Func<TStatusTarget, TransducerMachine<TStatus, TStatusTarget>, RunArguments, TStatus, TStatus>> statusCreateCallbacks;
        private ConcurrentBag<TStatus> createdStatuses = new ConcurrentBag<TStatus>();

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

            var retVal = statusCreateCallbacks[statusType](target, machine, arguments, fromStatus);
            createdStatuses.Add(retVal);
            return retVal;
        }

        public void Dispose()
        {
            var bufferedStatuses = createdStatuses.ToArray();
            createdStatuses.Clear();
            foreach (var status in bufferedStatuses)
            {
                try
                {
                    status.Dispose();
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogDebugEvent($"Failed to dispose status: {ex.Message}", LogSeverity.Error);
                }
            }

            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {

        }
    }
}
