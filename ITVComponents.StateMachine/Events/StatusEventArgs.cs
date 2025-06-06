using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.StateMachine.BaseTypes;

namespace ITVComponents.StateMachine.Events
{
    public class StatusEventArgs<TStatus,TStatusTarget>
    where TStatus:Status<TStatus, TStatusTarget>
    where TStatusTarget: class
    {
        public TStatus Status { get; set; }
    }
}
