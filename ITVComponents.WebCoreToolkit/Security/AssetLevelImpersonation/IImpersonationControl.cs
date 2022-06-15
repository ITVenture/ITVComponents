using System;

namespace ITVComponents.WebCoreToolkit.Security.AssetLevelImpersonation
{
    public interface IImpersonationControl
    {
        IDisposable AsRealUser();

        IDisposable AsAssetAccessor(string assetKey);
    }
}
