using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.EFRepo.DynamicData
{
    public class DynamicDataColumnType
    {
        public string DataTypeName { get;set; }

        public string DisplayName { get; set; }

        public bool LengthRequired { get;set; }
        
        public Type ManagedType { get; set; }
    }
}
