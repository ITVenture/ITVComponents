using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Decisions
{
    public interface IDeciderContext
    {
        /// <summary>
        /// Sets a Context-Value for a specific constraint with a given name
        /// </summary>
        /// <param name="constraint">the constraint for which to set the value</param>
        /// <param name="name">the name of the value that is set</param>
        /// <param name="value">the value to set in the current context</param>
        void SetValueFor(IConstraint constraint, string name, object value);

        /// <summary>
        /// Gets a Context-Value for a specific constraint with a given name
        /// </summary>
        /// <param name="constraint"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        object GetValueFor(IConstraint constraint, string name);
    }
}
