using System;
using System.Linq;
using ITVComponents.EFRepo.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Interceptors;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sec = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Test
{
    /// <summary>
    /// Nagelt fest, dass eine geerbte Rechte-Zeile GENAU EINMAL entsteht, egal ueber wie viele Wege der
    /// <c>SecurityModificationInterceptor</c> im selben Speichervorgang auf sie kommt.
    /// </summary>
    /// <remarks>
    /// Der Anlass ist ein realer Fehler: eine Vorlagen-Rolle mit <c>Permissions</c> UND <c>RoleGrants</c>
    /// liess beide Nachbearbeitungen dieselbe Ableitung erzeugen - gleiche RoleId, PermissionId, TenantId
    /// und gleiche OriginId, weil es dieselbe Quellzeile ist. Das ist ein Verstoss gegen
    /// <c>IX_UniqueRolePermission</c>, und weil alle Nachbearbeitungen EIN SaveChanges teilen, riss er die
    /// ganze Mandanten-Anlage mit.
    /// <para>
    /// Die zweite Haelfte der Tests ist die wichtigere: entdoppeln darf nicht heissen, dass eine noetige
    /// Ableitung ausbleibt. Jeder der beiden Einzelwege wird darum auch fuer sich geprueft.
    /// </para>
    /// <para>
    /// Anders als die uebrigen Tests dieses Projekts laeuft dieser NICHT auf SQLite und nicht auf einem
    /// handgebauten Kontext. Beides geht hier nicht: der Interceptor prueft jede neue Vererbung auf Zyklen,
    /// und diese Pruefung loest ihre Typen ueber die volle Schliessung des Sicherheits-Kontextes auf - ein
    /// verkleinerter Kontext wird dabei gar nicht erst erkannt. Und die Ableitungs-Abfragen brauchen
    /// APPLY / LATERAL, was SQLite nicht uebersetzt. Der echte Kontext im Arbeitsspeicher ist der Weg, der
    /// beides bedient; den eindeutigen Index ersetzt hier die Zaehlung in den Zusicherungen, die denselben
    /// Fehler ebenso sicher faengt.
    /// </para>
    /// </remarks>
    [TestClass]
    public class RoleInheritanceDuplicateTest
    {
        // --- Der gemeldete Fall ---------------------------------------------------------------

        [TestMethod]
        public void PermissionsAndRoleGrantsInOneSave_DeriveTheLinkExactlyOnce()
        {
            using var ctx = Context();
            int tenant = Tenant(ctx, "acme");
            var owner = Role(ctx, tenant, "TenantOwner");
            var employees = Role(ctx, tenant, "Employees");
            var permission = Permission(ctx, tenant, "Sales.Manage");

            // Das Vorlagen-Muster, an dem es zerbrach: die Rolle bekommt ihr Recht UND wird zur
            // permissiven Seite einer Vererbung - in EINEM SaveChanges.
            ctx.RolePermissions.Add(new Sec.RolePermission
            {
                RoleId = owner.RoleId, PermissionId = permission.PermissionId, TenantId = tenant
            });
            ctx.RoleRoles.Add(new Sec.RoleRole
            {
                PermissiveRoleId = owner.RoleId, PermittedRoleId = employees.RoleId
            });
            ctx.SaveChanges();

            var derived = Inherited(ctx, employees.RoleId);
            Assert.AreEqual(1, derived.Length,
                "beide Nachbearbeitungen kommen auf dieselbe Quellzeile - die Ableitung darf trotzdem nur " +
                "einmal entstehen, sonst ist es IX_UniqueRolePermission und die ganze Transaktion faellt.");
            Assert.AreEqual(permission.PermissionId, derived[0].PermissionId);
            Assert.IsNotNull(derived[0].OriginId,
                "die geerbte Zeile muss ihre Quelle kennen, sonst raeumt die Kaskade sie spaeter nicht ab.");
        }

        [TestMethod]
        public void TwoGrantsFromTheSameRoleInOneSave_EachHeirGetsItOnce()
        {
            using var ctx = Context();
            int tenant = Tenant(ctx, "acme");
            var owner = Role(ctx, tenant, "TenantOwner");
            var employees = Role(ctx, tenant, "Employees");
            var guests = Role(ctx, tenant, "Guests");
            var permission = Permission(ctx, tenant, "Sales.Manage");

            ctx.RolePermissions.Add(new Sec.RolePermission
            {
                RoleId = owner.RoleId, PermissionId = permission.PermissionId, TenantId = tenant
            });
            ctx.RoleRoles.Add(new Sec.RoleRole
            {
                PermissiveRoleId = owner.RoleId, PermittedRoleId = employees.RoleId
            });
            ctx.RoleRoles.Add(new Sec.RoleRole
            {
                PermissiveRoleId = owner.RoleId, PermittedRoleId = guests.RoleId
            });
            ctx.SaveChanges();

            Assert.AreEqual(1, Inherited(ctx, employees.RoleId).Length);
            Assert.AreEqual(1, Inherited(ctx, guests.RoleId).Length,
                "entdoppelt wird je Ziel-Rolle, nicht ueber alle - der zweite Erbe darf nicht leer ausgehen.");
        }

        // --- Die Einzelwege muessen weiter tragen ---------------------------------------------

        [TestMethod]
        public void APermissionAddedLater_ReachesTheRoleThatAlreadyInherits()
        {
            using var ctx = Context();
            int tenant = Tenant(ctx, "acme");
            var owner = Role(ctx, tenant, "TenantOwner");
            var employees = Role(ctx, tenant, "Employees");
            var permission = Permission(ctx, tenant, "Sales.Manage");

            // Erst die Vererbung, gespeichert ...
            ctx.RoleRoles.Add(new Sec.RoleRole
            {
                PermissiveRoleId = owner.RoleId, PermittedRoleId = employees.RoleId
            });
            ctx.SaveChanges();

            // ... dann, getrennt, das Recht. Das ist der Weg ueber ProcessPermissionInheritanceChanges.
            ctx.RolePermissions.Add(new Sec.RolePermission
            {
                RoleId = owner.RoleId, PermissionId = permission.PermissionId, TenantId = tenant
            });
            ctx.SaveChanges();

            Assert.AreEqual(1, Inherited(ctx, employees.RoleId).Length,
                "ein Recht, das zu einer bereits vererbenden Rolle kommt, muss beim Erben ankommen.");
        }

        [TestMethod]
        public void AGrantAddedLater_CarriesThePermissionsTheRoleAlreadyHad()
        {
            using var ctx = Context();
            int tenant = Tenant(ctx, "acme");
            var owner = Role(ctx, tenant, "TenantOwner");
            var employees = Role(ctx, tenant, "Employees");
            var permission = Permission(ctx, tenant, "Sales.Manage");

            // Erst das Recht, gespeichert ...
            ctx.RolePermissions.Add(new Sec.RolePermission
            {
                RoleId = owner.RoleId, PermissionId = permission.PermissionId, TenantId = tenant
            });
            ctx.SaveChanges();

            // ... dann, getrennt, die Vererbung. Das ist der Weg ueber ProcessRoleInheritanceChanges.
            ctx.RoleRoles.Add(new Sec.RoleRole
            {
                PermissiveRoleId = owner.RoleId, PermittedRoleId = employees.RoleId
            });
            ctx.SaveChanges();

            Assert.AreEqual(1, Inherited(ctx, employees.RoleId).Length,
                "eine neue Vererbung muss die Rechte mitnehmen, die die Rolle schon hatte.");
        }

        [TestMethod]
        public void TheHeirsOwnPermission_IsNotSwallowedByTheInheritedOne()
        {
            using var ctx = Context();
            int tenant = Tenant(ctx, "acme");
            var owner = Role(ctx, tenant, "TenantOwner");
            var employees = Role(ctx, tenant, "Employees");
            var permission = Permission(ctx, tenant, "Sales.Manage");

            // Dieselbe Berechtigung direkt UND geerbt: die beiden unterscheiden sich in der OriginId
            // (direkt = null), sind also zwei verschiedene Zeilen und duerfen einander nicht verdraengen.
            ctx.RolePermissions.Add(new Sec.RolePermission
            {
                RoleId = employees.RoleId, PermissionId = permission.PermissionId, TenantId = tenant
            });
            ctx.RolePermissions.Add(new Sec.RolePermission
            {
                RoleId = owner.RoleId, PermissionId = permission.PermissionId, TenantId = tenant
            });
            ctx.RoleRoles.Add(new Sec.RoleRole
            {
                PermissiveRoleId = owner.RoleId, PermittedRoleId = employees.RoleId
            });
            ctx.SaveChanges();

            var atTheHeir = ctx.RolePermissions.Where(n => n.RoleId == employees.RoleId).ToArray();
            Assert.AreEqual(2, atTheHeir.Length,
                "die Entdopplung geht ueber das Index-Tupel - und OriginId gehoert dazu.");
            Assert.AreEqual(1, atTheHeir.Count(n => n.OriginId == null), "die direkt vergebene Zeile.");
            Assert.AreEqual(1, atTheHeir.Count(n => n.OriginId != null), "die geerbte Zeile.");
        }

        // --- Testgeruest ----------------------------------------------------------------------

        private static SecurityTestContext Context()
        {
            // Der Interceptor haelt den ServiceProvider nur fest; fuer die Vererbungs-Nachbearbeitung
            // fragt er nichts daraus ab.
            var services = new ServiceCollection().BuildServiceProvider();
            var options = new DbContextOptionsBuilder<SecurityTestContext>()
                .UseInMemoryDatabase($"roleInheritance_{Guid.NewGuid():N}")
                .AddInterceptors(new SecurityModificationInterceptor<Tenant, int, Sec.User, Sec.Role,
                    Sec.Permission, Sec.UserRole, Sec.RolePermission, Sec.TenantUser, Sec.RoleRole,
                    Sec.GlobalRole, Sec.GlobalRolePermission, Sec.GRoleLRole>(services))
                .Options;
            return new SecurityTestContext(new DbContextModelBuilderOptions<SecurityTestContext>(), options);
        }

        /// <summary>Die Zeilen, die eine Rolle GEERBT hat - nur die sagen etwas ueber die Ableitung.</summary>
        private static Sec.RolePermission[] Inherited(SecurityTestContext ctx, int roleId)
            => ctx.RolePermissions.Where(n => n.RoleId == roleId && n.OriginId != null).ToArray();

        private static int Tenant(SecurityTestContext ctx, string name)
        {
            // TenantNameLower von Hand: die Spalte ist in der Datenbank BERECHNET, und weder SQLite noch
            // der InMemory-Provider rechnet sie aus - der Insert scheitert dort sonst an NOT NULL bzw. an
            // "Required properties are missing". Dieselbe Bewegung wie bei RoleNameUniqueness weiter unten.
            var tenant = new Tenant
            {
                TenantName = name, DisplayName = name, TenantNameLower = name.ToLowerInvariant()
            };
            ctx.Tenants.Add(tenant);
            ctx.SaveChanges();
            return tenant.TenantId;
        }

        private static Sec.Role Role(SecurityTestContext ctx, int tenantId, string name)
        {
            var role = new Sec.Role
            {
                RoleName = name, TenantId = tenantId, RoleNameUniqueness = $"{tenantId}:{name}"
            };
            ctx.SecurityRoles.Add(role);
            ctx.SaveChanges();
            return role;
        }

        private static Sec.Permission Permission(SecurityTestContext ctx, int tenantId, string name)
        {
            var permission = new Sec.Permission
            {
                PermissionName = name, TenantId = tenantId, PermissionNameUniqueness = $"{tenantId}:{name}"
            };
            ctx.Permissions.Add(permission);
            ctx.SaveChanges();
            return permission;
        }

        /// <summary>
        /// Der echte Sicherheits-Kontext, gebaut ueber den Weg ohne Mandantenfilter: die Vererbung ist
        /// filterfrei, und ein Berechtigungs-Scope gehoert in diesen Test nicht hinein.
        /// </summary>
        public class SecurityTestContext : Basic.SecurityContext<SecurityTestContext>
        {
            public SecurityTestContext(DbContextModelBuilderOptions<SecurityTestContext> builderOptions,
                DbContextOptions<SecurityTestContext> options) : base(builderOptions, options)
            {
            }
        }
    }
}
