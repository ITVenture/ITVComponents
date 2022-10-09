using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.InterProcessExtensions.JwtAuth.Config
{
    [Serializable]
    public class JwtAuthConfigCollection:List<JwtAuthConfig>
    {
        public JwtAuthConfig this[string name] =>
            this.FirstOrDefault(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
