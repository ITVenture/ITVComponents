using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Evaluators.FlowControl
{
    public interface IAssignableEvaluator
    {
        /// <summary>
        /// Assigns the given value to this assignable Evaluator object
        /// </summary>
        /// <param name="value">the value to assign</param>
        /// <param name="contextTarget">A Descriptor for the target on which to set the given value</param>
        void Assign(object value, ActiveCodeAccessDescriptor contextTarget);
    }
}
