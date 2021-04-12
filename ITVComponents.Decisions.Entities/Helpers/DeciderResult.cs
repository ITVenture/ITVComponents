using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Decisions.Entities.Helpers
{
    [Serializable]
    public class DeciderResult
    {
        public DecisionResult Result { get; set; }

        public string Messages { get; set; }
    }
}
