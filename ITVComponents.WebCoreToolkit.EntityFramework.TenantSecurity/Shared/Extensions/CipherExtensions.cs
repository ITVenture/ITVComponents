using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Json;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Extensions
{
    public static class CipherExtensions
    {
        public static byte[] GetEncryptionKey<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>(
                this IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> context,
                string permissionScopeName, Func<TTrustConfig, TTrustConfig> configureTrust)
            where TTenant : Tenant
            where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
            where TWebPluginConstant : WebPluginConstant<TTenant>
            where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
            where TSequence : Sequence<TTenant>
            where TTenantSetting : TenantSetting<TTenant>
            where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
            where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
            where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
            where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
            where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        {
            using (var h = new FullSecurityAccessHelper<TTrustConfig>(context, configureTrust(new() { ShowAllTenants = true, HideGlobals = true })))
            {
                var t = context.Tenants.First(n => n.TenantName == permissionScopeName);
                if (!string.IsNullOrEmpty(t.TenantPassword))
                {
                    return Convert.FromBase64String(t.TenantPassword);
                }
            }

            return null;
        }

        public static string DecryptForScope<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>(
            this IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> context,
            string encryptedValue, string permissionScopeName, Func<TTrustConfig, TTrustConfig> configureTrust)
            where TTenant : Tenant
            where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
            where TWebPluginConstant : WebPluginConstant<TTenant>
            where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
            where TSequence : Sequence<TTenant>
            where TTenantSetting : TenantSetting<TTenant>
            where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
            where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
            where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
            where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
            where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        {
            byte[] passwd = null;
            if (!string.IsNullOrEmpty(permissionScopeName))
            {
                passwd = context.GetEncryptionKey(permissionScopeName, configureTrust);
            }

            if (passwd != null)
            {
                return AesEncryptor.Decrypt(encryptedValue, passwd);
            }

            return encryptedValue.Decrypt();
        }

        public static byte[] DecryptForScope<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>(
            this IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> context,
            byte[] encryptedValue, string permissionScopeName, Func<TTrustConfig, TTrustConfig> configureTrust)
            where TTenant : Tenant
            where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
            where TWebPluginConstant : WebPluginConstant<TTenant>
            where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
            where TSequence : Sequence<TTenant>
            where TTenantSetting : TenantSetting<TTenant>
            where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
            where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
            where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
            where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
            where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        {
            byte[] passwd = null;
            if (!string.IsNullOrEmpty(permissionScopeName))
            {
                passwd = context.GetEncryptionKey(permissionScopeName, configureTrust);
            }

            if (passwd != null)
            {
                return AesEncryptor.Decrypt(encryptedValue, passwd);
            }

            return encryptedValue.Decrypt();
        }

        public static byte[] DecryptForScope<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>(
            this IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> context,
            byte[] encryptedValue, string permissionScopeName, byte[] iv, byte[] salt, Func<TTrustConfig, TTrustConfig> configureTrust)
            where TTenant : Tenant
            where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
            where TWebPluginConstant : WebPluginConstant<TTenant>
            where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
            where TSequence : Sequence<TTenant>
            where TTenantSetting : TenantSetting<TTenant>
            where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
            where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
            where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
            where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
            where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        {
            byte[] passwd = null;
            if (!string.IsNullOrEmpty(permissionScopeName))
            {
                passwd = context.GetEncryptionKey(permissionScopeName, configureTrust);
            }

            if (passwd != null)
            {
                return AesEncryptor.Decrypt(encryptedValue, passwd, iv, salt);
            }

            throw new InvalidOperationException("This is only supported for explicit tenant-encryption");
        }

        public static byte[] EncryptForScope<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>(
            this IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> context,
            byte[] value, string permissionScopeName, out byte[] iv, out byte[] salt, Func<TTrustConfig, TTrustConfig> configureTrust)
            where TTenant : Tenant
            where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
            where TWebPluginConstant : WebPluginConstant<TTenant>
            where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
            where TSequence : Sequence<TTenant>
            where TTenantSetting : TenantSetting<TTenant>
            where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
            where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
            where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
            where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
            where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        {
            byte[] passwd = null;
            if (!string.IsNullOrEmpty(permissionScopeName))
            {
                passwd = context.GetEncryptionKey(permissionScopeName, configureTrust);
            }

            if (passwd != null)
            {
                return AesEncryptor.Encrypt(value, passwd, out iv, out salt);
            }

            throw new InvalidOperationException("This is only supported for explicit tenant-encryption");
        }

        public static byte[] EncryptForScope<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>(
            this IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> context,
            byte[] value, string permissionScopeName, Func<TTrustConfig, TTrustConfig> configureTrust)
            where TTenant : Tenant
            where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
            where TWebPluginConstant : WebPluginConstant<TTenant>
            where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
            where TSequence : Sequence<TTenant>
            where TTenantSetting : TenantSetting<TTenant>
            where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
            where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
            where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
            where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
            where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        {
            byte[] passwd = null;
            if (!string.IsNullOrEmpty(permissionScopeName))
            {
                passwd = context.GetEncryptionKey(permissionScopeName, configureTrust);
            }

            if (passwd != null)
            {
                return AesEncryptor.Encrypt(value, passwd);
            }

            return value.Encrypt();
        }

        public static string EncryptForScope<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>(
            this IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> context,
            string value, string permissionScopeName, Func<TTrustConfig, TTrustConfig> configureTrust)
            where TTenant : Tenant
            where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
            where TWebPluginConstant : WebPluginConstant<TTenant>
            where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
            where TSequence : Sequence<TTenant>
            where TTenantSetting : TenantSetting<TTenant>
            where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
            where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
            where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
            where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
            where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        {
            byte[] passwd = null;
            if (!string.IsNullOrEmpty(permissionScopeName))
            {
                passwd = context.GetEncryptionKey(permissionScopeName, configureTrust);
            }

            if (passwd != null)
            {
                return AesEncryptor.Encrypt(value, passwd);
            }

            return value.Encrypt();
        }

        public static string EncryptJsonObjectForScope<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>(
            this IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> context,
            object value, string permissionScopeName, Func<TTrustConfig, TTrustConfig> configureTrust)
            where TTenant : Tenant
            where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
            where TWebPluginConstant : WebPluginConstant<TTenant>
            where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
            where TSequence : Sequence<TTenant>
            where TTenantSetting : TenantSetting<TTenant>
            where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
            where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
            where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
            where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
            where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        {
            byte[] passwd = null;
            if (!string.IsNullOrEmpty(permissionScopeName))
            {
                passwd = context.GetEncryptionKey(permissionScopeName, configureTrust);
            }

            if (passwd != null)
            {
                return value.EncryptJsonValues(passwd);
            }

            throw new InvalidOperationException("This is only supported for explicit tenant-encryption");
        }

        public static Stream GetEncryptStreamForScope<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>(
            this IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> context,
            Stream baseStream, string permissionScopeName, out byte[] iv,
            out byte[] salt, Func<TTrustConfig, TTrustConfig> configureTrust)
            where TTenant : Tenant
            where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
            where TWebPluginConstant : WebPluginConstant<TTenant>
            where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
            where TSequence : Sequence<TTenant>
            where TTenantSetting : TenantSetting<TTenant>
            where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
            where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
            where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
            where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
            where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        {
            byte[] passwd = null;
            if (!string.IsNullOrEmpty(permissionScopeName))
            {
                passwd = context.GetEncryptionKey(permissionScopeName, configureTrust);
            }

            if (passwd != null)
            {
                return AesEncryptor.GetEncryptStream(baseStream, passwd, out iv, out salt);
            }

            throw new InvalidOperationException("This is only supported for explicit tenant-encryption");
        }

        public static Stream GetEncryptStreamForScope<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>(
            this IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> context,
            Stream baseStream, string permissionScopeName, Func<TTrustConfig, TTrustConfig> configureTrust)
            where TTenant : Tenant
            where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
            where TWebPluginConstant : WebPluginConstant<TTenant>
            where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
            where TSequence : Sequence<TTenant>
            where TTenantSetting : TenantSetting<TTenant>
            where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
            where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
            where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
            where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
            where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        {
            byte[] passwd = null;
            if (!string.IsNullOrEmpty(permissionScopeName))
            {
                passwd = context.GetEncryptionKey(permissionScopeName, configureTrust);
            }

            if (passwd != null)
            {
                return AesEncryptor.GetEncryptStream(baseStream, passwd);
            }

            throw new InvalidOperationException("This is only supported for explicit tenant-encryption");
        }

        //dec

        public static Stream GetDecryptStreamForScope<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>(
            this IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> context,
            Stream baseStream, string permissionScopeName, byte[] iv,
            byte[] salt, Func<TTrustConfig, TTrustConfig> configureTrust)
            where TTenant : Tenant
            where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
            where TWebPluginConstant : WebPluginConstant<TTenant>
            where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
            where TSequence : Sequence<TTenant>
            where TTenantSetting : TenantSetting<TTenant>
            where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
            where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
            where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
            where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
            where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        {
            byte[] passwd = null;
            if (!string.IsNullOrEmpty(permissionScopeName))
            {
                passwd = context.GetEncryptionKey(permissionScopeName, configureTrust);
            }

            if (passwd != null)
            {
                return AesEncryptor.GetDecryptStream(baseStream, passwd, iv, salt);
            }

            throw new InvalidOperationException("This is only supported for explicit tenant-encryption");
        }

        public static Stream GetDecryptStreamForScope<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>(
            this IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> context,
            Stream baseStream, string permissionScopeName, Func<TTrustConfig, TTrustConfig> configureTrust)
            where TTenant : Tenant
            where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
            where TWebPluginConstant : WebPluginConstant<TTenant>
            where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
            where TSequence : Sequence<TTenant>
            where TTenantSetting : TenantSetting<TTenant>
            where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
            where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
            where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
            where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
            where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        {
            byte[] passwd = null;
            if (!string.IsNullOrEmpty(permissionScopeName))
            {
                passwd = context.GetEncryptionKey(permissionScopeName, configureTrust);
            }

            if (passwd != null)
            {
                return AesEncryptor.GetDecryptStream(baseStream, passwd);
            }

            throw new InvalidOperationException("This is only supported for explicit tenant-encryption");
        }
    }
}
