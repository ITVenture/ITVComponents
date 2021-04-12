using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ITVComponents.DataExchange.Import;
using ITVComponents.Decisions;

namespace ITVComponents.DataExchange.TextImport
{
    public class TextAcceptanceConstraint:IConstraint<string>
    {
        private string linePattern;

        /// <summary>
        /// the parent object of this decider
        /// </summary>
        private IDecider<string> parent;

        /// <summary>
        /// Initializes a new instance of the TextAcceptanceConstraint class
        /// </summary>
        /// <param name="requiredLines"></param>
        public TextAcceptanceConstraint(int requiredLines)
        {
            RequiredLines = requiredLines;
            linePattern = string.Format(@"^(.*\r\n){{{0}}}$", requiredLines);
        }

        /// <summary>
        /// Gets a value indicating how many textlines are required in order to match a pattern
        /// </summary>
        public int RequiredLines { get; }

        public void SetParent(IDecider<string> parent)
        {
            if (this.parent != null)
            {
                throw new InvalidOperationException("SetParent can only be called once!");
            }

            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            this.parent = parent;
        }

        #region Implementation of IAcceptanceConstraint<string>

        /// <summary>
        /// Verifies the provided input
        /// </summary>
        /// <param name="data">the data that was provided by a source</param>
        /// <param name="message">the message that was generated during the validation of this constraint</param>
        /// <returns>a value indicating whether the data fullfills the requirements of the underlaying Requestor</returns>
        public DecisionResult Verify(string data, out string message)
        {
            message = null;
            return Regex.IsMatch(data, linePattern,
                RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline)
                ? DecisionResult.Success
                : DecisionResult.Fail;
        }

        void IConstraint.SetParent(IDecider parent)
        {
            SetParent((IDecider<string>) parent);
        }

        /// <summary>
        /// Verifies the provided input
        /// </summary>
        /// <param name="data">the data that was provided by a source</param>
        /// <param name="message">the message that was generated during the validation of this constraint</param>
        /// <returns>a value indicating whether the data fullfills the requirements of the underlaying Requestor</returns>
        DecisionResult IConstraint.Verify(object data, out string message)
        {
            return Verify((string) data, out message);
        }

        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            return $"Accepts input Textblocks of {RequiredLines} Line(s)";
        }

        #endregion
    }
}
