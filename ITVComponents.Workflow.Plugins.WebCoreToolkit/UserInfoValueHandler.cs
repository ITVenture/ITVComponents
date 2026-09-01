using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using ITVComponents.Logging;
using ITVComponents.MemberAccess;
using ITVComponents.TypeConversion;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.Workflow.ValueHandles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using CustomUserProperty = ITVComponents.WebCoreToolkit.Models.CustomUserProperty;

namespace ITVComponents.Workflow.Plugins.WebCoreToolkit
{
    /// <summary>
    /// Ein <b>lesender</b> Wert-Handler, der zu einer Benutzer-, Mandanten-Benutzer- oder
    /// Mitarbeiter-Kennung so viel ueber den Benutzer zusammentraegt, wie die Ablage hergibt.
    /// </summary>
    /// <remarks>
    /// Die Typparameter heissen <b>absichtlich genau so wie die von <c>ISecurityContext&lt;…&gt;</c></b>:
    /// die Plugin-Factory finalisiert einen offenen Typ ueber den Parameter
    /// <c>$$genericArgumentProvider</c>, und zugeordnet wird dabei ueber den <b>Namen</b> des
    /// Typparameters. Ein umbenannter Parameter sieht danach aus wie ein fehlender. Ein Eintrag
    /// <c>$$genericArgumentProvider</c> mit dem konkreten Security-Kontext genuegt damit fuer alle drei.
    /// <para>
    /// <b>Die Kennungen kommen nicht alle.</b> Es kann eine Benutzer-Id sein, eine Mandanten-Benutzer-Id,
    /// eine Mitarbeiter-Id, ein Anmeldename - oder mehreres davon. Der Handler nimmt, was da ist, geht vom
    /// Speziellen zum Allgemeinen und ergaenzt den Rest.
    /// </para>
    /// <para>
    /// <b>Nur lesend, und das mit Absicht.</b> Aufgeloest wird im Hintergrund-Scope des Mandanten der
    /// Instanz - nicht mit den Rechten dessen, der eine Maske ausfuellt. Ein schreibender Handler liesse
    /// einen Vorgang also Stammdaten aendern, die die handelnde Person selbst nicht aendern duerfte.
    /// </para>
    /// <para>
    /// <b>Die Mandantengrenze gilt.</b> Der Host fixiert den Scope auf den Mandanten der Instanz, die
    /// Abfragefilter greifen. Ein Benutzer, der diesem Mandanten nicht zugeordnet ist, kommt deshalb mit
    /// <c>User</c>, aber ohne <c>TenantUser</c> und ohne <c>Employee</c> zurueck. Das ist gewollt.
    /// </para>
    /// </remarks>
    /// <typeparam name="TUser">der konkrete Benutzer-Typ</typeparam>
    /// <typeparam name="TTenantUser">der konkrete Mandanten-Benutzer-Typ</typeparam>
    /// <typeparam name="TUserProperty">der konkrete Typ der benutzerdefinierten Eigenschaften</typeparam>
    public class UserInfoValueHandler<TUser, TTenantUser, TUserProperty> : IValueHandlerPlugin
        where TUser : class
        where TTenantUser : class
        where TUserProperty : CustomUserProperty
    {
        /// <summary>Der Name des Arguments, das eine Benutzer-Id traegt.</summary>
        public const string ArgumentUserId = "userId";

        /// <summary>Der Name des Arguments, das eine Mandanten-Benutzer-Id traegt.</summary>
        public const string ArgumentTenantUserId = "tenantUserId";

        /// <summary>Der Name des Arguments, das eine Mitarbeiter-Id traegt.</summary>
        public const string ArgumentEmployeeId = "employeeId";

        /// <summary>Der Name des Arguments, das einen Anmeldenamen traegt.</summary>
        public const string ArgumentUserName = "userName";

        /// <summary>Der Name des Arguments, das eine Rechnungsprofil-Id traegt.</summary>
        public const string ArgumentBillingProfileId = "billingProfileId";

        /// <summary>
        /// Der Name des <c>ProfileType</c>-Werts, der ein Profil einer <b>natuerlichen Person</b>
        /// bezeichnet.
        /// </summary>
        /// <remarks>
        /// Ueber den Namen und nicht ueber den Typ: dieser Handler spricht das ganze Modell ueber Namen an
        /// (<c>UserId</c>, <c>TenantUserId</c>, <c>InvitationStatus</c> …), damit er mit dem Flat- und dem
        /// Hierarchy-Modell laeuft. Fuer ein einzelnes Enum das Onboarding-Paket hereinzuziehen waere der
        /// teurere Handel. Ein Wert, der weder hier noch bei <see cref="CompanyProfileType"/> steht, wird
        /// protokolliert - ein umbenanntes Enum soll nicht still das Falsche tun.
        /// </remarks>
        protected const string PersonalProfileType = "Personal";

        /// <summary>Der Name des <c>ProfileType</c>-Werts, der ein Firmenprofil bezeichnet.</summary>
        protected const string CompanyProfileType = "Company";

        /// <summary>
        /// <c>EF.Property&lt;T&gt;(object, string)</c> - damit werden die Abfragen gebaut. Ueber die
        /// CLR-Eigenschaft zu gehen waere hier falsch: der Schluessel ist mal <c>int</c>, mal
        /// <c>string</c>, und der Modell-Weg kennt beides, ohne dass es jemand konfigurieren muss.
        /// </summary>
        private static readonly MethodInfo EfProperty = typeof(EF).GetMethod(
            nameof(EF.Property), BindingFlags.Public | BindingFlags.Static);

        private readonly IToolkitContextFactory contextFactory;
        private readonly bool allowMissing;

        /// <summary>
        /// Initialisiert den Handler.
        /// </summary>
        /// <param name="contextFactory">
        /// die Fabrik, die je Aufruf einen frischen Security-Kontext leiht (der Blazor-sichere Weg)
        /// </param>
        public UserInfoValueHandler(IToolkitContextFactory contextFactory) : this(contextFactory, false)
        {
        }

        /// <summary>
        /// Initialisiert den Handler.
        /// </summary>
        /// <param name="contextFactory">
        /// die Fabrik, die je Aufruf einen frischen Security-Kontext leiht (der Blazor-sichere Weg)
        /// </param>
        /// <param name="allowMissing">
        /// Ist „kein Benutzer" ein normaler Zustand? Vorgabe false: dann wirft der Handler, und der
        /// Vorgang faultet mit einer Meldung, die die Kennung nennt. Auf true kommt statt dessen ein
        /// Ergebnis mit <see cref="UserInfo.Found"/> = false zurueck.
        /// </param>
        public UserInfoValueHandler(IToolkitContextFactory contextFactory, bool allowMissing)
        {
            this.contextFactory = contextFactory
                                  ?? throw new ArgumentNullException(nameof(contextFactory));
            this.allowMissing = allowMissing;
        }

        /// <inheritdoc/>
        public event EventHandler Disposed;

        /// <inheritdoc/>
        public string UniqueName { get; set; }

        /// <summary>
        /// Kennt diese Ausprägung Mitarbeiter? In der Grundform nicht - dafuer gibt es
        /// <see cref="EmployeeUserInfoValueHandler{TUser,TTenantUser,TUserProperty,TEmployee,TBillingProfile}"/>.
        /// </summary>
        protected virtual bool SupportsEmployees => false;

        /// <summary>
        /// Kennt diese Auspraegung Rechnungsprofile? In der Grundform nicht - dafuer gibt es
        /// <see cref="EmployeeUserInfoValueHandler{TUser,TTenantUser,TUserProperty,TEmployee,TBillingProfile}"/>.
        /// </summary>
        protected virtual bool SupportsBillingProfiles => false;

        /// <inheritdoc/>
        public object Read(ValueHandleRequest request)
        {
            object employeeId = Argument(request, ArgumentEmployeeId);
            object tenantUserId = Argument(request, ArgumentTenantUserId);
            object userId = Argument(request, ArgumentUserId);
            object userName = Argument(request, ArgumentUserName);
            object billingProfileId = Argument(request, ArgumentBillingProfileId);

            if (employeeId == null && tenantUserId == null && userId == null && userName == null
                && billingProfileId == null)
            {
                throw new InvalidOperationException(
                    $"The user info handler '{UniqueName}' was asked without any of the arguments " +
                    $"'{ArgumentEmployeeId}', '{ArgumentTenantUserId}', '{ArgumentUserId}', " +
                    $"'{ArgumentUserName}' or '{ArgumentBillingProfileId}' - it would not know whom to " +
                    $"describe. Request: {request}.");
            }

            if (employeeId != null && !SupportsEmployees)
            {
                throw new InvalidOperationException(
                    $"The user info handler '{UniqueName}' was given an '{ArgumentEmployeeId}', but it is " +
                    "configured without employees. Use the employee-aware handler (and give it a TEmployee " +
                    "generic parameter), or ask by user instead.");
            }

            if (billingProfileId != null && !SupportsBillingProfiles)
            {
                throw new InvalidOperationException(
                    $"The user info handler '{UniqueName}' was given a '{ArgumentBillingProfileId}', but it " +
                    "is configured without billing profiles. Use the employee-aware handler (and give it a " +
                    "TBillingProfile generic parameter), or ask by user instead.");
            }

            using IContextLease<DbContext> lease = contextFactory.Lease<DbContext>();
            DbContext db = lease.Context;

            // Vom Speziellen zum Allgemeinen: der Mitarbeiter kennt seinen Benutzer und seine
            // Mandanten-Zuordnung, die Mandanten-Zuordnung kennt ihren Benutzer, das Rechnungsprofil
            // seinen Eigentuemer.
            object employee = employeeId != null ? EmployeeByKey(db, employeeId) : null;
            object profile = billingProfileId != null ? OwnerProfileByKey(db, billingProfileId) : null;
            if (profile != null && !IsPersonalProfile(profile))
            {
                // Ausdruecklich nach diesem Profil gefragt - und es ist ein Firmenprofil. Dessen
                // Namensfelder beschreiben die Firma bzw. eine Kontaktperson, nicht den Eigentuemer.
                // Sie hier auszugeben hiesse, einen anderen Menschen zu zeigen als den gemeinten.
                throw new InvalidOperationException(
                    $"The user info handler '{UniqueName}' was given the '{ArgumentBillingProfileId}' " +
                    $"{billingProfileId}, but that profile is not of type '{PersonalProfileType}'. A company " +
                    "profile does not describe a single person - ask by user, employee or tenant user " +
                    "instead.");
            }

            object tenantUser = tenantUserId != null ? ByKey<TTenantUser>(db, tenantUserId) : null;
            tenantUser ??= ByKey<TTenantUser>(db, Member(employee, "TenantUserId"));

            object key = Member(employee, "UserId") ?? Member(tenantUser, "UserId")
                                                    ?? Member(profile, "OwnerUserId");

            object user = null;
            if (key == null && userId != null)
            {
                key = userId;
            }

            if (key == null && userName != null)
            {
                user = FirstWhere<TUser>(db, "UserName", userName);
                key = KeyOf(db, user);
            }

            WarnOnContradiction(request, key, userId);

            user ??= ByKey<TUser>(db, key);
            tenantUser ??= FirstWhere<TTenantUser>(db, "UserId", key);
            employee ??= EmployeeByUser(db, key);

            // Nur wenn es keinen Mitarbeiter gibt: genau das ist die Lage des Mandanten-Eigentuemers -
            // er steht im Rechnungsprofil als OwnerUser, und ein Mitarbeiter-Datensatz wird fuer ihn nicht
            // angelegt. Wer einen Mitarbeiter hat, braucht diese Abfrage nicht, und der Mitarbeiter
            // gewinnt ohnehin (er ist der mandantenspezifische Datensatz).
            if (employee == null)
            {
                profile ??= OwnerProfileByUser(db, key);
            }

            var info = new UserInfo
            {
                Found = user != null || tenantUser != null || employee != null || profile != null
            };

            if (!info.Found)
            {
                if (!allowMissing)
                {
                    throw new InvalidOperationException(
                        $"The user info handler '{UniqueName}' found nobody for {Describe(employeeId,
                            tenantUserId, userId, userName, billingProfileId)}. If that is a normal state " +
                        "here, configure the handler with AllowMissing.");
                }

                LogEnvironment.LogEvent(
                    $"User info handler '{UniqueName}' found nobody for {Describe(employeeId, tenantUserId,
                        userId, userName, billingProfileId)} - continuing with an empty result because " +
                    "AllowMissing is set.",
                    LogSeverity.Report);
                return info;
            }

            Fill(db, info, key, user, tenantUser, employee, profile);
            return info;
        }

        /// <inheritdoc/>
        public void Write(ValueHandleRequest request, object value)
        {
            throw new NotSupportedException(
                $"The user info handler '{UniqueName}' is read-only. It resolves in the background scope of " +
                "the instance's tenant, not with the rights of whoever fills in a mask - writing through it " +
                "would let a workflow change master data that the acting person may not change. Model an " +
                "activity that writes deliberately, with its own handler.");
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Sucht den Mitarbeiter zu seiner Id. In der Grundform gibt es keine.
        /// </summary>
        /// <param name="db">der geliehene Kontext</param>
        /// <param name="employeeId">die Mitarbeiter-Id</param>
        /// <returns>der Mitarbeiter, oder null</returns>
        protected virtual object EmployeeByKey(DbContext db, object employeeId)
        {
            return null;
        }

        /// <summary>
        /// Sucht den Mitarbeiter zu einem Benutzer. In der Grundform gibt es keine.
        /// </summary>
        /// <param name="db">der geliehene Kontext</param>
        /// <param name="userKey">die Benutzer-Id</param>
        /// <returns>der Mitarbeiter, oder null</returns>
        protected virtual object EmployeeByUser(DbContext db, object userKey)
        {
            return null;
        }

        /// <summary>
        /// Sucht das Rechnungsprofil zu seiner Id. In der Grundform gibt es keine.
        /// </summary>
        /// <param name="db">der geliehene Kontext</param>
        /// <param name="billingProfileId">die Rechnungsprofil-Id</param>
        /// <returns>das Profil, oder null</returns>
        protected virtual object OwnerProfileByKey(DbContext db, object billingProfileId)
        {
            return null;
        }

        /// <summary>
        /// Sucht das <b>persoenliche</b> Rechnungsprofil, als dessen Eigentuemer der Benutzer eingetragen
        /// ist. In der Grundform gibt es keine.
        /// </summary>
        /// <param name="db">der geliehene Kontext</param>
        /// <param name="userKey">die Benutzer-Id</param>
        /// <returns>das Profil, oder null</returns>
        protected virtual object OwnerProfileByUser(DbContext db, object userKey)
        {
            return null;
        }

        /// <summary>
        /// Beschreibt dieses Rechnungsprofil eine <b>natuerliche Person</b>?
        /// </summary>
        /// <remarks>
        /// Geprueft wird im Speicher und nicht in der Abfrage: fuer ein <c>WHERE</c> auf das Enum braeuchte
        /// es dessen konkreten Typ, und ein Eigentuemer hat hoechstens eine Handvoll Profile.
        /// </remarks>
        /// <param name="profile">das geladene Profil</param>
        /// <returns>true, wenn der <c>ProfileType</c> eine Person bezeichnet</returns>
        protected bool IsPersonalProfile(object profile)
        {
            object value = Member(profile, "ProfileType");
            if (value == null)
            {
                // Ein Enum ist nie null - fehlt der Wert, fehlt das Member. Dann ist der konfigurierte
                // Typ nicht der erwartete, und das ist ein Konfigurationsfehler, kein Datenfall.
                throw new InvalidOperationException(
                    $"The user info handler '{UniqueName}' got a billing profile without a 'ProfileType'. " +
                    "The handler is configured with a type that does not have the expected shape - check " +
                    "the TBillingProfile generic parameter of the plugin.");
            }

            string name = value.ToString();
            if (string.Equals(name, PersonalProfileType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.Equals(name, CompanyProfileType, StringComparison.OrdinalIgnoreCase))
            {
                // Weder das eine noch das andere: entweder wurde die Aufzaehlung erweitert oder umbenannt.
                // Als 'kein Personenprofil' zu behandeln ist die sichere Seite - aber still darf es nicht
                // passieren, sonst sucht man den fehlenden Namen an der falschen Stelle.
                LogEnvironment.LogEvent(
                    $"User info handler '{UniqueName}': billing profile type '{name}' is neither " +
                    $"'{PersonalProfileType}' nor '{CompanyProfileType}'. It is treated as NOT personal, so " +
                    "no name is taken from it. The onboarding model has probably gained or renamed a " +
                    "profile type.", LogSeverity.Error);
            }

            return false;
        }

        /// <summary>
        /// Sucht die Entitaet mit dem gegebenen Primaerschluessel.
        /// </summary>
        /// <typeparam name="TEntity">der Entitaets-Typ</typeparam>
        /// <param name="db">der geliehene Kontext</param>
        /// <param name="rawKey">der Schluesselwert, so wie er aus der Bindung kam</param>
        /// <returns>die Entitaet, oder null</returns>
        protected static TEntity ByKey<TEntity>(DbContext db, object rawKey) where TEntity : class
        {
            return rawKey == null ? null : FirstWhere<TEntity>(db, SingleKey(db, typeof(TEntity)).Name, rawKey);
        }

        /// <summary>
        /// Sucht die erste Entitaet, deren Eigenschaft dem gegebenen Wert entspricht.
        /// </summary>
        /// <typeparam name="TEntity">der Entitaets-Typ</typeparam>
        /// <param name="db">der geliehene Kontext</param>
        /// <param name="property">der Name der Eigenschaft</param>
        /// <param name="rawValue">der Wert, so wie er aus der Bindung kam</param>
        /// <returns>die Entitaet, oder null</returns>
        protected static TEntity FirstWhere<TEntity>(DbContext db, string property, object rawValue)
            where TEntity : class
        {
            return Where<TEntity>(db, property, rawValue)?.FirstOrDefault();
        }

        /// <summary>
        /// Baut die Abfrage „Eigenschaft gleich Wert" ueber das Modell.
        /// </summary>
        /// <remarks>
        /// Ueber <c>EF.Property</c> und den im Modell hinterlegten CLR-Typ: damit ist die
        /// int-oder-string-Frage der Benutzer-Id beantwortet, ohne dass irgendwo eine Ausdrucks-Eigenschaft
        /// je Ausprägung hinterlegt werden muesste. Die Umwandlung des Bindungs-Arguments laeuft ueber
        /// <see cref="TypeConverter"/>.
        /// </remarks>
        /// <typeparam name="TEntity">der Entitaets-Typ</typeparam>
        /// <param name="db">der geliehene Kontext</param>
        /// <param name="property">der Name der Eigenschaft</param>
        /// <param name="rawValue">der Wert, so wie er aus der Bindung kam</param>
        /// <returns>die Abfrage, oder null wenn es nichts zu suchen gibt</returns>
        protected static IQueryable<TEntity> Where<TEntity>(DbContext db, string property, object rawValue)
            where TEntity : class
        {
            if (rawValue == null)
            {
                return null;
            }

            IEntityType entity = Entity(db, typeof(TEntity));
            IProperty declared = entity.FindProperty(property);
            if (declared == null)
            {
                throw new InvalidOperationException(
                    $"'{typeof(TEntity).FullName}' has no property '{property}' in the model. The user info " +
                    "handler is configured with a type that does not have the expected shape - check the " +
                    "generic parameters of the plugin.");
            }

            Type target = Nullable.GetUnderlyingType(declared.ClrType) ?? declared.ClrType;
            object value = ConvertTo(rawValue, target, property, typeof(TEntity));
            if (value == null)
            {
                // Der Konverter hat null geliefert: es gibt nichts zu suchen. Ein 'IS NULL' waere hier das
                // Falsche - danach hat niemand gefragt.
                return null;
            }

            ParameterExpression e = Expression.Parameter(typeof(TEntity), "e");
            Expression access = Expression.Call(EfProperty.MakeGenericMethod(declared.ClrType), e,
                Expression.Constant(declared.Name));
            Expression constant = Expression.Constant(value, target);
            if (constant.Type != declared.ClrType)
            {
                constant = Expression.Convert(constant, declared.ClrType);
            }

            return db.Set<TEntity>()
                .Where(Expression.Lambda<Func<TEntity, bool>>(Expression.Equal(access, constant), e));
        }

        /// <summary>
        /// Liest den Wert eines Members, ohne dass der Typ ihn deklarieren muesste.
        /// </summary>
        /// <remarks>
        /// Ueber den geteilten Member-Zugriff: der meldet ein fehlendes Member, statt zu werfen. So kommt
        /// dieser Handler ohne die langen Typ-Constraints der Modell-Basisklassen aus - er kennt die
        /// Entitaeten an ihren Namen, nicht an ihrer Vererbung.
        /// </remarks>
        /// <param name="target">die Entitaet, oder null</param>
        /// <param name="name">der Name des Members</param>
        /// <returns>der Wert, oder null</returns>
        protected static object Member(object target, string name)
        {
            if (target == null)
            {
                return null;
            }

            MemberSlot slot = MemberAccessor.Resolve(target, name);
            return slot.Exists() ? slot.Read() : null;
        }

        /// <summary>
        /// Liest den Primaerschluessel einer geladenen Entitaet.
        /// </summary>
        /// <remarks>
        /// Ueber den Change-Tracker und nicht ueber <c>entity.GetType()</c>: ein Lazy-Loading-Proxy hat
        /// einen anderen CLR-Typ als die Entitaet, und der steht im Modell nicht.
        /// </remarks>
        /// <param name="db">der geliehene Kontext</param>
        /// <param name="entity">die Entitaet, oder null</param>
        /// <returns>der Schluesselwert, oder null</returns>
        protected static object KeyOf(DbContext db, object entity)
        {
            if (entity == null)
            {
                return null;
            }

            IEntityType metadata = db.Entry(entity).Metadata;
            IProperty key = OnlyKeyOf(metadata);
            return db.Entry(entity).Property(key.Name).CurrentValue;
        }

        /// <summary>
        /// Traegt die flachen Felder und die Eigenschaften zusammen.
        /// </summary>
        /// <param name="db">der geliehene Kontext</param>
        /// <param name="info">das zu fuellende Ergebnis</param>
        /// <param name="key">die Benutzer-Id</param>
        /// <param name="user">der Benutzer-Datensatz, oder null</param>
        /// <param name="tenantUser">der Mandanten-Benutzer-Datensatz, oder null</param>
        /// <param name="employee">der Mitarbeiter-Datensatz, oder null</param>
        /// <param name="profile">das persoenliche Rechnungsprofil, oder null</param>
        private void Fill(DbContext db, UserInfo info, object key, object user, object tenantUser,
            object employee, object profile)
        {
            info.User = user;
            info.TenantUser = tenantUser;
            info.Employee = employee;
            info.BillingProfile = profile;

            info.UserId = key ?? KeyOf(db, user);
            info.UserName = Text(Member(user, "UserName"));

            // Der Mitarbeiter schlaegt das Rechnungsprofil: er ist der mandantenspezifische Datensatz.
            // Das Profil ist der Weg fuer den Mandanten-Eigentuemer, fuer den es gar keinen gibt.
            info.FirstName = Text(Member(employee, "FirstName")) ?? Text(Member(profile, "FirstName"));
            info.LastName = Text(Member(employee, "LastName")) ?? Text(Member(profile, "LastName"));

            // Der Mitarbeiter schlaegt den Benutzer; 'Email' und 'EMail' sind beide unterwegs (Identity
            // schreibt es klein, das Onboarding-Modell gross). Das Rechnungsprofil zuletzt: die
            // Anmeldeadresse ist die verbindliche, die Rechnungsadresse darf eine andere sein.
            info.EMail = Text(Member(employee, "EMail"))
                         ?? Text(Member(user, "Email"))
                         ?? Text(Member(user, "EMail"))
                         ?? Text(Member(profile, "Email"));

            // Die einzige Angabe, die der Benutzer-Datensatz gar nicht kennt.
            info.PhoneNumber = Text(Member(employee, "PhoneNumber")) ?? Text(Member(profile, "PhoneNumber"));

            info.DisplayName = Join(info.FirstName, info.LastName) ?? info.UserName;

            info.TenantId = Number(Member(employee, "TenantId")) ?? Number(Member(tenantUser, "TenantId"))
                            ?? Number(Member(profile, "TenantId"));
            info.TenantUserId = Number(Member(tenantUser, "TenantUserId"));
            info.Enabled = Member(tenantUser, "Enabled") as bool?;
            info.EmployeeId = Number(Member(employee, "EmployeeId"));
            info.BillingProfileId = Number(Member(profile, "BillingProfileId"));
            info.InvitationStatus = Text(Member(employee, "InvitationStatus"));

            if (info.UserId == null)
            {
                return;
            }

            foreach (TUserProperty property in Where<TUserProperty>(db, "UserId", info.UserId)
                     ?? Enumerable.Empty<TUserProperty>().AsQueryable())
            {
                if (string.IsNullOrEmpty(property?.PropertyName))
                {
                    continue;
                }

                if (info.Properties.ContainsKey(property.PropertyName))
                {
                    // Der eindeutige Index steht auf (Benutzer, ART, Name) - derselbe Name kann also in
                    // zwei Arten vorkommen. Hier gaebe es nur einen Platz dafuer; wer das tut, soll es
                    // sehen, statt sich zu wundern, welcher Wert gewonnen hat.
                    LogEnvironment.LogEvent(
                        $"User info handler '{UniqueName}': user '{info.UserId}' has more than one custom " +
                        $"property named '{property.PropertyName}' (different property types). The first one " +
                        "is used.", LogSeverity.Warning);
                    continue;
                }

                info.Properties[property.PropertyName] = property.Value;
            }
        }

        /// <summary>
        /// Meldet, wenn eine mitgegebene Benutzer-Id nicht zu der passt, die sich aus der spezielleren
        /// Kennung ergeben hat.
        /// </summary>
        /// <remarks>
        /// Kein Fehler - die speziellere Kennung gewinnt, und das ist eine klare Regel. Aber es ist ein
        /// Modellierungsfehler, der sonst unsichtbar bliebe: die Maske zeigte dann einen anderen Menschen
        /// als den, den der Vorgang meint.
        /// </remarks>
        /// <param name="request">die Anfrage (fuer die Meldung)</param>
        /// <param name="resolved">die aufgeloeste Benutzer-Id</param>
        /// <param name="given">die mitgegebene Benutzer-Id</param>
        private void WarnOnContradiction(ValueHandleRequest request, object resolved, object given)
        {
            if (resolved == null || given == null || Same(resolved, given))
            {
                return;
            }

            LogEnvironment.LogEvent(
                $"User info handler '{UniqueName}': the given '{ArgumentUserId}' ({given}) does not match the " +
                $"user behind the more specific argument ({resolved}). The more specific one wins. " +
                $"Request: {request}.", LogSeverity.Warning);
        }

        /// <summary>Vergleicht zwei Schluesselwerte, die aus verschiedenen Quellen verschieden getippt sind.</summary>
        private static bool Same(object a, object b)
        {
            return string.Equals(Convert.ToString(a, CultureInfo.InvariantCulture),
                Convert.ToString(b, CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Nennt die mitgegebenen Kennungen - fuer die Meldung, wenn nichts gefunden wurde.</summary>
        private static string Describe(object employeeId, object tenantUserId, object userId, object userName,
            object billingProfileId)
        {
            var parts = new List<string>();
            if (employeeId != null)
            {
                parts.Add($"{ArgumentEmployeeId}={employeeId}");
            }

            if (tenantUserId != null)
            {
                parts.Add($"{ArgumentTenantUserId}={tenantUserId}");
            }

            if (userId != null)
            {
                parts.Add($"{ArgumentUserId}={userId}");
            }

            if (userName != null)
            {
                parts.Add($"{ArgumentUserName}={userName}");
            }

            if (billingProfileId != null)
            {
                parts.Add($"{ArgumentBillingProfileId}={billingProfileId}");
            }

            return string.Join(", ", parts);
        }

        /// <summary>
        /// Holt ein Argument - ohne auf Gross-/Kleinschreibung zu bestehen und ohne leere Zeichenketten.
        /// </summary>
        /// <param name="request">die Anfrage</param>
        /// <param name="name">der Name des Arguments</param>
        /// <returns>der Wert, oder null</returns>
        private static object Argument(ValueHandleRequest request, string name)
        {
            foreach (KeyValuePair<string, object> pair in request.Arguments)
            {
                if (!string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Ein leeres Feld ist „nicht angegeben" und nicht „suche den Benutzer mit dem leeren Namen".
                return pair.Value is string text && string.IsNullOrWhiteSpace(text) ? null : pair.Value;
            }

            return null;
        }

        /// <summary>Der Entitaets-Typ im Modell, mit einer Meldung, die die Ursache nennt.</summary>
        private static IEntityType Entity(DbContext db, Type clrType)
        {
            return db.Model.FindEntityType(clrType)
                   ?? throw new InvalidOperationException(
                       $"'{clrType.FullName}' is not part of the security model of " +
                       $"'{db.GetType().FullName}'. The user info handler was given a generic parameter that " +
                       "does not belong to this context.");
        }

        /// <summary>Der einzige Primaerschluessel eines Entitaets-Typs.</summary>
        private static IProperty SingleKey(DbContext db, Type clrType)
        {
            return OnlyKeyOf(Entity(db, clrType));
        }

        /// <summary>Der einzige Primaerschluessel - zusammengesetzte Schluessel kann dieser Handler nicht.</summary>
        private static IProperty OnlyKeyOf(IEntityType entity)
        {
            IKey key = entity.FindPrimaryKey()
                       ?? throw new InvalidOperationException(
                           $"'{entity.ClrType.FullName}' has no primary key.");
            if (key.Properties.Count != 1)
            {
                throw new InvalidOperationException(
                    $"'{entity.ClrType.FullName}' has a composite primary key - the user info handler " +
                    "addresses records by a single key.");
            }

            return key.Properties[0];
        }

        /// <summary>Bringt einen Bindungswert auf den Typ, den das Modell verlangt.</summary>
        private static object ConvertTo(object raw, Type target, string property, Type owner)
        {
            if (target.IsInstanceOfType(raw))
            {
                return raw;
            }

            if (TypeConverter.TryConvert(raw, target, out object converted))
            {
                return converted;
            }

            throw new InvalidOperationException(
                $"The value '{raw}' ({raw.GetType().FullName}) does not fit '{property}' of " +
                $"'{owner.FullName}' ({target.FullName}). Check the binding argument - or the host may be " +
                "missing the converter for this type.");
        }

        /// <summary>Ein Wert als Text, leer wird zu null.</summary>
        private static string Text(object value)
        {
            string text = value?.ToString();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        /// <summary>Ein Wert als Zahl, soweit er eine ist.</summary>
        private static int? Number(object value)
        {
            if (value == null)
            {
                return null;
            }

            return TypeConverter.TryConvert(value, typeof(int), out object converted) ? (int)converted : null;
        }

        /// <summary>Vor- und Nachname, soweit vorhanden.</summary>
        private static string Join(string first, string last)
        {
            string joined = string.Join(" ", new[] { first, last }.Where(n => !string.IsNullOrWhiteSpace(n)));
            return string.IsNullOrWhiteSpace(joined) ? null : joined;
        }
    }
}
