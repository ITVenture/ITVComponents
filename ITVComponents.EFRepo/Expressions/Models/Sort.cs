using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.EFRepo.Expressions.Models
{
    public class Sort
    {
        public string MemberName { get; set; }

        public SortDirection Direction { get; set; }
    }

    public enum SortDirection
    {
        Ascending,
        Descending
    }
}
