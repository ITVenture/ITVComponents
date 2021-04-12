using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Models
{
    public class UserScope
    {
        public DateTime Created { get; set; } = DateTime.Now;

        public string ScopeName { get; set; }
    }
}
