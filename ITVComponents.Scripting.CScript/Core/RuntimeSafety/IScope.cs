using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
#if !Community
using ITVComponents.ExtendedFormatting;
#endif

namespace ITVComponents.Scripting.CScript.Core.RuntimeSafety
{
    public interface IScope:IDictionary<string,object>
    {
        /// <summary>
        /// Gets the value of the specified variable name
        /// </summary>
        /// <param name="memberName">the variable for which to check</param>
        /// <param name="rootOnly">indicates whether to check only in the initial scope</param>
        /// <returns>the value of the requeted variable or null if it does not exist</returns>
        object this[string memberName, bool rootOnly] { get; }

        //object this[string memberName] { get; set; }
#if !Community
        /// <summary>
        /// Gets the Smart-Property that is bound to the given Variable name if defined
        /// </summary>
        /// <param name="name">the variable that is expected to be a smart-property</param>
        /// <param name="rootOnly">indicates whether to check only in the initial scope</param>
        /// <returns>the smart-property or null, that is associated with the given name</returns>
        SmartProperty GetSmartProperty(string name, bool rootOnly = false);
#endif


        /// <summary>
        /// Gets a value indicating whether a specific key exists in the current scope
        /// </summary>
        /// <param name="key">the name for which to check the current scope</param>
        /// <param name="rootOnly">indicates whether to check only the base-scope</param>
        /// <returns>a value indicating whether the scope contains the requested value</returns>
        bool ContainsKey(string key, bool rootOnly);

        /// <summary>
        /// Copies the initial root of the current scope
        /// </summary>
        /// <returns></returns>
        Dictionary<string, object> CopyInitial();

        /// <summary>
        /// Creates a Snapshot of the current Scope
        /// </summary>
        /// <returns>a dictionary that contains all variables that are in the current scope</returns>
        Dictionary<string, object> Snapshot();

        /// <summary>
        /// Opens an inner scope
        /// </summary>
        void OpenInnerScope();

        /// <summary>
        /// Collapses an inner scope
        /// </summary>
        void CollapseScope();

        /// <summary>
        /// Clears all elements and initializes this scope with new root values
        /// </summary>
        /// <param name="rootVariables">the root variables to put on the scope</param>
        void Clear(IDictionary<string, object> rootVariables);
    }
}
