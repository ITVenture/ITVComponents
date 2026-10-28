using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;

/// <summary>
/// Eigener Schluessel fuer einen Mandanten, der in der Admin-Maske angelegt wird - wie bei den Onboarding-Handlern.
/// </summary>
internal static class TenantPasswordAssignment
{
    /// <summary>
    /// Setzt <see cref="Tenant.TenantPassword"/> eines neuen Mandanten, wenn er noch keinen hat und <paramref name="generate"/>
    /// gesetzt ist. Ein vorhandener Schluessel bleibt unangetastet.
    /// </summary>
    public static void EnsureForNewTenant(Tenant tenant, bool generate)
    {
        if (generate && string.IsNullOrEmpty(tenant.TenantPassword))
        {
            tenant.TenantPassword = Convert.ToBase64String(AesEncryptor.CreateKey());
        }
    }
}
