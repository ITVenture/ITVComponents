using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Health
{
    public interface IServices
    {
        T GetService<T>();

        IList<T> GetServices<T>();
    }
}
