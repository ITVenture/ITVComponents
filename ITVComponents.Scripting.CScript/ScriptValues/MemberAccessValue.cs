using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using ITVComponents.Scripting.CScript.Core.Literals;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Scripting.CScript.Optimization;
using ValueType = ITVComponents.Scripting.CScript.Helpers.ValueType;

namespace ITVComponents.Scripting.CScript.ScriptValues
{
    public class MemberAccessValue:ScriptValue
    {
        /// <summary>
        /// the base value on which the defined member should be defined
        /// </summary>
        private ScriptValue baseValue;

        /// <summary>
        /// the explicit type that is used for this MemberAccess-value
        /// </summary>
        private Type explicitType;

        /// <summary>
        /// the name of the member that is represented by this scriptvalue
        /// </summary>
        private string memberName;

        /// <summary>
        /// Initializes a new instance of the MemberAccessValue class
        /// </summary>
        /// <param name="handler">the handler that is used to lock/unlock this value</param>
        public MemberAccessValue(IScriptSymbol creator, bool bypassCompatibilityOnLazyInvokation) :base(creator, bypassCompatibilityOnLazyInvokation) 
        {
        }

        /// <summary>
        /// Initialiezs a new instance of the MemberAccessValue class
        /// </summary>
        /// <param name="baseValue"></param>
        /// <param name="memberName"></param>
        public void Initialize(ScriptValue baseValue, string memberName, Type explicitType)
        {
            this.baseValue = baseValue;
            this.memberName = memberName;
            this.explicitType = explicitType;
            ValueType = ValueType.PropertyOrField;
        }

        #region Overrides of ScriptValue

        protected override Type ExplicitType => explicitType;

        #endregion

        /// <summary>
        /// Gets a value indicating whether this ScriptValue is Writable
        /// </summary>
        public override bool Writable
        {
            get { return true; }
        }

        /// <summary>
        /// Gets a value indicating whether this ScriptValue is Getable
        /// </summary>
        public override bool Getable
        {
            get { return true; }
        }

        /// <summary>
        /// Gets the baseValue of this MemberAccess Value object
        /// </summary>
        protected object BaseValue { get { return baseValue.GetValue(null); } }

        /// <summary>
        /// Gets the Value of this ScriptValue
        /// </summary>
        protected override object Value
        {
            get
            {
                object baseVal = baseValue.GetValue(null);
                return baseVal.GetMemberValue(Name, explicitType, ValueType);
            }
        }

        /// <summary>
        /// Gets the Value Type of this ScriptValue
        /// </summary>
        public override sealed ValueType ValueType { get; set; }

        #region Overrides of ScriptValue

        public override void Dispose()
        {
            base.Dispose();
            baseValue = null;
            explicitType = null;
            memberName = null;
        }

        #endregion

        /// <summary>
        /// The Name of the Target object. This is only required for Methods
        /// </summary>
        protected override string Name
        {
            get { return memberName; }
        }

        /// <summary>
        /// Sets the Value of this ScriptValue object
        /// </summary>
        /// <param name="value">the new Value to assign to this Value</param>
        public override void SetValue(object value)
        {
            if (ValueType != ValueType.PropertyOrField)
            {
                throw new ScriptException("Unable to set the value of something else than Property or Field");
            }

            object target = baseValue.GetValue(null);
            target.SetMemberValue(Name, value, explicitType, ValueType);
        }
    }
}
