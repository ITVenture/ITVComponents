using ITVComponents.EFRepo.Extensions;
using ITVComponents.EFRepo.Options;
using ITVComponents.Json;
using ITVComponents.WebCoreToolkit.EntityFramework.Helpers.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.VirtualModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using TargetInterface = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.IHierarchySecurityContext<ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.HierarchyTenant, string, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.User, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.Role, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.Permission, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.UserRole, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.RolePermission,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.HierarchyTenantUser, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.RoleRole, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.GlobalRole, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.GlobalRolePermission, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.GRoleLRole, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.NavigationMenu, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.TenantNavigationMenu, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.DiagnosticsQuery,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.DiagnosticsQueryParameter, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.TenantDiagnosticsQuery, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.DashboardWidget, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.DashboardParam,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.DashboardWidgetLocalization, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.UserWidget
    , ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.CustomUserProperty, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.AssetTemplate, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.AssetTemplatePath, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.AssetTemplateGrant, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.AssetTemplateFeature,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.SharedAsset, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.SharedAssetUserFilter, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.SharedAssetTenantFilter, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.ClientAppTemplate, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.AppPermission,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.AppPermissionSet, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.ClientAppTemplatePermission, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.ClientApp, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.ClientAppPermission, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.ClientAppUser,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels.HierarchyWebPlugin, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels.HierarchyWebPluginConstant, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels.HierarchyWebPluginGenericParameter,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels.HierarchySequence, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels.HierarchyTenantSetting, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels.HierarchyTenantFeatureActivation,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels.HierarchyExternalOAuthService, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels.HierarchyExternalOAuthServiceState, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels.HierarchyExternalOAuthServiceTenantLogin,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Helpers.Models.HierarchyTenantContextSecurityTrustConfig>;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.PostgreSql.SyntaxHelper
{
    /// <summary>
    /// Die PostgreSQL-Fassung der hierarchischen Mandanten-Sicherheit. Gegenstueck zu
    /// <c>…TenantSecurity.SqlServer.CoreIdentityTree.SyntaxHelper.SqlColumnsSyntaxHelper</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Alle Bezeichner stehen in Anfuehrungszeichen.</b> T-SQL vergleicht Namen ohne Ruecksicht auf
    /// Gross- und Kleinschreibung, PostgreSQL faltet unquotierte Namen dagegen auf Kleinbuchstaben. EF
    /// legt die Tabellen und Spalten in gemischter Schreibweise an und findet die Objekte hier nur
    /// wieder, wenn sie <b>genau so</b> heissen. Ein vergessenes Anfuehrungszeichen faellt nicht beim
    /// Uebersetzen auf, sondern erst in der Datenbank.
    /// </para>
    /// <para>
    /// <b>Die Spaltennamen richten sich nach dem Modell, nicht nach der T-SQL-Vorlage.</b> Das ist an
    /// zwei Stellen ein Unterschied: <see cref="DownwardsUserRoleView{TUserId}.ViewPointTenantId"/>
    /// schreibt sich mit grossem P, die Vorlage schreibt <c>ViewpointTenantId</c>; und
    /// <see cref="UserAccessTree{TUserId}.DirectAssign"/> steht in der Vorlage als <c>directAssign</c>.
    /// Auf SQL Server ist das gleichgueltig, hier nicht.
    /// </para>
    /// </remarks>
    public static class PostgreSqlColumnsSyntaxHelper
    {
        /// <summary>
        /// Der Waechter, der <c>OPTION (MAXRECURSION)</c> ersetzt. Kein Objekt aus
        /// <see cref="GlobalDbObjectNaming"/>: der geteilte Code kennt ihn nicht, er gehoert allein
        /// dieser Datenbank.
        /// </summary>
        private const string RecursionGuardFunction = "TreeRecursionGuard";

        /// <summary>
        /// Die Anzahl REKURSIONSSCHRITTE, nach denen abgebrochen wird - dieselbe Zahl, die SQL Server ohne
        /// ausdrueckliches <c>OPTION (MAXRECURSION n)</c> vorgibt.
        /// </summary>
        /// <remarks>
        /// <b>Schritte, nicht Ebenen.</b> Der Anker liefert bereits Ebene 1, ohne einen Rekursionsschritt
        /// gebraucht zu haben; Ebene L entsteht also nach L-1 Schritten. Wer die Zahl direkt gegen die Ebene
        /// prueft, bricht eine Ebene zu frueh ab - nachgemessen: SQL Server traegt eine Kette von 101
        /// Mandanten und scheitert erst bei 102. Genau so lag die erste Fassung hier daneben, und der
        /// Gleichheitstest hat es nicht gezeigt, weil seine Hierarchie drei Ebenen tief war.
        /// </remarks>
        private const int MaxRecursionDepth = 100;

        /// <remarks>
        /// <b>Jede dieser Spalten braucht <c>stored: true</c>.</b> PostgreSQL kennt unterhalb von Version 18
        /// nur gespeicherte berechnete Spalten, und Npgsql baut daraus nicht still etwas anderes, sondern
        /// verweigert schon das Erzeugen der Migration. Auf SQL Server steht das Gegenstueck (<c>persisted</c>)
        /// im Ausdruck selbst - deshalb sieht die dortige Fassung an dieser Stelle anders aus.
        /// </remarks>
        public static void ConfigureComputedColumns<TContext>(DbContextModelBuilderOptions<TContext> builderOptions)
        {
            builderOptions.ConfigureComputedColumn<NavigationMenu, string>(n => n.UrlUniqueness,
                "case when COALESCE(\"Url\",'')='' and COALESCE(\"RefTag\",'')='' then 'MENU__'||cast(\"NavigationMenuId\" as character varying(10)) when COALESCE(\"Url\",'')='' then \"RefTag\" else \"Url\" end", stored: true);
            builderOptions.ConfigureComputedColumn<Role, string>(r => r.RoleNameUniqueness,
                "'__T'||cast(\"TenantId\" as character varying(10))||'##'||\"RoleName\"", stored: true);
            builderOptions.ConfigureComputedColumn<Permission, string>(p => p.PermissionNameUniqueness,
                "case when \"TenantId\" is null then \"PermissionName\" else '__T'||cast(\"TenantId\" as character varying(10))||'##'||\"PermissionName\" end", stored: true);
            builderOptions.ConfigureComputedColumn<HierarchyWebPlugin, string>(w => w.PluginNameUniqueness,
                "case when \"TenantId\" is null then \"UniqueName\" else '__T'||cast(\"TenantId\" as character varying(10))||'##'||\"UniqueName\" end", stored: true);
            builderOptions.ConfigureComputedColumn<HierarchyWebPluginConstant, string>(c => c.NameUniqueness,
                "case when \"TenantId\" is null then \"Name\" else '__T'||cast(\"TenantId\" as character varying(10))||'##'||\"Name\" end", stored: true);
            builderOptions.ConfigureComputedColumn<HierarchyExternalOAuthService, string>(s => s.CalculatedUniqueServiceName,
                "case when \"TenantId\" is null then 'GLOBAL'||\"UniqueConnectionName\" else 'T'||cast(\"TenantId\" as character varying(10))||'-'||\"UniqueConnectionName\" end", stored: true);
            ConfigureVirtualTables(builderOptions);
        }

        // -------------------------------------------------------------------------------------------
        // Die Bindung der Entitaeten an die Datenbank-Objekte. Reine Namen - deshalb Zeile fuer Zeile
        // dieselbe wie auf SQL Server. Genau das ist der Gewinn aus GlobalDbObjectNaming.
        // -------------------------------------------------------------------------------------------

        public static void ConfigureVirtualTables(IContextModelBuilderOptions builderOptions)
        {
            ConfigureUpwardsTree(builderOptions);
            ConfigureDownwardsTree(builderOptions);
            ConfigureAccessTree(builderOptions);
            ConfigureUpwardsRoleTree(builderOptions);
            ConfigureDownwardsRoleTree(builderOptions);
        }

        public static void ConfigureUpwardsTree(IContextModelBuilderOptions builderOptions)
        {
            builderOptions.ConfigureEntity<UpwardsTenantView>(uu =>
                uu.ToView(GlobalDbObjectNaming.UpwardsTenantTreeView).HasNoKey());
        }

        public static void ConfigureAccessTree(IContextModelBuilderOptions builderOptions)
        {
            builderOptions.ConfigureEntity<UserAccessTree<string>>(uat =>
                uat.ToView(GlobalDbObjectNaming.UserAccessTree).HasNoKey());
        }

        public static void ConfigureUpwardsRoleTree(IContextModelBuilderOptions builderOptions)
        {
            builderOptions.ConfigureDbFunction(GlobalDbObjectNaming.UpwardsRoleTreeForIdFunction,
                m => m.HasName(GlobalDbObjectNaming.UpwardsRoleTreeForIdFunction));
            builderOptions.ConfigureDbFunction(GlobalDbObjectNaming.UpwardsRoleTreeForLabelsFunction,
                m => m.HasName(GlobalDbObjectNaming.UpwardsRoleTreeForLabelsFunction));
            builderOptions.ConfigureDbFunction(GlobalDbObjectNaming.UpwardsRoleTreeForIdByLeafFunction,
                m => m.HasName(GlobalDbObjectNaming.UpwardsRoleTreeForIdByLeafFunction));
            builderOptions.ConfigureDbFunction(GlobalDbObjectNaming.UpwardsRoleTreeForLabelsByLeafFunction,
                m => m.HasName(GlobalDbObjectNaming.UpwardsRoleTreeForLabelsByLeafFunction));

            builderOptions.ConfigureEntity<UpwardsRoleUserView<string>>(b => b.HasNoKey().ToView(null));
        }

        public static void ConfigureDownwardsTree(IContextModelBuilderOptions builderOptions)
        {
            builderOptions.ConfigureEntity<DownwardsTenantView>(dd =>
                dd.ToView(GlobalDbObjectNaming.DownwardsTenantTreeView).HasNoKey());
        }

        public static void ConfigureDownwardsRoleTree(IContextModelBuilderOptions builderOptions)
        {
            builderOptions.ConfigureEntity<DownwardsUserRoleView<string>>(b => b.HasNoKey().ToView(null));
        }

        // -------------------------------------------------------------------------------------------
        // Das Methoden-Verzeichnis: die Zugriffe, die sich nicht ueber einen blossen Objekt-Namen
        // ausdruecken lassen.
        // -------------------------------------------------------------------------------------------

        public static void ConfigureMethods(IContextModelBuilderOptions bld)
        {
            bld.ConfigureMethod("SequenceNextVal", (DbContext c, string name, int tenantId) =>
            {
                var tmp = c.Database.SqlQuery<ValueTableModel<int>>(@"UPDATE ""Sequences""
    SET ""CurrentValue"" = case when ""CurrentValue""+""StepSize""<=""MaxValue"" then ""CurrentValue""+""StepSize"" when ""CurrentValue""+""StepSize"" > ""MaxValue"" and ""Cycle""=true then ""MinValue"" else -1 end
    WHERE ""SequenceName""= @name and ""TenantId"" = @tenantId
    RETURNING ""CurrentValue"" ""Value""", new NpgsqlParameter("name", name),
                    new NpgsqlParameter("tenantId", tenantId));
                return tmp.First().Value;
            });

            ConfigureChildTenantPermissionMethod(bld);
            ConfigureRoleTreeMethods(bld);
        }

        /// <summary>
        /// Die Zugriffe auf den Rollen-Baum. Der Unterschied zu T-SQL liegt allein in der Aufrufart:
        /// PostgreSQL kennt keine Prozedur, die eine Ergebnismenge liefert, deshalb steht hier eine
        /// Funktion und damit <c>select * from …</c> statt <c>exec …</c>.
        /// </summary>
        public static void ConfigureRoleTreeMethods(IContextModelBuilderOptions bld)
        {
            bld.ConfigureMethod<Func<DbContext, string, bool, string, IEnumerable<DownwardsUserRoleView<string>>>>(
                GlobalDbObjectNaming.DownwardsTenantUserRolesMethod,
                (c, userId, userIdIsLabels, viewpointTenant) =>
                    c.Set<DownwardsUserRoleView<string>>()
                        .FromSqlInterpolated(
                            $@"select * from ""GetDownwardsRoleTreeProc""({userId}, {userIdIsLabels}, {viewpointTenant})")
                        .ToList());

            // Je Benutzer die naechstgelegenen Zeilen des Baums nach oben. RANK() und nicht ROW_NUMBER,
            // damit alle Gleichstaende auf der kleinsten Ebene erhalten bleiben - genau wie in der
            // T-SQL-Fassung, die damit den frueheren zweifachen Baum-Durchlauf ersetzt hat.
            //
            // Liefert bewusst IQueryable und materialisiert NICHT: der Aufrufer haengt einen Join an,
            // und der gehoert in dieselbe Abfrage.
            bld.ConfigureMethod<Func<DbContext, string, string, int, IQueryable<UpwardsRoleUserView<string>>>>(
                GlobalDbObjectNaming.ClosestUpwardsRoleTreeByLabelsMethod,
                (c, labelsJson, forTenant, currentTenant) =>
                    c.Set<UpwardsRoleUserView<string>>().FromSqlInterpolated(
                        $@"SELECT ""UserId"",""ParentTenantId"",""ParentTenantName"",""TenantUserId"",""OutermostLeafTenantId"",""OutermostLeafTenantName"",""ParentLevel"",""OutermostRoleId""
FROM (
    SELECT f.""UserId"", f.""ParentTenantId"", f.""ParentTenantName"", f.""TenantUserId"", f.""OutermostLeafTenantId"", f.""OutermostLeafTenantName"", f.""ParentLevel"", f.""OutermostRoleId"",
           RANK() OVER (PARTITION BY f.""UserId"" ORDER BY f.""ParentLevel"") AS ""__rnk""
    FROM ""GetUpwardsRoleTreeForLabels""({labelsJson}, {forTenant}) f
    WHERE f.""OutermostLeafTenantId"" = {currentTenant}
) AS x
WHERE x.""__rnk"" = 1"));
        }

        public static void ConfigureChildTenantPermissionMethod(IContextModelBuilderOptions bld)
        {
            bld.ConfigureMethod<Func<DbContext, string, string, string[], HierarchyTenant[]>>(
                GlobalDbObjectNaming.ChildTenantsWithProc,
                (c, userId, currentTenant, requiredPermissions) =>
                {
                    var ctx = (TargetInterface)c;
                    return ctx.Tenants
                        .FromSql(
                            $@"select * from ""GetChildTenantsWithPermsProc""({userId}, false, {currentTenant}, {JsonHelper.ToJson(requiredPermissions, SerializationTypingMode.StaticTyping)})")
                        .ToArray();
                });

            bld.ConfigureMethod<Func<DbContext, string[], string, string[], HierarchyTenant[]>>(
                GlobalDbObjectNaming.ChildTenantsWithProc,
                (c, userLabels, currentTenant, requiredPermissions) =>
                {
                    var ctx = (TargetInterface)c;
                    return ctx.Tenants
                        .FromSql(
                            $@"select * from ""GetChildTenantsWithPermsProc""({JsonHelper.ToJson(userLabels, SerializationTypingMode.StaticTyping)}, true, {currentTenant}, {JsonHelper.ToJson(requiredPermissions, SerializationTypingMode.StaticTyping)})")
                        .ToArray();
                });

            //--

            bld.ConfigureMethod<Func<DbContext, string, int?, string[], HierarchyTenant[]>>(
                GlobalDbObjectNaming.ChildTenantsWithProc,
                (c, userId, currentTenant, requiredPermissions) =>
                {
                    var ctx = (TargetInterface)c;
                    return ctx.Tenants
                        .FromSql(
                            $@"select * from ""GetChildTenantsWithPermsByVpIdProc""({userId}, false, {currentTenant}, {JsonHelper.ToJson(requiredPermissions, SerializationTypingMode.StaticTyping)})")
                        .ToArray();
                });

            bld.ConfigureMethod<Func<DbContext, string[], int?, string[], HierarchyTenant[]>>(
                GlobalDbObjectNaming.ChildTenantsWithProc,
                (c, userLabels, currentTenant, requiredPermissions) =>
                {
                    var ctx = (TargetInterface)c;
                    return ctx.Tenants
                        .FromSql(
                            $@"select * from ""GetChildTenantsWithPermsByVpIdProc""({JsonHelper.ToJson(userLabels, SerializationTypingMode.StaticTyping)}, true, {currentTenant}, {JsonHelper.ToJson(requiredPermissions, SerializationTypingMode.StaticTyping)})")
                        .ToArray();
                });
        }

        // -------------------------------------------------------------------------------------------
        // Die Datenbank-Objekte.
        // -------------------------------------------------------------------------------------------

        public static void ConfigureViews(MigrationBuilder migrationBuilder, string schema = "public")
        {
            DropExistingObjects(migrationBuilder, schema);

            migrationBuilder.Sql(RecursionGuard(schema));

            migrationBuilder.Sql(UpwardsTenantTreeView(schema));
            migrationBuilder.Sql(DownwardsTenantTreeView(schema));
            migrationBuilder.Sql(TenantAccessTreeDownView(schema));
            migrationBuilder.Sql(TenantAccessTreeUpView(schema));
            migrationBuilder.Sql(TenantAccessTreeView(schema));

            migrationBuilder.Sql(EffectiveTenantUserRolesFunction(schema));

            migrationBuilder.Sql(UpwardsRoleTreeFunction(schema,
                GlobalDbObjectNaming.UpwardsRoleTreeForIdFunction, byLabels: false, leafById: false));
            migrationBuilder.Sql(UpwardsRoleTreeFunction(schema,
                GlobalDbObjectNaming.UpwardsRoleTreeForLabelsFunction, byLabels: true, leafById: false));
            migrationBuilder.Sql(UpwardsRoleTreeFunction(schema,
                GlobalDbObjectNaming.UpwardsRoleTreeForIdByLeafFunction, byLabels: false, leafById: true));
            migrationBuilder.Sql(UpwardsRoleTreeFunction(schema,
                GlobalDbObjectNaming.UpwardsRoleTreeForLabelsByLeafFunction, byLabels: true, leafById: true));

            migrationBuilder.Sql(DownwardsRoleTreeFunction(schema,
                GlobalDbObjectNaming.DownwardsRoleTreeProcName, leafById: false));
            migrationBuilder.Sql(DownwardsRoleTreeFunction(schema,
                GlobalDbObjectNaming.DownwardsRoleTreeByVpIdProcName, leafById: true));

            migrationBuilder.Sql(ChildTenantsWithPermsFunction(schema,
                GlobalDbObjectNaming.ChildTenantsWithPermsProcName,
                GlobalDbObjectNaming.DownwardsRoleTreeProcName, leafById: false));
            migrationBuilder.Sql(ChildTenantsWithPermsFunction(schema,
                GlobalDbObjectNaming.ChildTenantsWithPermsByVpIdProcName,
                GlobalDbObjectNaming.DownwardsRoleTreeByVpIdProcName, leafById: true));
        }

        /// <summary>
        /// Raeumt die Objekte weg, bevor sie neu entstehen - damit ein erneuter Lauf auf einer
        /// bestehenden Datenbank durchgeht und nicht an einem schon vorhandenen Objekt scheitert.
        /// </summary>
        /// <remarks>
        /// Die Reihenfolge ist nicht beliebig: PostgreSQL merkt sich, welche View auf welcher aufbaut,
        /// und verweigert das Loeschen, solange eine abhaengige View steht. Funktionen sind davon nicht
        /// betroffen - ihr Rumpf ist fuer die Datenbank blosser Text -, sie stehen hier trotzdem in
        /// derselben Ordnung, damit die Abhaengigkeiten ablesbar bleiben.
        /// </remarks>
        private static void DropExistingObjects(MigrationBuilder migrationBuilder, string schema)
        {
            migrationBuilder.Sql($@"DROP VIEW IF EXISTS ""{schema}"".""{GlobalDbObjectNaming.UserAccessTree}""");
            migrationBuilder.Sql($@"DROP VIEW IF EXISTS ""{schema}"".""{GlobalDbObjectNaming.UserAccessTreeUpView}""");
            migrationBuilder.Sql($@"DROP VIEW IF EXISTS ""{schema}"".""{GlobalDbObjectNaming.UserAccessTreeDownView}""");
            migrationBuilder.Sql($@"DROP VIEW IF EXISTS ""{schema}"".""{GlobalDbObjectNaming.UpwardsTenantTreeView}""");
            migrationBuilder.Sql($@"DROP VIEW IF EXISTS ""{schema}"".""{GlobalDbObjectNaming.DownwardsTenantTreeView}""");

            migrationBuilder.Sql($@"DROP FUNCTION IF EXISTS ""{schema}"".""{GlobalDbObjectNaming.ChildTenantsWithPermsProcName}""");
            migrationBuilder.Sql($@"DROP FUNCTION IF EXISTS ""{schema}"".""{GlobalDbObjectNaming.ChildTenantsWithPermsByVpIdProcName}""");
            migrationBuilder.Sql($@"DROP FUNCTION IF EXISTS ""{schema}"".""{GlobalDbObjectNaming.DownwardsRoleTreeProcName}""");
            migrationBuilder.Sql($@"DROP FUNCTION IF EXISTS ""{schema}"".""{GlobalDbObjectNaming.DownwardsRoleTreeByVpIdProcName}""");
            migrationBuilder.Sql($@"DROP FUNCTION IF EXISTS ""{schema}"".""{GlobalDbObjectNaming.UpwardsRoleTreeForIdFunction}""");
            migrationBuilder.Sql($@"DROP FUNCTION IF EXISTS ""{schema}"".""{GlobalDbObjectNaming.UpwardsRoleTreeForLabelsFunction}""");
            migrationBuilder.Sql($@"DROP FUNCTION IF EXISTS ""{schema}"".""{GlobalDbObjectNaming.UpwardsRoleTreeForIdByLeafFunction}""");
            migrationBuilder.Sql($@"DROP FUNCTION IF EXISTS ""{schema}"".""{GlobalDbObjectNaming.UpwardsRoleTreeForLabelsByLeafFunction}""");
            migrationBuilder.Sql($@"DROP FUNCTION IF EXISTS ""{schema}"".""{GlobalDbObjectNaming.EffectiveTenantUserRolesFunction}""");
            migrationBuilder.Sql($@"DROP FUNCTION IF EXISTS ""{schema}"".""{RecursionGuardFunction}""");
        }

        /// <summary>
        /// Der Ersatz fuer <c>OPTION (MAXRECURSION)</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// SQL Server bricht eine rekursive Abfrage ohne ausdrueckliche Vorgabe nach 100 Ebenen mit
        /// einem Fehler ab. Das ist keine Nebensache, sondern eine <b>Zusage</b>: ein Zyklus in der
        /// Mandanten- oder Rollen-Hierarchie faellt sofort und laut auf. PostgreSQL kennt keine solche
        /// Grenze - dieselbe Abfrage liefe dort endlos, bis der Arbeitsspeicher ausgeht.
        /// </para>
        /// <para>
        /// Deshalb ein Waechter, der bei jeder Ebene mitgezaehlt wird und beim Ueberschreiten wirft.
        /// Bewusst <b>nicht</b> die <c>CYCLE</c>-Klausel: die bricht die Rekursion still ab und liefert
        /// ein unvollstaendiges Ergebnis. Ein Zyklus ist hier aber ein Datenfehler und kein Sonderfall,
        /// den man wegsortiert - ein stilles Teilergebnis waere schlimmer als der Abbruch, weil dann
        /// jemand mit zu wenig Rechten weiterarbeitet, ohne dass es auffaellt.
        /// </para>
        /// </remarks>
        private static string RecursionGuard(string schema)
            => $$"""
                 CREATE FUNCTION "{{schema}}"."{{RecursionGuardFunction}}"(p_level integer, p_context text)
                 RETURNS integer
                 LANGUAGE plpgsql
                 IMMUTABLE
                 PARALLEL SAFE
                 AS $guard$
                 BEGIN
                     -- p_level ist die ERREICHTE EBENE, {{MaxRecursionDepth}} sind REKURSIONSSCHRITTE.
                     -- Ebene 1 kommt vom Anker, ohne Schritt - deshalb p_level - 1. Ohne dieses Minus
                     -- braeche PostgreSQL eine Ebene frueher ab als SQL Server (nachgemessen).
                     IF p_level - 1 > {{MaxRecursionDepth}} THEN
                         RAISE EXCEPTION 'Mehr als {{MaxRecursionDepth}} Rekursionsschritte in % - die Hierarchie ist tiefer als zulaessig oder enthaelt einen Zyklus. (Ebene %, der Anker zaehlt nicht als Schritt.)', p_context, p_level
                             USING ERRCODE = '54001';
                     END IF;
                     RETURN p_level;
                 END;
                 $guard$
                 """;

        private static string UpwardsTenantTreeView(string schema)
            => $$"""
                 CREATE VIEW "{{schema}}"."{{GlobalDbObjectNaming.UpwardsTenantTreeView}}" AS
                 WITH RECURSIVE r AS (
                     SELECT "TenantId" AS "OutermostLeafTenantId", "TenantName" AS "OutermostLeafTenantName",
                            "TenantId" AS "ParentTenantId", "TenantName" AS "ParentTenantName", 1 AS "ParentLevel",
                            "ParentTenantId" AS "NextParent"
                     FROM "{{schema}}"."Tenants"
                     UNION ALL
                     SELECT r_2."OutermostLeafTenantId", r_2."OutermostLeafTenantName",
                            u."TenantId", u."TenantName",
                            "{{schema}}"."{{RecursionGuardFunction}}"(r_2."ParentLevel" + 1, '{{GlobalDbObjectNaming.UpwardsTenantTreeView}}'),
                            u."ParentTenantId"
                     FROM "{{schema}}"."Tenants" AS u
                     INNER JOIN r AS r_2 ON u."TenantId" = r_2."NextParent"
                 )
                 SELECT r_1."OutermostLeafTenantId", r_1."OutermostLeafTenantName", r_1."ParentTenantId",
                        r_1."ParentTenantName", r_1."ParentLevel"
                 FROM r AS r_1
                 """;

        private static string DownwardsTenantTreeView(string schema)
            => $$"""
                 CREATE VIEW "{{schema}}"."{{GlobalDbObjectNaming.DownwardsTenantTreeView}}" AS
                 WITH RECURSIVE r AS (
                     SELECT "TenantId" AS "TopmostTenantId", "TenantName" AS "TopmostTenantName",
                            "TenantId" AS "ChildTenantId", "TenantName" AS "ChildTenantName", 1 AS "ChildLevel"
                     FROM "{{schema}}"."Tenants"
                     UNION ALL
                     SELECT r."TopmostTenantId", r."TopmostTenantName",
                            u."TenantId", u."TenantName",
                            "{{schema}}"."{{RecursionGuardFunction}}"(r."ChildLevel" + 1, '{{GlobalDbObjectNaming.DownwardsTenantTreeView}}')
                     FROM "{{schema}}"."Tenants" AS u
                     INNER JOIN r ON r."ChildTenantId" = u."ParentTenantId"
                 )
                 SELECT r."TopmostTenantId", r."TopmostTenantName", r."ChildTenantId", r."ChildTenantName", r."ChildLevel"
                 FROM r
                 """;

        private static string TenantAccessTreeDownView(string schema)
            => $$"""
                 CREATE VIEW "{{schema}}"."{{GlobalDbObjectNaming.UserAccessTreeDownView}}" AS
                 WITH RECURSIVE "rDown" AS (
                     SELECT t."TenantId" AS "TopmostTenantId", t."TenantName" AS "TopmostTenantName",
                            s."RoleId" AS "TopmostSecurityRole", tu."TenantUserId", tu."UserId",
                            t."TenantId" AS "ChildTenantId", t."TenantName" AS "ChildTenantName",
                            s."RoleId" AS "ChildTenantRole", s."RoleId" AS "NextParentRoleId", 1 AS "level"
                     FROM "{{schema}}"."Tenants" t
                     INNER JOIN "{{schema}}"."TenantUsers" tu ON tu."TenantId" = t."TenantId"
                     INNER JOIN "{{schema}}"."SecurityRoles" s ON s."TenantId" = t."TenantId"
                     INNER JOIN "{{schema}}"."TenantUserRoles" tur ON tur."TenantUserId" = tu."TenantUserId" AND tur."RoleId" = s."RoleId"
                     UNION ALL
                     SELECT r_2."TopmostTenantId", r_2."TopmostTenantName", r_2."TopmostSecurityRole",
                            r_2."TenantUserId", r_2."UserId",
                            tc."TenantId", tc."TenantName", ts."RoleId", ts."RoleId",
                            "{{schema}}"."{{RecursionGuardFunction}}"(r_2."level" + 1, '{{GlobalDbObjectNaming.UserAccessTreeDownView}}')
                     FROM "rDown" r_2
                     INNER JOIN "{{schema}}"."Tenants" tc ON tc."ParentTenantId" = r_2."ChildTenantId"
                     INNER JOIN "{{schema}}"."SecurityRoles" ts ON ts."TenantId" = tc."TenantId"
                     INNER JOIN "{{schema}}"."RoleRoles" tcr ON tcr."PermittedRoleId" = r_2."NextParentRoleId" AND tcr."PermissiveRoleId" = ts."RoleId"
                 )
                 SELECT * FROM "rDown"
                 """;

        private static string TenantAccessTreeUpView(string schema)
            => $$"""
                 CREATE VIEW "{{schema}}"."{{GlobalDbObjectNaming.UserAccessTreeUpView}}" AS
                 WITH RECURSIVE "rUp" AS (
                     SELECT s."RoleId", s."RoleId" AS "ParentRoleId", s."TenantId", s."TenantId" AS "ParentTenantId",
                            st."ParentTenantId" AS "nextparent", s."RoleId" AS "nextChildRole", 1 AS "level"
                     FROM "{{schema}}"."SecurityRoles" s
                     INNER JOIN "{{schema}}"."Tenants" st ON st."TenantId" = s."TenantId"
                     UNION ALL
                     SELECT r_2."RoleId", pr."RoleId", r_2."TenantId", pr."TenantId",
                            pt."ParentTenantId", pr."RoleId",
                            "{{schema}}"."{{RecursionGuardFunction}}"(r_2."level" + 1, '{{GlobalDbObjectNaming.UserAccessTreeUpView}}')
                     FROM "rUp" AS r_2
                     INNER JOIN "{{schema}}"."RoleRoles" roro ON r_2."nextChildRole" = roro."PermissiveRoleId"
                     INNER JOIN "{{schema}}"."SecurityRoles" pr ON pr."RoleId" = roro."PermittedRoleId" AND pr."TenantId" = r_2."nextparent"
                     INNER JOIN "{{schema}}"."Tenants" ct ON ct."TenantId" = r_2."TenantId"
                     INNER JOIN "{{schema}}"."Tenants" pt ON pt."TenantId" = pr."TenantId" AND pr."TenantId" = r_2."nextparent"
                 )
                 SELECT * FROM "rUp"
                 """;

        /// <remarks>
        /// Der linke Verbund auf <c>TenantAccessTreeUp</c> steht so schon in der T-SQL-Fassung: er
        /// liefert keine Spalte ins Ergebnis und geht auch nicht in die Reihenfolge ein - er kann Zeilen
        /// nur vervielfachen, und genau die faengt <c>ROW_NUMBER</c> wieder ein. Bewusst
        /// <b>unveraendert</b> uebernommen: eine Vereinfachung waere eine inhaltliche Aenderung, und die
        /// gehoert nicht in eine Uebersetzung.
        /// </remarks>
        private static string TenantAccessTreeView(string schema)
            => $$"""
                 CREATE VIEW "{{schema}}"."{{GlobalDbObjectNaming.UserAccessTree}}" AS
                 WITH ranked AS (
                     SELECT t."OutermostLeafTenantId", t."OutermostLeafTenantName", t."ParentTenantId",
                            t."ParentTenantName", t."ParentLevel",
                            d."TopmostTenantId", d."TopmostTenantName", d."TenantUserId", d."UserId",
                            d."ChildTenantId", d."ChildTenantName", d."level",
                            ROW_NUMBER() OVER (
                                PARTITION BY d."UserId", t."OutermostLeafTenantId", d."ChildTenantId"
                                ORDER BY t."ParentLevel" DESC
                            ) AS "rn"
                     FROM "{{schema}}"."{{GlobalDbObjectNaming.UpwardsTenantTreeView}}" t
                     INNER JOIN "{{schema}}"."{{GlobalDbObjectNaming.UserAccessTreeDownView}}" d ON d."ChildTenantId" = t."ParentTenantId"
                     LEFT OUTER JOIN ("{{schema}}"."{{GlobalDbObjectNaming.UserAccessTreeUpView}}" u
                                      INNER JOIN "{{schema}}"."TenantUsers" tut ON tut."TenantId" = u."ParentTenantId")
                         ON d."ChildTenantId" = u."ParentTenantId"
                         AND d."ChildTenantRole" = u."ParentRoleId"
                         AND tut."UserId" = d."UserId"
                         AND d."ChildTenantId" = t."OutermostLeafTenantId"
                 )
                 SELECT ranked."OutermostLeafTenantId", ranked."OutermostLeafTenantName", ranked."ParentTenantId",
                        ranked."ParentTenantName", ranked."ParentLevel", ranked."TopmostTenantId",
                        ranked."TopmostTenantName", ranked."TenantUserId", ranked."UserId",
                        ranked."ChildTenantId", ranked."ChildTenantName",
                        CASE WHEN ranked."level" = 1 THEN true ELSE false END AS "DirectAssign"
                 FROM ranked
                 WHERE ranked."rn" = 1
                 """;

        /// <remarks>
        /// „Diskrete Weitergabe": die wirksamen Rollen EINES Mandanten-Benutzers, also seine direkten
        /// Rollen erweitert um die Huelle der Rollen-Rollen INNERHALB desselben Mandanten. Siehe
        /// docs/ISSUE-MLM-PermissionSet-CrossTenant-Propagation.md.
        /// <para>
        /// Gegenueber der T-SQL-Vorlage ist eine Ebenen-Zaehlung dazugekommen, die im Ergebnis nicht
        /// vorkommt. Sie traegt allein den Waechter: auch diese Rekursion ist auf SQL Server durch die
        /// Vorgabe von 100 Ebenen gedeckelt, und ein Ring aus Rollen-Rollen innerhalb eines Mandanten
        /// liefe hier sonst endlos.
        /// </para>
        /// </remarks>
        private static string EffectiveTenantUserRolesFunction(string schema)
            => $$"""
                 CREATE FUNCTION "{{schema}}"."{{GlobalDbObjectNaming.EffectiveTenantUserRolesFunction}}"(p_tenant_user_id integer)
                 RETURNS TABLE("RoleId" integer)
                 LANGUAGE sql
                 STABLE
                 AS $fn$
                     WITH RECURSIVE effroles AS (
                         SELECT tur."RoleId", 1 AS "level"
                         FROM "{{schema}}"."TenantUserRoles" tur
                         WHERE tur."TenantUserId" = p_tenant_user_id AND tur."RoleId" IS NOT NULL
                         UNION ALL
                         SELECT cc."RoleId",
                                "{{schema}}"."{{RecursionGuardFunction}}"(e."level" + 1, '{{GlobalDbObjectNaming.EffectiveTenantUserRolesFunction}}')
                         FROM effroles e
                         INNER JOIN "{{schema}}"."RoleRoles" roro ON roro."PermittedRoleId" = e."RoleId"
                         INNER JOIN "{{schema}}"."SecurityRoles" pp ON pp."RoleId" = e."RoleId"
                         INNER JOIN "{{schema}}"."SecurityRoles" cc ON cc."RoleId" = roro."PermissiveRoleId" AND cc."TenantId" = pp."TenantId"
                     )
                     SELECT DISTINCT e."RoleId" FROM effroles e
                 $fn$
                 """;

        /// <summary>
        /// Der Rollen-Baum nach OBEN, in vier Auspraegungen: gesucht ueber die Benutzer-Id oder ueber
        /// Kennzeichen, eingeschraenkt auf ein Blatt ueber dessen Namen oder dessen Id.
        /// </summary>
        /// <remarks>
        /// Die T-SQL-Vorlage schreibt alle vier vollstaendig aus. Hier steht dafuer eine Fassung mit zwei
        /// Schaltern: der Rumpf ist in allen vier Faellen derselbe, und vier Abschriften, die sich um
        /// zwei Zeilen unterscheiden, laufen mit der Zeit auseinander.
        /// </remarks>
        private static string UpwardsRoleTreeFunction(string schema, string functionName, bool byLabels, bool leafById)
        {
            var leafType = leafById ? "integer" : "text";
            var anchorLeafFilter = leafById
                ? @"p_from_leaf IS NULL OR s.""TenantId"" = p_from_leaf"
                : @"p_from_leaf IS NULL OR st.""TenantName"" = p_from_leaf";
            var resultLeafFilter = leafById
                ? @"t.""OutermostLeafTenantId"" = p_from_leaf OR p_from_leaf IS NULL"
                : @"t.""OutermostLeafTenantName"" = p_from_leaf OR p_from_leaf IS NULL";

            // Ueber Kennzeichen: die Liste kommt als JSON-Feld herein und wird zu Zeilen aufgefaltet.
            // openjson(@UserId) with ([value] nvarchar(150) '$') heisst hier json_array_elements_text.
            var labelJoin = byLabels
                ? $@"
    INNER JOIN json_array_elements_text(p_user_id::json) uta ON u.""NormalizedUserName"" = uta.value"
                : string.Empty;
            var userFilter = byLabels
                ? $"({resultLeafFilter})"
                : $"u.\"Id\" = p_user_id AND ({resultLeafFilter})";

            return $$"""
                     CREATE FUNCTION "{{schema}}"."{{functionName}}"(p_user_id text, p_from_leaf {{leafType}})
                     RETURNS TABLE("OutermostLeafTenantId" integer, "OutermostLeafTenantName" text,
                                   "ParentTenantId" integer, "ParentTenantName" text, "ParentRoleId" integer,
                                   "ParentLevel" integer, "TenantUserId" integer, "UserId" text,
                                   "OutermostRoleId" integer)
                     LANGUAGE sql
                     STABLE
                     AS $fn$
                         WITH RECURSIVE r AS (
                             SELECT s."RoleId", s."RoleId" AS "ParentRoleId", s."TenantId", s."TenantId" AS "ParentTenantId",
                                    st."ParentTenantId" AS "nextparent", s."RoleId" AS "nextChildRole", 1 AS "level"
                             FROM "{{schema}}"."SecurityRoles" s
                             INNER JOIN "{{schema}}"."Tenants" st ON st."TenantId" = s."TenantId"
                             WHERE {{anchorLeafFilter}}
                             UNION ALL
                             SELECT r_2."RoleId", pr."RoleId", r_2."TenantId", pr."TenantId",
                                    pt."ParentTenantId", pr."RoleId",
                                    "{{schema}}"."{{RecursionGuardFunction}}"(r_2."level" + 1, '{{functionName}}')
                             FROM r AS r_2
                             INNER JOIN "{{schema}}"."RoleRoles" roro ON r_2."nextChildRole" = roro."PermissiveRoleId"
                             INNER JOIN "{{schema}}"."SecurityRoles" pr ON pr."RoleId" = roro."PermittedRoleId" AND pr."TenantId" = r_2."nextparent"
                             INNER JOIN "{{schema}}"."Tenants" ct ON ct."TenantId" = r_2."TenantId"
                             INNER JOIN "{{schema}}"."Tenants" pt ON pt."TenantId" = pr."TenantId" AND pr."TenantId" = r_2."nextparent"
                         )
                         SELECT t."OutermostLeafTenantId", t."OutermostLeafTenantName", t."ParentTenantId",
                                t."ParentTenantName", pr."RoleId", t."ParentLevel", tu."TenantUserId",
                                u."Id", cr."RoleId"
                         FROM "{{schema}}"."{{GlobalDbObjectNaming.UpwardsTenantTreeView}}" t
                         INNER JOIN r ON r."TenantId" = t."OutermostLeafTenantId" AND r."ParentTenantId" = t."ParentTenantId"
                         INNER JOIN "{{schema}}"."TenantUsers" tu ON tu."TenantId" = t."ParentTenantId"
                         INNER JOIN "{{schema}}"."Users" u ON u."Id" = tu."UserId"{{labelJoin}}
                         INNER JOIN "{{schema}}"."SecurityRoles" cr ON cr."TenantId" = r."TenantId" AND cr."RoleId" = r."RoleId"
                         INNER JOIN "{{schema}}"."SecurityRoles" pr ON pr."TenantId" = r."ParentTenantId" AND pr."RoleId" = r."ParentRoleId"
                         CROSS JOIN LATERAL (
                             SELECT 1 AS m
                             FROM "{{schema}}"."{{GlobalDbObjectNaming.EffectiveTenantUserRolesFunction}}"(tu."TenantUserId") er
                             WHERE er."RoleId" = pr."RoleId"
                             LIMIT 1
                         ) tur
                         WHERE {{userFilter}}
                     $fn$
                     """;
        }

        /// <summary>
        /// Der Rollen-Baum nach UNTEN. Auf SQL Server eine Prozedur mit Tabellen-Variablen, hier eine
        /// Funktion aus einer einzigen Abfrage.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Zwei Stellen weichen von der Vorlage ab, ohne das Ergebnis zu aendern:
        /// </para>
        /// <para>
        /// Erstens die Verzweigung ueber <c>p_user_is_labels</c>. Die Vorlage schreibt <c>if … else</c>
        /// und fuellt eine Tabellen-Variable; hier stehen beide Zweige als Vereinigung, jeder mit dem
        /// Schalter als Bedingung. PostgreSQL erkennt den nicht zutreffenden Zweig als dauerhaft falsch
        /// und ruft die Funktion dahinter gar nicht erst auf.
        /// </para>
        /// <para>
        /// Zweitens der „Shrinker": die Vorlage sammelt je (Blatt, Benutzer) das kleinste
        /// <c>ParentLevel</c> und verknuepft dann ein zweites Mal darauf. <c>RANK() = 1</c> waehlt
        /// dieselben Zeilen in einem Durchgang - RANK und nicht ROW_NUMBER, damit Gleichstaende auf der
        /// kleinsten Ebene alle erhalten bleiben, genau wie beim Gleichheits-Verbund der Vorlage.
        /// </para>
        /// </remarks>
        private static string DownwardsRoleTreeFunction(string schema, string functionName, bool leafById)
        {
            var leafType = leafById ? "integer" : "text";
            var labelsFunction = leafById
                ? GlobalDbObjectNaming.UpwardsRoleTreeForLabelsByLeafFunction
                : GlobalDbObjectNaming.UpwardsRoleTreeForLabelsFunction;
            var idFunction = leafById
                ? GlobalDbObjectNaming.UpwardsRoleTreeForIdByLeafFunction
                : GlobalDbObjectNaming.UpwardsRoleTreeForIdFunction;

            return $$"""
                     CREATE FUNCTION "{{schema}}"."{{functionName}}"(p_user_id text, p_user_is_labels boolean, p_view_point {{leafType}})
                     RETURNS TABLE("ViewPointTenantId" integer, "ViewpointTenantName" text,
                                   "TopmostTenantId" integer, "TopmostTenantName" text,
                                   "ChildTenantId" integer, "ChildTenantName" text,
                                   "TenantUserId" integer, "UserId" text, "ResultingChildRoleId" integer,
                                   "ChildLevel" integer, "TopmostParentLevel" integer)
                     LANGUAGE sql
                     STABLE
                     AS $fn$
                         WITH RECURSIVE "completeUpTree" AS (
                             SELECT * FROM "{{schema}}"."{{labelsFunction}}"(p_user_id, p_view_point) WHERE p_user_is_labels
                             UNION ALL
                             SELECT * FROM "{{schema}}"."{{idFunction}}"(p_user_id, p_view_point) WHERE NOT p_user_is_labels
                         ),
                         "resultingUpTree" AS (
                             SELECT s."OutermostLeafTenantId", s."OutermostLeafTenantName", s."ParentTenantId",
                                    s."ParentTenantName", s."ParentRoleId", s."ParentLevel", s."TenantUserId",
                                    s."UserId", s."OutermostRoleId"
                             FROM (
                                 SELECT c.*,
                                        RANK() OVER (PARTITION BY c."OutermostLeafTenantId", c."OutermostLeafTenantName", c."UserId"
                                                     ORDER BY c."ParentLevel") AS "__rnk"
                                 FROM "completeUpTree" c
                             ) s
                             WHERE s."__rnk" = 1
                         ),
                         "rawTree" AS (
                             SELECT a."ParentTenantId" AS "TopmostTenantId", a."ParentTenantName" AS "TopmostTenantName",
                                    a."ParentRoleId" AS "TopmostRoleId",
                                    a."OutermostLeafTenantId" AS "ViewPointTenantId",
                                    a."OutermostLeafTenantName" AS "ViewpointTenantName",
                                    d."ChildTenantId", d."ChildTenantName", d."ChildLevel", a."UserId", a."OutermostRoleId"
                             FROM "resultingUpTree" a
                             INNER JOIN "{{schema}}"."{{GlobalDbObjectNaming.DownwardsTenantTreeView}}" d ON d."TopmostTenantId" = a."OutermostLeafTenantId"
                         ),
                         r AS (
                             SELECT s."RoleId", s."RoleId" AS "ParentRoleId", s."TenantId", s."TenantId" AS "ParentTenantId",
                                    st."ParentTenantId" AS "nextparent", s."RoleId" AS "nextChildRole", 1 AS "level"
                             FROM "{{schema}}"."SecurityRoles" s
                             INNER JOIN "{{schema}}"."Tenants" st ON st."TenantId" = s."TenantId"
                             WHERE s."TenantId" IN (SELECT rt."ChildTenantId" FROM "rawTree" rt)
                             UNION ALL
                             SELECT r_2."RoleId", pr."RoleId", r_2."TenantId", pr."TenantId",
                                    pt."ParentTenantId", pr."RoleId",
                                    "{{schema}}"."{{RecursionGuardFunction}}"(r_2."level" + 1, '{{functionName}}')
                             FROM r AS r_2
                             INNER JOIN "{{schema}}"."RoleRoles" roro ON r_2."nextChildRole" = roro."PermissiveRoleId"
                             INNER JOIN "{{schema}}"."SecurityRoles" pr ON pr."RoleId" = roro."PermittedRoleId" AND pr."TenantId" = r_2."nextparent"
                             INNER JOIN "{{schema}}"."Tenants" ct ON ct."TenantId" = r_2."TenantId"
                             INNER JOIN "{{schema}}"."Tenants" pt ON pt."TenantId" = pr."TenantId" AND pr."TenantId" = r_2."nextparent"
                         )
                         SELECT d."ViewPointTenantId", d."ViewpointTenantName", r."ParentTenantId", t."ParentTenantName",
                                t."OutermostLeafTenantId", t."OutermostLeafTenantName", tu."TenantUserId", u."Id",
                                r."RoleId", d."ChildLevel", t."ParentLevel"
                         FROM "{{schema}}"."{{GlobalDbObjectNaming.UpwardsTenantTreeView}}" t
                         INNER JOIN r ON r."TenantId" = t."OutermostLeafTenantId" AND r."ParentTenantId" = t."ParentTenantId"
                         INNER JOIN "{{schema}}"."TenantUsers" tu ON tu."TenantId" = t."ParentTenantId"
                         INNER JOIN "{{schema}}"."Users" u ON u."Id" = tu."UserId"
                         INNER JOIN "{{schema}}"."SecurityRoles" cr ON cr."TenantId" = r."TenantId" AND cr."RoleId" = r."RoleId"
                         INNER JOIN "{{schema}}"."SecurityRoles" pr ON pr."TenantId" = r."ParentTenantId" AND pr."RoleId" = r."ParentRoleId"
                         CROSS JOIN LATERAL (
                             SELECT 1 AS m
                             FROM "{{schema}}"."{{GlobalDbObjectNaming.EffectiveTenantUserRolesFunction}}"(tu."TenantUserId") er
                             WHERE er."RoleId" = pr."RoleId"
                             LIMIT 1
                         ) tur
                         INNER JOIN "rawTree" d ON d."TopmostTenantId" = r."ParentTenantId"
                             AND d."ChildTenantId" = t."OutermostLeafTenantId"
                             AND d."UserId" = u."Id"
                             AND d."TopmostRoleId" = pr."RoleId"
                     $fn$
                     """;
        }

        /// <summary>
        /// Die Kind-Mandanten, in denen ein Benutzer bestimmte Berechtigungen hat. Liefert die Spalten
        /// des Mandanten, weil der Aufrufer daraus Entitaeten macht.
        /// </summary>
        private static string ChildTenantsWithPermsFunction(string schema, string functionName, string downwardsFunction, bool leafById)
        {
            var leafType = leafById ? "integer" : "text";

            return $$"""
                     CREATE FUNCTION "{{schema}}"."{{functionName}}"(p_user_id text, p_user_is_labels boolean, p_view_point {{leafType}}, p_required_permissions text)
                     RETURNS TABLE("TenantId" integer, "ParentTenantId" integer, "TenantName" text,
                                   "DisplayName" text, "TenantPassword" text, "TimeZone" text,
                                   "TenantTypeId" integer, "TenantDirty" boolean)
                     LANGUAGE sql
                     STABLE
                     AS $fn$
                         WITH "rtQuery" AS (
                             SELECT * FROM "{{schema}}"."{{downwardsFunction}}"(p_user_id, p_user_is_labels, p_view_point)
                         ),
                         "permRaw" AS (
                             SELECT pr.value FROM json_array_elements_text(p_required_permissions::json) pr
                         ),
                         "perm2T" AS (
                             SELECT p."PermissionId", q."ChildTenantId"
                             FROM "rtQuery" q
                             INNER JOIN "{{schema}}"."RolePermissions" rp ON rp."RoleId" = q."ResultingChildRoleId"
                             INNER JOIN "{{schema}}"."Permissions" p ON p."PermissionId" = rp."PermissionId"
                             INNER JOIN "permRaw" rqr ON p."PermissionName" = rqr.value
                             UNION
                             SELECT p."PermissionId", q."ChildTenantId"
                             FROM "rtQuery" q
                             INNER JOIN "{{schema}}"."GlobalToLocalRoles" gl ON gl."LocalRoleId" = q."ResultingChildRoleId"
                             INNER JOIN "{{schema}}"."GlobalRolePermissions" grp ON grp."GlobalRoleId" = gl."GlobalRoleId"
                             INNER JOIN "{{schema}}"."Permissions" p ON p."PermissionId" = grp."PermissionId"
                             INNER JOIN "permRaw" rqr ON p."PermissionName" = rqr.value
                         )
                         SELECT t."TenantId", t."ParentTenantId", t."TenantName", t."DisplayName",
                                NULL::text, t."TimeZone", t."TenantTypeId", t."TenantDirty"
                         FROM "perm2T" x
                         INNER JOIN "{{schema}}"."Tenants" t ON t."TenantId" = x."ChildTenantId"
                         GROUP BY t."TenantId", t."ParentTenantId", t."TenantName", t."DisplayName",
                                  t."TimeZone", t."TenantTypeId", t."TenantDirty"
                     $fn$
                     """;
        }
    }
}
