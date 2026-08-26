using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.Security.AssetLevelImpersonation
{
    internal class DefaultAssetImpersonator:IImpersonationControl
    {
        private IPermissionScope permissionScope;
        private readonly IHttpContextUserProvider userProvider;
        private readonly ISharedAssetAdapter assetAdapter;
        private SecurityRepository securityRepository;
        public DefaultAssetImpersonator(IPermissionScope permissionScope, ISecurityRepository securityRepository, IHttpContextUserProvider userProvider, ISharedAssetAdapter assetAdapter)
        {
            this.permissionScope = permissionScope;
            this.userProvider = userProvider;
            this.assetAdapter = assetAdapter;
            if (securityRepository is not SecurityRepository srep)
            {
                throw new InvalidOperationException(
                    "SecurityRepository is required to make this work! Use GetAssetSecurityRepository in your ISecurityRepository dependency injection call.");
            }

            this.securityRepository = srep;
        }
        public IDisposable AsRealUser() => new DeImpersonator(this);

        public IDisposable AsAssetAccessor(string assetKey)
        {
            var user = userProvider.User;
            var tmp = assetAdapter.GetAssetInfo(assetKey, user);
            // Der Referer ist ein Rueckfall fuer die abgekuendigte Query-Form und kann fehlen - ohne die
            // Null-Probe wirft der zweite Zweig genau dann, wenn der erste nicht getroffen hat.
            var refer = userProvider.HttpContext?.Request.GetTypedHeaders().Referer;
            if (tmp != null && (assetAdapter.VerifyRequestLocation(userProvider.RequestPath, assetKey,
                                    tmp.UserScopeName, user)
                                || (refer != null && assetAdapter.VerifyRequestLocation(refer.LocalPath, assetKey,
                                    tmp.UserScopeName, user))))
            {
                securityRepository.PushRepo(new AssetSecurityRepository(user, securityRepository.Current, tmp));
                permissionScope.PushScope(tmp.UserScopeName);
                return new Impersonator(this);
            }

            throw new InvalidOperationException("Share not found!");
        }

        private class DeImpersonator:IDisposable
        {
            private readonly DefaultAssetImpersonator imp;

            public DeImpersonator(DefaultAssetImpersonator imp)
            {
                this.imp = imp;
                imp.permissionScope.SetImpersonationOff();
                imp.securityRepository.UseRoot();
                imp.assetAdapter.SetImpersonationOff();
                //imp.securityRepository
            }

            public void Dispose()
            {
                imp.permissionScope.SetImpersonationOn();
                imp.securityRepository.ResetRoot();
                imp.assetAdapter.SetImpersonationOn();
            }
        }

        private class Impersonator : IDisposable
        {
            private readonly DefaultAssetImpersonator imp;

            public Impersonator(DefaultAssetImpersonator imp)
            {
                this.imp = imp;
            }

            public void Dispose()
            {
                imp.permissionScope.PopScope();
                imp.securityRepository.PopRepo().Dispose();
            }
        }
    }
}
