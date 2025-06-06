using ITVComponents.StateMachine.BaseTypes;
using ITVComponents.StateMachine.Models;
using ITVComponents.StateMachine.StatusTransit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.StateMachine.Interfaces
{
    public interface ITransition<TStatus, TStatusTarget>
        where TStatusTarget : class
        where TStatus : Status<TStatus, TStatusTarget>
    {
        bool Condition(RunArguments arguments, Status<TStatus, TStatusTarget> status, TStatusTarget target,
            TransducerMachine<TStatus, TStatusTarget> machine);

        Task ExecuteTransition(TStatus status, TStatusTarget target, RunArguments arguments);
        string TransitionDescription { get; }
        string FromStatus { get; }
        string ToStatus { get; }
        TransitionType TransitionType { get; }
        ITransition<TStatus,TStatusTarget> WithAlternativeSource(string nt);
    }
}
