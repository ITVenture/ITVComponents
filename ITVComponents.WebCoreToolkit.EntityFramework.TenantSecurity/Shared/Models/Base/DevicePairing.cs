using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.Security.DevicePairing;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base
{
    /// <summary>
    /// Ein laufender Kopplungsvorgang: das Geraet fragt einen Code an, ein angemeldeter Benutzer
    /// bestaetigt ihn, das Geraet holt daraufhin sein Geheimnis ab.
    /// </summary>
    /// <remarks>
    /// Die einzige wirklich neue Tabelle des Umbaus. Sie haelt nur die Zwischenzustaende - das Ergebnis
    /// einer erfolgreichen Kopplung ist ein <see cref="ClientAppAccess{TTenant,TUserId,TUser,TRole,TPermission,TUserRole,TRolePermission,TTenantUser,TRoleRole,TGlobalRole,TGlobalRolePermission,TGRoleLRole,TAppPermission,TAppPermissionSet,TClientAppPermission,TClientApp,TClientAppAccess}"/>,
    /// und der lebt weiter, wenn dieser Eintrag laengst abgeraeumt ist.
    /// </remarks>
    [Index(nameof(DeviceCodeHash), IsUnique = true, Name = "UQ_DevicePairingDeviceCode")]
    [Index(nameof(UserCode), Name = "IX_DevicePairingUserCode")]
    public class DevicePairing<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess, TClientAppTemplate>
        where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TTenantUser : TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TAppPermission : AppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate>
        where TAppPermissionSet : AppPermissionSet<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate>
        where TClientAppPermission : ClientAppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess, TClientAppTemplate>
        where TClientApp : ClientApp<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess, TClientAppTemplate>
        where TClientAppAccess : ClientAppAccess<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess, TClientAppTemplate>
        where TTenant : Tenant
        where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGlobalRole : GlobalRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGlobalRolePermission : GlobalRolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGRoleLRole : GRoleLRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    {
        [Key]
        public int DevicePairingId { get; set; }

        public int TenantId { get; set; }

        /// <summary>Die Anwendung, fuer die gekoppelt wird - sie bringt die Rechtebuendel mit.</summary>
        public int ClientAppId { get; set; }

        /// <summary>
        /// Der Hash des Geraetecodes. <b>Der Geraetecode ist das Geheimnis dieses Vorgangs</b> - er
        /// verlaesst den Agenten nie, und deshalb steht hier nur sein Hash. Wer die Tabelle lesen kann,
        /// kann damit keinen fremden Vorgang abholen.
        /// </summary>
        [Required, MaxLength(512)]
        public string DeviceCodeHash { get; set; }

        /// <summary>
        /// Der Code, den der Benutzer abtippt. <b>Kein Geheimnis</b> - er darf auf einem Bildschirm im
        /// Laden stehen; allein bringt er niemanden weiter, weil zum Abholen der Geraetecode noetig ist.
        /// </summary>
        /// <remarks>
        /// Meidet verwechselbare Zeichen (kein 0/O, kein 1/I/l): er wird vorgelesen und abgetippt, oft
        /// von jemandem, der nebenbei Kunden bedient.
        /// </remarks>
        [Required, MaxLength(32)]
        public string UserCode { get; set; }

        /// <summary>Wie sich das Geraet nennt. Landet bei Erfolg am Zugang.</summary>
        [MaxLength(200)]
        public string DeviceLabel { get; set; }

        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Ablauf des Vorgangs. <b>Pflicht, nicht Option</b> - ein Kopplungsvorgang, der offen bleibt,
        /// ist ein dauerhaft gueltiger Einstieg.
        /// </summary>
        public DateTime ExpiresUtc { get; set; }

        public DevicePairingState State { get; set; }

        /// <summary>Wer bestaetigt hat. Fuer die Nachvollziehbarkeit, nicht fuer die Rechte.</summary>
        public int? ConfirmedByTenantUserId { get; set; }

        public DateTime? ConfirmedUtc { get; set; }

        /// <summary>Der bei der Bestaetigung angelegte Zugang.</summary>
        public int? ClientAppAccessId { get; set; }

        /// <summary>
        /// Wann das Geheimnis abgeholt wurde. Gesetzt heisst: <b>nie wieder herausgeben</b>, nur noch den
        /// Zustand melden.
        /// </summary>
        public DateTime? SecretDeliveredUtc { get; set; }

        /// <summary>
        /// Zaehler der Abfragen. Zusammen mit <see cref="LastPollUtc"/> die Bremse - ohne sie waere der
        /// kurze Benutzercode ratbar.
        /// </summary>
        public int PollCount { get; set; }

        public DateTime? LastPollUtc { get; set; }

        [ForeignKey(nameof(TenantId))]
        public virtual TTenant Tenant { get; set; }

        [ForeignKey(nameof(ClientAppId))]
        public virtual TClientApp ClientApp { get; set; }

        [ForeignKey(nameof(ConfirmedByTenantUserId))]
        public virtual TTenantUser ConfirmedBy { get; set; }

        [ForeignKey(nameof(ClientAppAccessId))]
        public virtual TClientAppAccess ClientAppAccess { get; set; }
    }
}
