using ITVComponents.StateMachine.BaseTypes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.StateMachine.Models;

namespace ITVComponents.StateMachine.Interfaces
{
    public interface IStatusFactory<TStatus, TStatusTarget>:IDisposable where TStatusTarget : class
        where TStatus : Status<TStatus, TStatusTarget>
    {
        IEnumerable<string> KnownStates { get; }
        TStatus ConstructStatus(string statusType, TStatusTarget target, TransducerMachine<TStatus, TStatusTarget> machine, RunArguments arguments, TStatus fromStatus);
    }
}
