using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.EFRepo.DynamicData
{
    public class AliasQualifiedColumn:TableColumnDefinition
    {
        public string FullQualifiedName { get; set; }
    }
}