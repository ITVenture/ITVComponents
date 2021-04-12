using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Decisions
{
    public interface IConstraint
    {
        /// <summary>
        /// Sets the Parent of this Constraint
        /// </summary>
        /// <param name="parent">the new Parent of this constraint</param>
        void SetParent(IDecider parent);

        /// <summary>
        /// Verifies the provided input
        /// </summary>
        /// <param name="data">the data that was provided by a source</param>
        /// <param name="message">the message that was generated during the validation of this constraint</param>
        /// <returns>a value indicating whether the data fullfills the requirements of the underlaying Requestor</returns>
        DecisionResult Verify(object data, out string message);
    }
}
