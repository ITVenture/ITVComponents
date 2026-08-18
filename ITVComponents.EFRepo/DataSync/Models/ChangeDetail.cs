using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.EFRepo.DataSync.Models
{
    public class ChangeDetail
    {
        public string Name { get; set; }

        public string TargetProp { get; set; }

        public string CurrentValue { get; set; }

        public string NewValue { get; set; }

        public string ValueExpression { get; set; }

        public bool MultilineContent { get; set; }

        public bool Apply { get; set; } = true;

        /// <summary>
        /// What the review dialog shows instead of <see cref="NewValue"/>, when the raw value is not something a
        /// human can read or edit — a base64 blob, a very large text. <see cref="NewValue"/> stays the payload
        /// that is actually applied, so the apply engine is unaffected. Null (the default) keeps the previous
        /// behaviour of showing the value itself.
        /// </summary>
        public string DisplayValue { get; set; }

        /// <summary>Same as <see cref="DisplayValue"/> for the current-value column.</summary>
        public string DisplayCurrentValue { get; set; }

        /// <summary>
        /// Locks the value against editing in the review dialog. Mandatory wherever <see cref="NewValue"/> carries
        /// a payload rather than a readable value: a single keystroke in the field would otherwise destroy it.
        /// </summary>
        public bool ReadOnly { get; set; }
    }
}
