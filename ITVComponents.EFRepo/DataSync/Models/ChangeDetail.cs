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
    }
}
