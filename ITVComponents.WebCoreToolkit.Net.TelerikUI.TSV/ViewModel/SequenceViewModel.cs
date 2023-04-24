using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class SequenceViewModel
    {
        public int SequenceId { get; set; }

        [MaxLength(300), Required]
        public string SequenceName { get; set; }

        public int MinValue { get; set; } = -1;

        public int MaxValue { get; set; } = int.MaxValue;

        public bool Cycle { get; set; } = false;

        public int StepSize { get; set; } = 1;
    }
}
