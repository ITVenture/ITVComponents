using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.StateMachine.Interfaces;
using ITVComponents.StateMachine.Models;

namespace ITVComponents.StateMachine.BaseTypes
{
    public abstract class Status<TStatus, TStatusTarget> where TStatusTarget : class
    where TStatus: Status<TStatus,TStatusTarget>
    {
        private TransducerMachine<TStatus, TStatusTarget> machine;

        public Status(TStatusTarget target)
        {
            Target = target;
        }

        public ITransition<TStatus, TStatusTarget>[] Transitions { get; private set; }

        public TStatusTarget Target { get; }
        public abstract Task EnterAsync();

        public abstract Task ExitAsync();

        public abstract Task<RunResult<TStatus,TStatusTarget>> RunAsync(RunArguments arguments, TransducerMachine<TStatus, TStatusTarget> stateMachine);

        internal void SetTransitions(ITransition<TStatus, TStatusTarget>[] transitions, TransducerMachine<TStatus, TStatusTarget> machine)
        {
            Transitions = transitions;
            this.machine = machine;
        }

        /*protected bool TransitionRequired(RunArguments arguments)
        {
            return Transitions.Count(n => n.Condition(arguments, this, Target, machine)) == 1;
        }*/

        protected internal ITransition<TStatus,TStatusTarget> GetNextStatus(RunArguments arguments, bool doThrow = true)
        {
            var retValRaw = Transitions.Where(n => n.Condition(arguments, this, Target, machine)).ToArray();
            if (retValRaw.Length > 1)
            {
                if (!doThrow)
                {
                    return null;
                }

                throw new InvalidOperationException($"Found {retValRaw.Length}. Expected: 1");
            }

            if (retValRaw.Length == 0)
            {
                if (!doThrow)
                {
                    return null;
                }

                var legalTransitions = string.Join(", ",
                    Transitions.Where(n =>
                            !string.IsNullOrEmpty(n.TransitionDescription))
                        .Select(n => n.TransitionDescription));
                if (string.IsNullOrEmpty(legalTransitions))
                {
                    throw new InvalidOperationException("No transition was found for the desired status.");
                }

                throw new InvalidOperationException(
                    $"No transition was found for the desired status. Legal Transitions: {legalTransitions}.");
            }

            return retValRaw[0];
        }
    }
}
