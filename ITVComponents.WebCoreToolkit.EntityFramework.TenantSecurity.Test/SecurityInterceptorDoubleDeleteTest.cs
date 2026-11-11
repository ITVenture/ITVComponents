using System;
using System.Linq;
using System.Threading.Tasks;
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
    /// Nagelt fest, dass der <c>SecurityModificationInterceptor</c> beim Nach-Speichern nichts loescht, was schon
    /// geloescht ist, und dass ein fehlgeschlagenes Speichern keine Aktionen fuer das naechste hinterlaesst
    /// (Issue interceptor-01).
    /// </summary>
    /// <remarks>
    /// Anlass: ein externer Login loeschte UserRoles UND TenantUser im selben SaveChanges. Die Kaskaden-Erfassung
    /// fand dieselben, schon geloeschten UserRoles, haengte sie nach dem ersten Speichern wieder als Deleted an,
    /// und das Nach-Speichern endete mit 0 betroffenen Zeilen in einer DbUpdateConcurrencyException - obwohl das
    /// eigentliche Speichern laengst durch war. Der InMemory-Provider meldet ein DELETE ohne Zeile genauso, darum
    /// laeuft das hier wie <see cref="RoleInheritanceDuplicateTest"/> auf dem echten Kontext im Arbeitsspeicher.
    /// Jeder Fall laeuft synchron und asynchron, weil beide Wege getrennte Einstiege im Interceptor haben.
    /// </remarks>
    [TestClass]
    public class SecurityInterceptorDoubleDeleteTest
    {
        // --- Der gemeldete Fall ---------------------------------------------------------------

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task CallerRemovesUserRolesAndTenantUserInOneSave_NoConcurrencyError(bool async)
        {
            using var ctx = Context();
            int tenant = Tenant(ctx, "acme");
            var role = Role(ctx, tenant, "Employees");
            var tenantUser = TenantUserWithRole(ctx, tenant, "jdoe", role);

            // Genau das Muster aus dem Login-Abgleich: erst die Abhaengigen, dann der Principal.
            ctx.TenantUserRoles.RemoveRange(ctx.TenantUserRoles.Where(n => n.TenantUserId == tenantUser.TenantUserId).ToList());
            ctx.TenantUsers.Remove(tenantUser);
            await Save(ctx, async);

            Assert.IsFalse(ctx.TenantUsers.Any(n => n.TenantUserId == tenantUser.TenantUserId));
            Assert.IsFalse(ctx.TenantUserRoles.Any(n => n.TenantUserId == tenantUser.TenantUserId));
            Assert.AreEqual(1, ctx.SecurityRoles.Count(n => n.RoleId == role.RoleId), "die Rolle selbst bleibt.");
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task CallerRemovesOnlyTheTenantUser_TheCascadeStillRemovesTheUserRoles(bool async)
        {
            using var ctx = Context();
            int tenant = Tenant(ctx, "acme");
            var role = Role(ctx, tenant, "Employees");
            var tenantUser = TenantUserWithRole(ctx, tenant, "jdoe", role);

            // Der bisherige Workaround - die Kaskade des Interceptors muss weiter tragen.
            ctx.TenantUsers.Remove(tenantUser);
            await Save(ctx, async);

            Assert.IsFalse(ctx.TenantUserRoles.Any(n => n.RoleId == role.RoleId),
                "ohne den Principal duerfen seine Rollen-Zuordnungen nicht liegen bleiben.");
        }

        // --- Mehrstufige Vererbung ------------------------------------------------------------

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task RemovingTheFirstLinkOfAThreeLevelChain_RemovesBothDerivations(bool async)
        {
            using var ctx = Context();
            int tenant = Tenant(ctx, "acme");
            var a = Role(ctx, tenant, "A");
            var b = Role(ctx, tenant, "B");
            var c = Role(ctx, tenant, "C");
            var permission = Permission(ctx, tenant, "Sales.Manage");

            var p0 = new Sec.RolePermission { RoleId = a.RoleId, PermissionId = permission.PermissionId, TenantId = tenant };
            ctx.RolePermissions.Add(p0);
            ctx.SaveChanges();
            var aToB = new Sec.RoleRole { PermissiveRoleId = a.RoleId, PermittedRoleId = b.RoleId };
            ctx.RoleRoles.Add(aToB);
            ctx.SaveChanges();
            var bToC = new Sec.RoleRole { PermissiveRoleId = b.RoleId, PermittedRoleId = c.RoleId };
            ctx.RoleRoles.Add(bToC);
            ctx.SaveChanges();

            var p1 = Inherited(ctx, b.RoleId).Single();
            var p2 = Inherited(ctx, c.RoleId).Single();
            Assert.AreEqual(p0.RolePermissionId, p1.OriginId, "Voraussetzung: B erbt von A.");
            Assert.AreEqual(p1.RolePermissionId, p2.OriginId, "Voraussetzung: C erbt ueber B.");

            // Das Nach-Speichern erfasst p1/p2 ein zweites Mal; das dritte Speichern darf sie nicht erneut loeschen.
            ctx.RoleRoles.Remove(aToB);
            await Save(ctx, async);

            Assert.AreEqual(0, Inherited(ctx, b.RoleId).Length, "p1 haengt an der entfernten Vererbung.");
            Assert.AreEqual(0, Inherited(ctx, c.RoleId).Length, "p2 stammt von p1 ab und muss mit.");
            Assert.AreEqual(1, ctx.RolePermissions.Count(n => n.RolePermissionId == p0.RolePermissionId), "p0 bleibt.");
            Assert.AreEqual(1, ctx.RoleRoles.Count(n => n.RoleRoleId == bToC.RoleRoleId), "B -> C bleibt.");
        }

        // --- Fehlschlag ------------------------------------------------------------------------

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task AFailedSave_LeavesNoActionsForTheNextSave(bool async)
        {
            using var ctx = Context();
            int tenant = Tenant(ctx, "acme");
            var owner = Role(ctx, tenant, "TenantOwner");
            var employees = Role(ctx, tenant, "Employees");
            var permission = Permission(ctx, tenant, "Sales.Manage");

            var p0 = new Sec.RolePermission { RoleId = owner.RoleId, PermissionId = permission.PermissionId, TenantId = tenant };
            ctx.RolePermissions.Add(p0);
            ctx.RoleRoles.Add(new Sec.RoleRole { PermissiveRoleId = owner.RoleId, PermittedRoleId = employees.RoleId });
            ctx.SaveChanges();
            var p1 = Inherited(ctx, employees.RoleId).Single();

            // Loeschen von p0 laesst den Interceptor "loesche p1" vormerken; der Benutzer ohne Namen bringt das
            // Speichern zu Fall.
            ctx.RolePermissions.Remove(p0);
            var broken = new Sec.User();
            ctx.Users.Add(broken);
            var failed = false;
            try
            {
                await Save(ctx, async);
            }
            catch (DbUpdateException)
            {
                failed = true;
            }

            Assert.IsTrue(failed, "Voraussetzung: das erste Speichern muss scheitern.");

            // Der Aufrufer nimmt beides zurueck und speichert etwas Unabhaengiges.
            ctx.Entry(broken).State = EntityState.Detached;
            ctx.Entry(p0).State = EntityState.Unchanged;
            Permission(ctx, tenant, "Unrelated");

            Assert.AreEqual(1, ctx.RolePermissions.Count(n => n.RolePermissionId == p1.RolePermissionId),
                "die Vormerkung aus dem gescheiterten Speichern darf im naechsten nicht ausgefuehrt werden.");
        }

        // --- Testgeruest ----------------------------------------------------------------------

        private static Task Save(SecurityTestContext ctx, bool async)
        {
            if (async)
            {
                return ctx.SaveChangesAsync();
            }

            ctx.SaveChanges();
            return Task.CompletedTask;
        }

        private static SecurityTestContext Context()
        {
            var services = new ServiceCollection().BuildServiceProvider();
            var options = new DbContextOptionsBuilder<SecurityTestContext>()
                .UseInMemoryDatabase($"interceptorDoubleDelete_{Guid.NewGuid():N}")
                .AddInterceptors(new SecurityModificationInterceptor<Tenant, int, Sec.User, Sec.Role,
                    Sec.Permission, Sec.UserRole, Sec.RolePermission, Sec.TenantUser, Sec.RoleRole,
                    Sec.GlobalRole, Sec.GlobalRolePermission, Sec.GRoleLRole>(services))
                .Options;
            return new SecurityTestContext(new DbContextModelBuilderOptions<SecurityTestContext>(), options);
        }

        private static Sec.RolePermission[] Inherited(SecurityTestContext ctx, int roleId)
            => ctx.RolePermissions.Where(n => n.RoleId == roleId && n.OriginId != null).ToArray();

        private static int Tenant(SecurityTestContext ctx, string name)
        {
            // TenantNameLower ist in der Datenbank berechnet; der InMemory-Provider rechnet sie nicht aus.
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

        private static Sec.TenantUser TenantUserWithRole(SecurityTestContext ctx, int tenantId, string userName, Sec.Role role)
        {
            var user = new Sec.User { UserName = userName };
            ctx.Users.Add(user);
            ctx.SaveChanges();
            var tenantUser = new Sec.TenantUser { UserId = user.UserId, TenantId = tenantId };
            ctx.TenantUsers.Add(tenantUser);
            ctx.SaveChanges();
            ctx.TenantUserRoles.Add(new Sec.UserRole { TenantUserId = tenantUser.TenantUserId, RoleId = role.RoleId });
            ctx.SaveChanges();
            return tenantUser;
        }

        /// <summary>Der echte Sicherheits-Kontext, gebaut ueber den Weg ohne Mandantenfilter.</summary>
        public class SecurityTestContext : Basic.SecurityContext<SecurityTestContext>
        {
            public SecurityTestContext(DbContextModelBuilderOptions<SecurityTestContext> builderOptions,
                DbContextOptions<SecurityTestContext> options) : base(builderOptions, options)
            {
            }
        }
    }
}
