using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base
{
    [Index(nameof(DiagnosticsQueryName), IsUnique = true, Name = "IX_DiagnosticsQueryUniqueness")]
    public abstract class DiagnosticsQuery<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery>
        where TRole : Role<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TPermission : Permission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TUserRole : UserRole<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TRolePermission : RolePermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TTenantUser : TenantUser<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TQuery:DiagnosticsQuery<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery>
        where TTenantQuery: TenantDiagnosticsQuery<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery>
        where TQueryParameter : DiagnosticsQueryParameter<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery>
    {
        public DiagnosticsQuery()
        {
        }

        [Key] public int DiagnosticsQueryId { get; set; }

        [Required, MaxLength(128)] public string DiagnosticsQueryName { get; set; }

        [Required, MaxLength(128)] public string DbContext { get; set; }

        public bool AutoReturn { get; set; }

        public string QueryText { get; set; }

        public int PermissionId { get; set; }

        [ForeignKey(nameof(PermissionId))] public virtual TPermission Permission { get; set; }

        public virtual ICollection<TQueryParameter> Parameters { get;set; } = new List<TQueryParameter>();

        public virtual ICollection<TTenantQuery> Tenants { get; set; } = new List<TTenantQuery>();
    }
}
