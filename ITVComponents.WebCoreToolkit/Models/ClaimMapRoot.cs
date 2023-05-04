using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Models
{
    public class ClaimMapRoot:ClaimData
    {
        public ClaimMap ClaimMap { get; set; }

        public Type ClaimType => typeof(ClaimData);
    }
}
