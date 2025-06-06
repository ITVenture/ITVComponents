using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.StateMachine.BaseTypes;
using ITVComponents.StateMachine.Interfaces;

namespace ITVComponents.StateMachine.Models
{
    public class RunResult<TStatus, TStatusTarget> where TStatus : Status<TStatus, TStatusTarget>
    where TStatusTarget:class
    {
        public RunResult(bool keepStatus, ITransition<TStatus,TStatusTarget> transition, TStatus parentLayerStatus)
        {
            KeepStatus = keepStatus;
            ParentLayerStatus = parentLayerStatus;
            Transition = transition;
        }

        public RunResult(TStatus previousStatus)
        {
            UpStatus = previousStatus;
        }

        public bool KeepStatus { get; }

        public TStatus UpStatus { get; }

        public TStatus ParentLayerStatus { get; }
        public ITransition<TStatus, TStatusTarget> Transition { get; }
    }
}
