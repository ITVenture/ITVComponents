using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.ParallelProcessing
{
    /// <summary>
    /// A Scheduler that modifies the Evaluation-Context of a Scheduler-Policy object can implement this interface. Before the Scheduler-instruction of the Policy is queried, thie method EnterPolicyContext is called.
    /// </summary>
    public interface ISchedulerlPolicyContextProvider
    {
        /// <summary>
        /// Modifies the Evaluation-Context of the used JobSchedulePolicy, before the value of the Instruction is queried
        /// </summary>
        /// <returns></returns>
        IDisposable EnterPolicyContext();
    }
}
