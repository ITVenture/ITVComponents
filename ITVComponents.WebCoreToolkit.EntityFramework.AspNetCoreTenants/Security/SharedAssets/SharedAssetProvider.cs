using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Security.SharedAssets
{
    public class SharedAssetProvider:SharedAssetProvider<AspNetSecurityContext>
    {
        public SharedAssetProvider(IUserNameMapper userNameMapper, ISecurityRepository securityRepo, AspNetSecurityContext database, IServiceProvider services) : base(userNameMapper, securityRepo, database, services)
        {
        }
    }
}
