using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Extras.AnonymousAssetAccess.Models;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.Extras.AnonymousAssetAccess
{
    public interface IGetAnonymousAssetQuery
    {
        AnonymousAsset Execute(IQueryCollection requestQuery, out bool denied);
    }
}
