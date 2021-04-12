using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Decisions
{
    public class SimpleContext<T>:IDeciderContext<T>, IFlushableContext where T:class
    {
        private Dictionary<IConstraint<T>, Dictionary<string, object>> contextData = new Dictionary<IConstraint<T>, Dictionary<string, object>>();

        #region Implementation of IDeciderContext<T>

        /// <summary>
        /// Sets a Context-Value for a specific constraint with a given name
        /// </summary>
        /// <param name="constraint">the constraint for which to set the value</param>
        /// <param name="name">the name of the value that is set</param>
        /// <param name="value">the value to set in the current context</param>
        public void SetValueFor(IConstraint<T> constraint, string name, object value)
        {
            Dictionary<string, object> targetDictionary = FindDictionary(constraint);
            targetDictionary[name] = value;
        }

        /// <summary>
        /// Gets a Context-Value for a specific constraint with a given name
        /// </summary>
        /// <param name="constraint"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public object GetValueFor(IConstraint<T> constraint, string name)
        {
            Dictionary<string, object> targetDictionary = FindDictionary(constraint);
            object retVal = null;
            if (targetDictionary.ContainsKey(name))
            {
                retVal = targetDictionary[name];
            }

            return retVal;
        }

        /// <summary>
        /// Sets a Context-Value for a specific constraint with a given name
        /// </summary>
        /// <param name="constraint">the constraint for which to set the value</param>
        /// <param name="name">the name of the value that is set</param>
        /// <param name="value">the value to set in the current context</param>
        void IDeciderContext.SetValueFor(IConstraint constraint, string name, object value)
        {
            SetValueFor((IConstraint<T>) constraint, name, value);
        }

        /// <summary>
        /// Gets a Context-Value for a specific constraint with a given name
        /// </summary>
        /// <param name="constraint"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        object IDeciderContext.GetValueFor(IConstraint constraint, string name)
        {
            return GetValueFor((IConstraint<T>) constraint, name);
        }

        /// <summary>
        /// Finds the Object-dictionary for a specific constraint
        /// </summary>
        /// <param name="constraint">the constraint that is requesting a context-value</param>
        /// <returns>a dictionary that is holding the context-information for the given constraint</returns>
        private Dictionary<string, object> FindDictionary(IConstraint<T> constraint)
        {
            if (!contextData.ContainsKey(constraint))
            {
                contextData.Add(constraint, new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase));
            }

            return contextData[constraint];
        } 

        #endregion

        #region Implementation of IFlushableContext

        /// <summary>
        /// Flushes all settings in the current context
        /// </summary>
        void IFlushableContext.Flush()
        {
            foreach (KeyValuePair<IConstraint<T>, Dictionary<string, object>> item in contextData)
            {
                foreach (KeyValuePair<string, object> dataItem in item.Value)
                {
                    IDisposable disposable = dataItem.Value as IDisposable;
                    disposable?.Dispose();
                }

                item.Value.Clear();
            }

            contextData.Clear();
        }

        #endregion
    }
}
