﻿using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Optimization;

namespace ITVComponents.Scripting.CScript.ScriptValues
{
    public class WeakReferenceMemberAccessValue:MemberAccessValue
    {
        /// <summary>
        /// Initializes a new instance of the WeakReferenceMemberAccessValue class
        /// </summary>
        /// <param name="handler">the base handler that is used to lock/unlock items</param>
        public WeakReferenceMemberAccessValue(IScriptSymbol creator, bool bypassCompatibilityOnLazyInvokation) :base(creator, bypassCompatibilityOnLazyInvokation)
        {
        }

        #region Overrides of ScriptValue

        public override object GetValue(ScriptValue[] arguments)
        {
            if (BaseValue != null)
            {
                return base.GetValue(arguments);
            }

            return null;
        }

        #endregion

        /// <summary>
        /// Gets the Value of this ScriptValue
        /// </summary>
        protected override object Value
        {
            get
            {
                if (BaseValue != null)
                {
                    return base.Value;
                }

                return null;
            }
        }

        /// <summary>
        /// Sets the Value of this ScriptValue object
        /// </summary>
        /// <param name="value">the new Value to assign to this Value</param>
        internal override void SetValue(object value)
        {
            throw new ScriptException("SetValue not supported for Weak-Reference access");
        }
    }
}
