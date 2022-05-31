using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AnonymousAssetAccess.Models;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.AnonymousAssetAccess
{
    public interface IGetAnonymousAssetQuery
    {
        AnonymousAsset Execute(IQueryCollection requestQuery, out bool denied);
    }
}
