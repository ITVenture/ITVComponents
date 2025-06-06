using ITVComponents.StateMachine.BaseTypes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.StateMachine.Interfaces;
using ITVComponents.StateMachine.Models;
using ITVComponents.StateMachine.StatusTransit;

namespace ITVComponents.StateMachine.DefaultImplementations
{
    public class Transition<TStatus, TStatusTarget>:ITransition<TStatus,TStatusTarget>
        where TStatusTarget : class
        where TStatus : Status<TStatus, TStatusTarget>
    {
        private Func<RunArguments, Status<TStatus, TStatusTarget>, TStatusTarget,
            TransducerMachine<TStatus, TStatusTarget>, bool> condition;

        private Func<TStatus, TStatusTarget, RunArguments, Task> execute;

        public Transition(string fromStatus, string toStatus, TransitionType transitionType, string transitionDescription, Func<TStatus, TStatusTarget, RunArguments, Task> execute, Func<RunArguments, Status<TStatus, TStatusTarget>, TStatusTarget, TransducerMachine<TStatus, TStatusTarget>, bool> condition)
        {
            FromStatus = fromStatus;
            ToStatus = toStatus;
            TransitionType = transitionType;
            TransitionDescription = transitionDescription;
            this.execute = execute;
            this.condition = condition;
        }

        public string FromStatus { get; }
        public string ToStatus { get; }

        public TransitionType TransitionType { get; }
        public ITransition<TStatus, TStatusTarget> WithAlternativeSource(string newSourceStatus)
        {
            return new Transition<TStatus, TStatusTarget>(newSourceStatus, ToStatus, TransitionType, TransitionDescription,
                execute, condition);
        }

        public bool Condition(RunArguments arguments, Status<TStatus, TStatusTarget> status, TStatusTarget target, TransducerMachine<TStatus, TStatusTarget> machine)
        {
            return condition?.Invoke(arguments, status, target, machine) ?? false;
        }

        public Task ExecuteTransition(TStatus status, TStatusTarget target, RunArguments arguments)
        {
            return execute?.Invoke(status, target, arguments) ?? Task.CompletedTask;
        }

        public string TransitionDescription { get;  }
    }
}
