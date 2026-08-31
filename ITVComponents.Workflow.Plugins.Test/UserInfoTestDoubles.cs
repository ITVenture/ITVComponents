using System;
using System.ComponentModel.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using CustomUserProperty = ITVComponents.WebCoreToolkit.Models.CustomUserProperty;

namespace ITVComponents.Workflow.Plugins.Test
{
    /// <summary>
    /// Die Testmodelle sind bewusst <b>nicht</b> die echten Security-Entitaeten: der Handler kennt die
    /// Entitaeten an ihren Namen und nicht an ihrer Vererbung, und genau das soll der Test nachweisen.
    /// Ein Modell mit denselben Namen und einem anderen Schluesseltyp muss ihm ebenso genuegen.
    /// </summary>
    public class TestUser
    {
        /// <summary>Der Primaerschluessel - hier eine Zahl.</summary>
        [Key]
        public int UserId { get; set; }

        /// <summary>Der Anmeldename.</summary>
        public string UserName { get; set; }

        /// <summary>Die Anmeldeadresse (wie beim Identity-Benutzer klein geschrieben).</summary>
        public string Email { get; set; }
    }

    /// <summary>Die Mandanten-Zuordnung eines <see cref="TestUser"/>.</summary>
    public class TestTenantUser
    {
        /// <summary>Der Primaerschluessel.</summary>
        [Key]
        public int TenantUserId { get; set; }

        /// <summary>Der Benutzer.</summary>
        public int UserId { get; set; }

        /// <summary>Der Mandant.</summary>
        public int TenantId { get; set; }

        /// <summary>Ob die Zuordnung aktiv ist.</summary>
        public bool? Enabled { get; set; }
    }

    /// <summary>Der Mitarbeiter zu einem <see cref="TestUser"/>.</summary>
    public class TestEmployee
    {
        /// <summary>Der Primaerschluessel.</summary>
        [Key]
        public int EmployeeId { get; set; }

        /// <summary>Der Benutzer.</summary>
        public int UserId { get; set; }

        /// <summary>Der Mandant.</summary>
        public int TenantId { get; set; }

        /// <summary>Die Mandanten-Zuordnung.</summary>
        public int? TenantUserId { get; set; }

        /// <summary>Der Vorname.</summary>
        public string FirstName { get; set; }

        /// <summary>Der Nachname.</summary>
        public string LastName { get; set; }

        /// <summary>Die Geschaeftsadresse (im Onboarding-Modell gross geschrieben).</summary>
        public string EMail { get; set; }

        /// <summary>Der Stand der Einladung.</summary>
        public TestInvitationStatus InvitationStatus { get; set; }
    }

    /// <summary>Der Stand einer Einladung - hier nur so weit, wie der Test ihn braucht.</summary>
    public enum TestInvitationStatus
    {
        /// <summary>Keine Einladung im Spiel.</summary>
        None,

        /// <summary>Eingeladen, noch nicht angenommen.</summary>
        Pending,

        /// <summary>Angenommen.</summary>
        Committed
    }

    /// <summary>Eine benutzerdefinierte Eigenschaft - mit demselben Zuschnitt wie im Toolkit.</summary>
    public class TestUserProperty : CustomUserProperty
    {
        /// <summary>Der Primaerschluessel.</summary>
        [Key]
        public int CustomUserPropertyId { get; set; }

        /// <summary>Der Benutzer.</summary>
        public int UserId { get; set; }
    }

    /// <summary>Ein Benutzer mit einem <b>Text</b>-Schluessel - die andere Ausprägung.</summary>
    public class TextUser
    {
        /// <summary>Der Primaerschluessel - hier eine Zeichenkette.</summary>
        [Key]
        public string Id { get; set; }

        /// <summary>Der Anmeldename.</summary>
        public string UserName { get; set; }

        /// <summary>Die Anmeldeadresse.</summary>
        public string Email { get; set; }
    }

    /// <summary>Die Mandanten-Zuordnung eines <see cref="TextUser"/>.</summary>
    public class TextTenantUser
    {
        /// <summary>Der Primaerschluessel.</summary>
        [Key]
        public int TenantUserId { get; set; }

        /// <summary>Der Benutzer - hier eine Zeichenkette.</summary>
        public string UserId { get; set; }

        /// <summary>Der Mandant.</summary>
        public int TenantId { get; set; }

        /// <summary>Ob die Zuordnung aktiv ist.</summary>
        public bool? Enabled { get; set; }
    }

    /// <summary>Eine benutzerdefinierte Eigenschaft eines <see cref="TextUser"/>.</summary>
    public class TextUserProperty : CustomUserProperty
    {
        /// <summary>Der Primaerschluessel.</summary>
        [Key]
        public int CustomUserPropertyId { get; set; }

        /// <summary>Der Benutzer.</summary>
        public string UserId { get; set; }
    }

    /// <summary>Der Testkontext mit beiden Ausprägungen nebeneinander.</summary>
    public class TestSecurityContext : DbContext
    {
        /// <summary>Initialisiert den Kontext.</summary>
        /// <param name="options">die Optionen</param>
        public TestSecurityContext(DbContextOptions<TestSecurityContext> options) : base(options)
        {
        }

        /// <summary>Die Benutzer mit Zahl-Schluessel.</summary>
        public DbSet<TestUser> Users { get; set; }

        /// <summary>Die Mandanten-Zuordnungen.</summary>
        public DbSet<TestTenantUser> TenantUsers { get; set; }

        /// <summary>Die Mitarbeiter.</summary>
        public DbSet<TestEmployee> Employees { get; set; }

        /// <summary>Die benutzerdefinierten Eigenschaften.</summary>
        public DbSet<TestUserProperty> UserProperties { get; set; }

        /// <summary>Die Benutzer mit Text-Schluessel.</summary>
        public DbSet<TextUser> TextUsers { get; set; }

        /// <summary>Die Mandanten-Zuordnungen der Text-Benutzer.</summary>
        public DbSet<TextTenantUser> TextTenantUsers { get; set; }

        /// <summary>Die Eigenschaften der Text-Benutzer.</summary>
        public DbSet<TextUserProperty> TextUserProperties { get; set; }
    }

    /// <summary>
    /// Eine Kontext-Fabrik, die je Leihe einen frischen Kontext auf derselben Verbindung baut - so, wie
    /// es der echte Weg tut (ein frischer Kontext je Operation).
    /// </summary>
    public sealed class TestContextFactory : IToolkitContextFactory
    {
        private readonly Func<TestSecurityContext> create;

        /// <summary>Initialisiert die Fabrik.</summary>
        /// <param name="create">baut einen frischen Kontext</param>
        public TestContextFactory(Func<TestSecurityContext> create)
        {
            this.create = create;
        }

        /// <inheritdoc/>
        public T Create<T>() where T : class => (T)(object)create();

        /// <inheritdoc/>
        public IContextLease<T> Lease<T>() where T : class
        {
            TestSecurityContext context = create();
            return new ContextLease<T>((T)(object)context, context);
        }

        private sealed class ContextLease<T> : IContextLease<T> where T : class
        {
            private readonly IDisposable owner;

            public ContextLease(T context, IDisposable owner)
            {
                Context = context;
                this.owner = owner;
            }

            public T Context { get; }

            public void Dispose() => owner.Dispose();
        }
    }
}
