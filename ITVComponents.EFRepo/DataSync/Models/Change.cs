using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.EFRepo.DataSync.Models
{
    public class Change
    {
        public string EntityName { get; set; }
        public Dictionary<string,string> Key { get; set; }
        public Dictionary<string,string> KeyExpression { get; set; }
        public ChangeType ChangeType { get; set; }

        public List<ChangeDetail> Details { get; set; } = new List<ChangeDetail>();

        public bool Apply { get; set; }
    }

    public enum ChangeType
    {
        Insert,
        Update,
        Delete,
        Info,
        Warning
    }
}
