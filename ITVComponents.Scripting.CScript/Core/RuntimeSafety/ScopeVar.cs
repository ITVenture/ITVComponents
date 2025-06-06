using System;
using ITVComponents.ExtendedFormatting;

namespace ITVComponents.Scripting.CScript.Core.RuntimeSafety
{
    internal class ScopeVar
    {
        private Action<int> leaveLayer;
        private Action clear;
        private object value;
        private SmartProperty smartValue;
        private bool isSmart;

        public ScopeVar()
        {
            Layer = 0;
            Revision = -1;
        }

        public int Layer { get; set; }

        public int Revision { get; set; } 

        public object Value
        {
            get
            {
                return !isSmart?value:smartValue.Value;
            }
            set
            {
                if (!isSmart)
                {
                    this.value = value;
                    smartValue = value as SmartProperty;
                }
                else
                {
                    smartValue.Value = value;
                }
                isSmart = smartValue != null;
            }
        }

        public SmartProperty GetSmartProperty()
        {
            SmartProperty retVal = null;
            if (isSmart)
            {
                retVal = smartValue;
            }

            return retVal;
        }
    }
}
