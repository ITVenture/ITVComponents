using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.HelperModels
{
    internal class FormatHelperInfo
    {
        public string Name { get; set; }

        public string Value { get; set; }

        public string EncryptedTenantPassword { get; set; }

        public bool IsPublic { get; set; }
    }
}
