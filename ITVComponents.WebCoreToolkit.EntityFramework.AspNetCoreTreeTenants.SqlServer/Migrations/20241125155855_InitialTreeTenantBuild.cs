using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialTreeTenantBuild : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppPermissionSets",
                columns: table => new
                {
                    AppPermissionSetId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPermissionSets", x => x.AppPermissionSetId);
                });

            migrationBuilder.CreateTable(
                name: "AuthenticationTypes",
                columns: table => new
                {
                    AuthenticationTypeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuthenticationTypeName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthenticationTypes", x => x.AuthenticationTypeId);
                });

            migrationBuilder.CreateTable(
                name: "ClientApps",
                columns: table => new
                {
                    ClientAppId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClientKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClientSecret = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientApps", x => x.ClientAppId);
                });

            migrationBuilder.CreateTable(
                name: "ClientAppTemplates",
                columns: table => new
                {
                    ClientAppTemplateId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientAppTemplates", x => x.ClientAppTemplateId);
                });

            migrationBuilder.CreateTable(
                name: "Cultures",
                columns: table => new
                {
                    CultureId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cultures", x => x.CultureId);
                });

            migrationBuilder.CreateTable(
                name: "Features",
                columns: table => new
                {
                    FeatureId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FeatureName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    FeatureDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Features", x => x.FeatureId);
                });

            migrationBuilder.CreateTable(
                name: "GlobalSettings",
                columns: table => new
                {
                    GlobalSettingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SettingsKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SettingsValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    JsonSetting = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalSettings", x => x.GlobalSettingId);
                });

            migrationBuilder.CreateTable(
                name: "HealthScripts",
                columns: table => new
                {
                    HealthScriptId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HealthScriptName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Script = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HealthScripts", x => x.HealthScriptId);
                });

            migrationBuilder.CreateTable(
                name: "Localizations",
                columns: table => new
                {
                    LocalizationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Identifier = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Localizations", x => x.LocalizationId);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemLog",
                columns: table => new
                {
                    SystemEventId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LogLevel = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EventTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemLog", x => x.SystemEventId);
                });

            migrationBuilder.CreateTable(
                name: "TenantTemplates",
                columns: table => new
                {
                    TenantTemplateId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Markup = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantTemplates", x => x.TenantTemplateId);
                });

            migrationBuilder.CreateTable(
                name: "TrustedFullAccessComponents",
                columns: table => new
                {
                    TrustedFullAccessComponentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullQualifiedTypeName = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    TargetQualifiedTypeName = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TrustLevelConfig = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrustedFullAccessComponents", x => x.TrustedFullAccessComponentId);
                });

            migrationBuilder.CreateTable(
                name: "Tutorials",
                columns: table => new
                {
                    VideoTutorialId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SortableName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModuleUrl = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tutorials", x => x.VideoTutorialId);
                });

            migrationBuilder.CreateTable(
                name: "AuthenticationClaimMappings",
                columns: table => new
                {
                    AuthenticationClaimMappingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuthenticationTypeId = table.Column<int>(type: "int", nullable: false),
                    IncomingClaimName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Condition = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OutgoingClaimName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    OutgoingValueType = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    OutgoingIssuer = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    OutgoingOriginalIssuer = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    OutgoingClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthenticationClaimMappings", x => x.AuthenticationClaimMappingId);
                    table.ForeignKey(
                        name: "FK_AuthenticationClaimMappings_AuthenticationTypes_AuthenticationTypeId",
                        column: x => x.AuthenticationTypeId,
                        principalTable: "AuthenticationTypes",
                        principalColumn: "AuthenticationTypeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AuthenticationTypeId = table.Column<int>(type: "int", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_AuthenticationTypes_AuthenticationTypeId",
                        column: x => x.AuthenticationTypeId,
                        principalTable: "AuthenticationTypes",
                        principalColumn: "AuthenticationTypeId");
                });

            migrationBuilder.CreateTable(
                name: "ClientAppPermissions",
                columns: table => new
                {
                    ClientAppPermissionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientAppId = table.Column<int>(type: "int", nullable: false),
                    AppPermissionSetId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientAppPermissions", x => x.ClientAppPermissionId);
                    table.ForeignKey(
                        name: "FK_ClientAppPermissions_AppPermissionSets_AppPermissionSetId",
                        column: x => x.AppPermissionSetId,
                        principalTable: "AppPermissionSets",
                        principalColumn: "AppPermissionSetId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientAppPermissions_ClientApps_ClientAppId",
                        column: x => x.ClientAppId,
                        principalTable: "ClientApps",
                        principalColumn: "ClientAppId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientAppTemplatePermissions",
                columns: table => new
                {
                    ClientAppTemplatePermissionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientAppTemplateId = table.Column<int>(type: "int", nullable: false),
                    AppPermissionSetId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientAppTemplatePermissions", x => x.ClientAppTemplatePermissionId);
                    table.ForeignKey(
                        name: "FK_ClientAppTemplatePermissions_AppPermissionSets_AppPermissionSetId",
                        column: x => x.AppPermissionSetId,
                        principalTable: "AppPermissionSets",
                        principalColumn: "AppPermissionSetId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientAppTemplatePermissions_ClientAppTemplates_ClientAppTemplateId",
                        column: x => x.ClientAppTemplateId,
                        principalTable: "ClientAppTemplates",
                        principalColumn: "ClientAppTemplateId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TemplateModules",
                columns: table => new
                {
                    TemplateModuleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TemplateModuleName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FeatureId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateModules", x => x.TemplateModuleId);
                    table.ForeignKey(
                        name: "FK_TemplateModules_Features_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "Features",
                        principalColumn: "FeatureId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LocalizationCultures",
                columns: table => new
                {
                    LocalizationCultureId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CultureId = table.Column<int>(type: "int", nullable: false),
                    LocalizationId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalizationCultures", x => x.LocalizationCultureId);
                    table.ForeignKey(
                        name: "FK_LocalizationCultures_Cultures_CultureId",
                        column: x => x.CultureId,
                        principalTable: "Cultures",
                        principalColumn: "CultureId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LocalizationCultures_Localizations_LocalizationId",
                        column: x => x.LocalizationId,
                        principalTable: "Localizations",
                        principalColumn: "LocalizationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleClaims_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantTypes",
                columns: table => new
                {
                    TenantTypeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantTypeName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    TypeMetaData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantTemplateId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantTypes", x => x.TenantTypeId);
                    table.ForeignKey(
                        name: "FK_TenantTypes_TenantTemplates_TenantTemplateId",
                        column: x => x.TenantTemplateId,
                        principalTable: "TenantTemplates",
                        principalColumn: "TenantTemplateId");
                });

            migrationBuilder.CreateTable(
                name: "TutorialStreams",
                columns: table => new
                {
                    TutorialStreamId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LanguageTag = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VideoTutorialId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorialStreams", x => x.TutorialStreamId);
                    table.ForeignKey(
                        name: "FK_TutorialStreams_Tutorials_VideoTutorialId",
                        column: x => x.VideoTutorialId,
                        principalTable: "Tutorials",
                        principalColumn: "VideoTutorialId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserClaims_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_UserLogins_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserProperties",
                columns: table => new
                {
                    CustomUserPropertyId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PropertyType = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProperties", x => x.CustomUserPropertyId);
                    table.ForeignKey(
                        name: "FK_UserProperties_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_UserTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TemplateModuleConfigurators",
                columns: table => new
                {
                    TemplateModuleConfiguratorId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CustomConfiguratorView = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ConfiguratorTypeBack = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    TemplateModuleId = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateModuleConfigurators", x => x.TemplateModuleConfiguratorId);
                    table.ForeignKey(
                        name: "FK_TemplateModuleConfigurators_TemplateModules_TemplateModuleId",
                        column: x => x.TemplateModuleId,
                        principalTable: "TemplateModules",
                        principalColumn: "TemplateModuleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TemplateModuleScripts",
                columns: table => new
                {
                    TemplateModuleScriptId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScriptFile = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    TemplateModuleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateModuleScripts", x => x.TemplateModuleScriptId);
                    table.ForeignKey(
                        name: "FK_TemplateModuleScripts_TemplateModules_TemplateModuleId",
                        column: x => x.TemplateModuleId,
                        principalTable: "TemplateModules",
                        principalColumn: "TemplateModuleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LocalizationCultureStrings",
                columns: table => new
                {
                    LocalizationStringId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LocalizationCultureId = table.Column<int>(type: "int", nullable: false),
                    LocalizationKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LocalizationValue = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalizationCultureStrings", x => x.LocalizationStringId);
                    table.ForeignKey(
                        name: "FK_LocalizationCultureStrings_LocalizationCultures_LocalizationCultureId",
                        column: x => x.LocalizationCultureId,
                        principalTable: "LocalizationCultures",
                        principalColumn: "LocalizationCultureId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    TenantId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentTenantId = table.Column<int>(type: "int", nullable: true),
                    TenantName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    TenantPassword = table.Column<string>(type: "nvarchar(125)", maxLength: 125, nullable: true),
                    TimeZone = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    TenantTypeId = table.Column<int>(type: "int", nullable: true),
                    TenantDirty = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.TenantId);
                    table.ForeignKey(
                        name: "FK_Tenants_TenantTypes_TenantTypeId",
                        column: x => x.TenantTypeId,
                        principalTable: "TenantTypes",
                        principalColumn: "TenantTypeId");
                    table.ForeignKey(
                        name: "FK_Tenants_Tenants_ParentTenantId",
                        column: x => x.ParentTenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantId");
                });

            migrationBuilder.CreateTable(
                name: "TutorialStreamBlob",
                columns: table => new
                {
                    TutorialStreamBlobId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    TutorialStreamId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorialStreamBlob", x => x.TutorialStreamBlobId);
                    table.ForeignKey(
                        name: "FK_TutorialStreamBlob_TutorialStreams_TutorialStreamId",
                        column: x => x.TutorialStreamId,
                        principalTable: "TutorialStreams",
                        principalColumn: "TutorialStreamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TemplateModuleConfiguratorParameters",
                columns: table => new
                {
                    TemplateModuleCfgParameterId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParameterName = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParameterValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TemplateModuleConfiguratorId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateModuleConfiguratorParameters", x => x.TemplateModuleCfgParameterId);
                    table.ForeignKey(
                        name: "FK_TemplateModuleConfiguratorParameters_TemplateModuleConfigurators_TemplateModuleConfiguratorId",
                        column: x => x.TemplateModuleConfiguratorId,
                        principalTable: "TemplateModuleConfigurators",
                        principalColumn: "TemplateModuleConfiguratorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    PermissionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PermissionName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: true),
                    PermissionNameUniqueness = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false, computedColumnSql: "case when TenantId is null then PermissionName else '__T'+convert(varchar(10),TenantId)+'##'+PermissionName end persisted")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.PermissionId);
                    table.ForeignKey(
                        name: "FK_Permissions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantId");
                });

            migrationBuilder.CreateTable(
                name: "SecurityRoles",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    IsSystemRole = table.Column<bool>(type: "bit", nullable: false),
                    RoleMetaData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RoleNameUniqueness = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false, computedColumnSql: "'__T'+convert(varchar(10),TenantId)+'##'+RoleName persisted")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityRoles", x => x.RoleId);
                    table.ForeignKey(
                        name: "FK_SecurityRoles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sequences",
                columns: table => new
                {
                    SequenceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    SequenceName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    MinValue = table.Column<int>(type: "int", nullable: false),
                    MaxValue = table.Column<int>(type: "int", nullable: false),
                    Cycle = table.Column<bool>(type: "bit", nullable: false),
                    StepSize = table.Column<int>(type: "int", nullable: false),
                    CurrentValue = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sequences", x => x.SequenceId);
                    table.ForeignKey(
                        name: "FK_Sequences_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantFeatureActivations",
                columns: table => new
                {
                    TenantFeatureActivationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FeatureId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    ActivationStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActivationEnd = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantFeatureActivations", x => x.TenantFeatureActivationId);
                    table.ForeignKey(
                        name: "FK_TenantFeatureActivations_Features_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "Features",
                        principalColumn: "FeatureId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenantFeatureActivations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantSettings",
                columns: table => new
                {
                    TenantSettingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    SettingsKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SettingsValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    JsonSetting = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSettings", x => x.TenantSettingId);
                    table.ForeignKey(
                        name: "FK_TenantSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantUsers",
                columns: table => new
                {
                    TenantUserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: true, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantUsers", x => x.TenantUserId);
                    table.ForeignKey(
                        name: "FK_TenantUsers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenantUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WebPluginConstants",
                columns: table => new
                {
                    WebPluginConstantId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameUniqueness = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false, computedColumnSql: "case when TenantId is null then Name else '__T'+convert(varchar(10),TenantId)+'##'+Name end persisted"),
                    TenantId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebPluginConstants", x => x.WebPluginConstantId);
                    table.ForeignKey(
                        name: "FK_WebPluginConstants_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantId");
                });

            migrationBuilder.CreateTable(
                name: "WebPlugins",
                columns: table => new
                {
                    WebPluginId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UniqueName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Constructor = table.Column<string>(type: "nvarchar(max)", maxLength: 8192, nullable: true),
                    AutoLoad = table.Column<bool>(type: "bit", nullable: false),
                    StartupRegistrationConstructor = table.Column<string>(type: "nvarchar(max)", maxLength: 8192, nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: true),
                    PluginNameUniqueness = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false, computedColumnSql: "case when TenantId is null then UniqueName else '__T'+convert(varchar(10),TenantId)+'##'+UniqueName end persisted")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebPlugins", x => x.WebPluginId);
                    table.ForeignKey(
                        name: "FK_WebPlugins_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantId");
                });

            migrationBuilder.CreateTable(
                name: "AppPermissions",
                columns: table => new
                {
                    AppPermissionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppPermissionSetId = table.Column<int>(type: "int", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPermissions", x => x.AppPermissionId);
                    table.ForeignKey(
                        name: "FK_AppPermissions_AppPermissionSets_AppPermissionSetId",
                        column: x => x.AppPermissionSetId,
                        principalTable: "AppPermissionSets",
                        principalColumn: "AppPermissionSetId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppPermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "PermissionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssetTemplates",
                columns: table => new
                {
                    AssetTemplateId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FeatureId = table.Column<int>(type: "int", nullable: true),
                    PermissionId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SystemKey = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetTemplates", x => x.AssetTemplateId);
                    table.ForeignKey(
                        name: "FK_AssetTemplates_Features_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "Features",
                        principalColumn: "FeatureId");
                    table.ForeignKey(
                        name: "FK_AssetTemplates_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "PermissionId");
                });

            migrationBuilder.CreateTable(
                name: "DiagnosticsQueries",
                columns: table => new
                {
                    DiagnosticsQueryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiagnosticsQueryName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DbContext = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AutoReturn = table.Column<bool>(type: "bit", nullable: false),
                    QueryText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PermissionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagnosticsQueries", x => x.DiagnosticsQueryId);
                    table.ForeignKey(
                        name: "FK_DiagnosticsQueries_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "PermissionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Navigation",
                columns: table => new
                {
                    NavigationMenuId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    UrlUniqueness = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false, computedColumnSql: "case when isnull(Url,'')='' and isnull(RefTag,'')='' then 'MENU__'+convert(varchar(10),NavigationMenuId) when isnull(Url,'')='' then RefTag else Url end persisted"),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: true),
                    PermissionId = table.Column<int>(type: "int", nullable: true),
                    FeatureId = table.Column<int>(type: "int", nullable: true),
                    SpanClass = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RefTag = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Navigation", x => x.NavigationMenuId);
                    table.ForeignKey(
                        name: "FK_Navigation_Features_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "Features",
                        principalColumn: "FeatureId");
                    table.ForeignKey(
                        name: "FK_Navigation_Navigation_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Navigation",
                        principalColumn: "NavigationMenuId");
                    table.ForeignKey(
                        name: "FK_Navigation_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "PermissionId");
                });

            migrationBuilder.CreateTable(
                name: "RoleRoles",
                columns: table => new
                {
                    RoleRoleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PermissiveRoleId = table.Column<int>(type: "int", nullable: true),
                    PermittedRoleId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleRoles", x => x.RoleRoleId);
                    table.ForeignKey(
                        name: "FK_RoleRoles_SecurityRoles_PermissiveRoleId",
                        column: x => x.PermissiveRoleId,
                        principalTable: "SecurityRoles",
                        principalColumn: "RoleId");
                    table.ForeignKey(
                        name: "FK_RoleRoles_SecurityRoles_PermittedRoleId",
                        column: x => x.PermittedRoleId,
                        principalTable: "SecurityRoles",
                        principalColumn: "RoleId");
                });

            migrationBuilder.CreateTable(
                name: "ClientAppUsers",
                columns: table => new
                {
                    ClientAppUserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Label = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TenantUserId = table.Column<int>(type: "int", nullable: false),
                    ClientAppId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientAppUsers", x => x.ClientAppUserId);
                    table.ForeignKey(
                        name: "FK_ClientAppUsers_ClientApps_ClientAppId",
                        column: x => x.ClientAppId,
                        principalTable: "ClientApps",
                        principalColumn: "ClientAppId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientAppUsers_TenantUsers_TenantUserId",
                        column: x => x.TenantUserId,
                        principalTable: "TenantUsers",
                        principalColumn: "TenantUserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantUserRoles",
                columns: table => new
                {
                    UserRoleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantUserId = table.Column<int>(type: "int", nullable: true),
                    RoleId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantUserRoles", x => x.UserRoleId);
                    table.ForeignKey(
                        name: "FK_TenantUserRoles_SecurityRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "SecurityRoles",
                        principalColumn: "RoleId");
                    table.ForeignKey(
                        name: "FK_TenantUserRoles_TenantUsers_TenantUserId",
                        column: x => x.TenantUserId,
                        principalTable: "TenantUsers",
                        principalColumn: "TenantUserId");
                });

            migrationBuilder.CreateTable(
                name: "GenericPluginParams",
                columns: table => new
                {
                    WebPluginGenericParameterId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GenericTypeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TypeExpression = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    WebPluginId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GenericPluginParams", x => x.WebPluginGenericParameterId);
                    table.ForeignKey(
                        name: "FK_GenericPluginParams_WebPlugins_WebPluginId",
                        column: x => x.WebPluginId,
                        principalTable: "WebPlugins",
                        principalColumn: "WebPluginId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssetTemplateFeatures",
                columns: table => new
                {
                    AssetTemplateFeatureId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssetTemplateId = table.Column<int>(type: "int", nullable: false),
                    FeatureId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetTemplateFeatures", x => x.AssetTemplateFeatureId);
                    table.ForeignKey(
                        name: "FK_AssetTemplateFeatures_AssetTemplates_AssetTemplateId",
                        column: x => x.AssetTemplateId,
                        principalTable: "AssetTemplates",
                        principalColumn: "AssetTemplateId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssetTemplateFeatures_Features_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "Features",
                        principalColumn: "FeatureId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssetTemplateGrants",
                columns: table => new
                {
                    AssetTemplateGrantId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssetTemplateId = table.Column<int>(type: "int", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetTemplateGrants", x => x.AssetTemplateGrantId);
                    table.ForeignKey(
                        name: "FK_AssetTemplateGrants_AssetTemplates_AssetTemplateId",
                        column: x => x.AssetTemplateId,
                        principalTable: "AssetTemplates",
                        principalColumn: "AssetTemplateId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssetTemplateGrants_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "PermissionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssetTemplatePathFilters",
                columns: table => new
                {
                    AssetTemplatePathId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssetTemplateId = table.Column<int>(type: "int", nullable: false),
                    PathTemplate = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetTemplatePathFilters", x => x.AssetTemplatePathId);
                    table.ForeignKey(
                        name: "FK_AssetTemplatePathFilters_AssetTemplates_AssetTemplateId",
                        column: x => x.AssetTemplateId,
                        principalTable: "AssetTemplates",
                        principalColumn: "AssetTemplateId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SharedAssets",
                columns: table => new
                {
                    SharedAssetId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssetTemplateId = table.Column<int>(type: "int", nullable: false),
                    AssetKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AnonymousAccessTokenRaw = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AssetTitle = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RootPath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    NotBefore = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NotAfter = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedAssets", x => x.SharedAssetId);
                    table.ForeignKey(
                        name: "FK_SharedAssets_AssetTemplates_AssetTemplateId",
                        column: x => x.AssetTemplateId,
                        principalTable: "AssetTemplates",
                        principalColumn: "AssetTemplateId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SharedAssets_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiagnosticsQueryParameters",
                columns: table => new
                {
                    DiagnosticsQueryParameterId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiagnosticsQueryId = table.Column<int>(type: "int", nullable: false),
                    ParameterName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ParameterType = table.Column<int>(type: "int", nullable: false),
                    Format = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Optional = table.Column<bool>(type: "bit", nullable: false),
                    DefaultValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagnosticsQueryParameters", x => x.DiagnosticsQueryParameterId);
                    table.ForeignKey(
                        name: "FK_DiagnosticsQueryParameters_DiagnosticsQueries_DiagnosticsQueryId",
                        column: x => x.DiagnosticsQueryId,
                        principalTable: "DiagnosticsQueries",
                        principalColumn: "DiagnosticsQueryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantDiagnosticsQueries",
                columns: table => new
                {
                    TenantDiagnosticsQueryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    DiagnosticsQueryId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantDiagnosticsQueries", x => x.TenantDiagnosticsQueryId);
                    table.ForeignKey(
                        name: "FK_TenantDiagnosticsQueries_DiagnosticsQueries_DiagnosticsQueryId",
                        column: x => x.DiagnosticsQueryId,
                        principalTable: "DiagnosticsQueries",
                        principalColumn: "DiagnosticsQueryId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenantDiagnosticsQueries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Widgets",
                columns: table => new
                {
                    DashboardWidgetId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisplayName = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    TitleTemplate = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    SystemName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DiagnosticsQueryId = table.Column<int>(type: "int", nullable: false),
                    Area = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomQueryString = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Template = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Widgets", x => x.DashboardWidgetId);
                    table.ForeignKey(
                        name: "FK_Widgets_DiagnosticsQueries_DiagnosticsQueryId",
                        column: x => x.DiagnosticsQueryId,
                        principalTable: "DiagnosticsQueries",
                        principalColumn: "DiagnosticsQueryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantNavigation",
                columns: table => new
                {
                    TenantNavigationMenuId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    NavigationMenuId = table.Column<int>(type: "int", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantNavigation", x => x.TenantNavigationMenuId);
                    table.ForeignKey(
                        name: "FK_TenantNavigation_Navigation_NavigationMenuId",
                        column: x => x.NavigationMenuId,
                        principalTable: "Navigation",
                        principalColumn: "NavigationMenuId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenantNavigation_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "PermissionId");
                    table.ForeignKey(
                        name: "FK_TenantNavigation_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RolePermissionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<int>(type: "int", nullable: true),
                    PermissionId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    OriginId = table.Column<int>(type: "int", nullable: true),
                    RoleRoleId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.RolePermissionId);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "PermissionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_RolePermissions_OriginId",
                        column: x => x.OriginId,
                        principalTable: "RolePermissions",
                        principalColumn: "RolePermissionId");
                    table.ForeignKey(
                        name: "FK_RolePermissions_RoleRoles_RoleRoleId",
                        column: x => x.RoleRoleId,
                        principalTable: "RoleRoles",
                        principalColumn: "RoleRoleId");
                    table.ForeignKey(
                        name: "FK_RolePermissions_SecurityRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "SecurityRoles",
                        principalColumn: "RoleId");
                    table.ForeignKey(
                        name: "FK_RolePermissions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SharedAssetTenantFilters",
                columns: table => new
                {
                    SharedAssetTenantFilterId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SharedAssetId = table.Column<int>(type: "int", nullable: false),
                    LabelFilter = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedAssetTenantFilters", x => x.SharedAssetTenantFilterId);
                    table.ForeignKey(
                        name: "FK_SharedAssetTenantFilters_SharedAssets_SharedAssetId",
                        column: x => x.SharedAssetId,
                        principalTable: "SharedAssets",
                        principalColumn: "SharedAssetId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SharedAssetUserFilters",
                columns: table => new
                {
                    SharedAssetUserFilterId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SharedAssetId = table.Column<int>(type: "int", nullable: false),
                    LabelFilter = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedAssetUserFilters", x => x.SharedAssetUserFilterId);
                    table.ForeignKey(
                        name: "FK_SharedAssetUserFilters_SharedAssets_SharedAssetId",
                        column: x => x.SharedAssetId,
                        principalTable: "SharedAssets",
                        principalColumn: "SharedAssetId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserWidgets",
                columns: table => new
                {
                    UserWidgetId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DashboardWidgetId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CustomQueryString = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWidgets", x => x.UserWidgetId);
                    table.ForeignKey(
                        name: "FK_UserWidgets_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserWidgets_Widgets_DashboardWidgetId",
                        column: x => x.DashboardWidgetId,
                        principalTable: "Widgets",
                        principalColumn: "DashboardWidgetId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WidgetLocales",
                columns: table => new
                {
                    DashboardWidgetLocalizationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DashboardWidgetId = table.Column<int>(type: "int", nullable: false),
                    LocaleName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    TitleTemplate = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Template = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WidgetLocales", x => x.DashboardWidgetLocalizationId);
                    table.ForeignKey(
                        name: "FK_WidgetLocales_Widgets_DashboardWidgetId",
                        column: x => x.DashboardWidgetId,
                        principalTable: "Widgets",
                        principalColumn: "DashboardWidgetId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WidgetParams",
                columns: table => new
                {
                    DashboardParamId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DashboardWidgetId = table.Column<int>(type: "int", nullable: false),
                    ParameterName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    InputType = table.Column<int>(type: "int", nullable: false),
                    InputConfig = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WidgetParams", x => x.DashboardParamId);
                    table.ForeignKey(
                        name: "FK_WidgetParams_Widgets_DashboardWidgetId",
                        column: x => x.DashboardWidgetId,
                        principalTable: "Widgets",
                        principalColumn: "DashboardWidgetId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppPermissions_AppPermissionSetId",
                table: "AppPermissions",
                column: "AppPermissionSetId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPermissions_PermissionId",
                table: "AppPermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "UQ_AppPermissionSetName",
                table: "AppPermissionSets",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetTemplateFeatures_AssetTemplateId",
                table: "AssetTemplateFeatures",
                column: "AssetTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTemplateFeatures_FeatureId",
                table: "AssetTemplateFeatures",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTemplateGrants_AssetTemplateId",
                table: "AssetTemplateGrants",
                column: "AssetTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTemplateGrants_PermissionId",
                table: "AssetTemplateGrants",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTemplatePathFilters_AssetTemplateId",
                table: "AssetTemplatePathFilters",
                column: "AssetTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTemplates_FeatureId",
                table: "AssetTemplates",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTemplates_PermissionId",
                table: "AssetTemplates",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "UQ_AssetTemplateSysKey",
                table: "AssetTemplates",
                column: "SystemKey",
                unique: true,
                filter: "[SystemKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationClaimMappings_AuthenticationTypeId",
                table: "AuthenticationClaimMappings",
                column: "AuthenticationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_UniqueAuthenticationType",
                table: "AuthenticationTypes",
                column: "AuthenticationTypeName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientAppPermissions_AppPermissionSetId",
                table: "ClientAppPermissions",
                column: "AppPermissionSetId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientAppPermissions_ClientAppId",
                table: "ClientAppPermissions",
                column: "ClientAppId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientAppTemplatePermissions_AppPermissionSetId",
                table: "ClientAppTemplatePermissions",
                column: "AppPermissionSetId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientAppTemplatePermissions_ClientAppTemplateId",
                table: "ClientAppTemplatePermissions",
                column: "ClientAppTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_UniqueTemplateName",
                table: "ClientAppTemplates",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientAppUsers_ClientAppId",
                table: "ClientAppUsers",
                column: "ClientAppId");

            migrationBuilder.CreateIndex(
                name: "UQ_ClientAppUser",
                table: "ClientAppUsers",
                column: "Label",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_TUserPerApp",
                table: "ClientAppUsers",
                columns: new[] { "TenantUserId", "ClientAppId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cultures_Name",
                table: "Cultures",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticsQueries_PermissionId",
                table: "DiagnosticsQueries",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticsQueryUniqueness",
                table: "DiagnosticsQueries",
                column: "DiagnosticsQueryName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticsQueryParameters_DiagnosticsQueryId",
                table: "DiagnosticsQueryParameters",
                column: "DiagnosticsQueryId");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureUniqueness",
                table: "Features",
                column: "FeatureName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UniqueGenericParamName",
                table: "GenericPluginParams",
                columns: new[] { "WebPluginId", "GenericTypeName" },
                unique: true,
                filter: "[GenericTypeName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_GlobalSettingsKey",
                table: "GlobalSettings",
                column: "SettingsKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_NamedHealthScript",
                table: "HealthScripts",
                column: "HealthScriptName",
                unique: true,
                filter: "[HealthScriptName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LocalizationCultures_CultureId_LocalizationId",
                table: "LocalizationCultures",
                columns: new[] { "CultureId", "LocalizationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocalizationCultures_LocalizationId",
                table: "LocalizationCultures",
                column: "LocalizationId");

            migrationBuilder.CreateIndex(
                name: "IX_LocalizationCultureStrings_LocalizationCultureId_LocalizationKey",
                table: "LocalizationCultureStrings",
                columns: new[] { "LocalizationCultureId", "LocalizationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Localizations_Identifier",
                table: "Localizations",
                column: "Identifier",
                unique: true,
                filter: "[Identifier] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Navigation_FeatureId",
                table: "Navigation",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_Navigation_ParentId",
                table: "Navigation",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Navigation_PermissionId",
                table: "Navigation",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_UniqueUrl",
                table: "Navigation",
                column: "UrlUniqueness",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_TenantId",
                table: "Permissions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UniquePermissionName",
                table: "Permissions",
                column: "PermissionNameUniqueness",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaims_RoleId",
                table: "RoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_OriginId",
                table: "RolePermissions",
                column: "OriginId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleRoleId",
                table: "RolePermissions",
                column: "RoleRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_TenantId",
                table: "RolePermissions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UniqueRolePermission",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId", "TenantId", "OriginId" },
                unique: true,
                filter: "[RoleId] IS NOT NULL AND [OriginId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RoleRoles_PermissiveRoleId",
                table: "RoleRoles",
                column: "PermissiveRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleRoles_PermittedRoleId",
                table: "RoleRoles",
                column: "PermittedRoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "Roles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityRoles_TenantId",
                table: "SecurityRoles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UniqueRoleName",
                table: "SecurityRoles",
                column: "RoleNameUniqueness",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sequences_TenantId",
                table: "Sequences",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UQ_SequenceName",
                table: "Sequences",
                columns: new[] { "SequenceName", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SharedAssets_AssetTemplateId",
                table: "SharedAssets",
                column: "AssetTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedAssets_TenantId",
                table: "SharedAssets",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedAssetTenantFilters_SharedAssetId",
                table: "SharedAssetTenantFilters",
                column: "SharedAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedAssetUserFilters_SharedAssetId",
                table: "SharedAssetUserFilters",
                column: "SharedAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateModuleConfiguratorParameters_TemplateModuleConfiguratorId",
                table: "TemplateModuleConfiguratorParameters",
                column: "TemplateModuleConfiguratorId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateModuleConfigurators_TemplateModuleId",
                table: "TemplateModuleConfigurators",
                column: "TemplateModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateModules_FeatureId",
                table: "TemplateModules",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateModuleScripts_TemplateModuleId",
                table: "TemplateModuleScripts",
                column: "TemplateModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantDiagnosticsQueries_DiagnosticsQueryId",
                table: "TenantDiagnosticsQueries",
                column: "DiagnosticsQueryId");

            migrationBuilder.CreateIndex(
                name: "IX_UniqueDiagnosticsTenantLink",
                table: "TenantDiagnosticsQueries",
                columns: new[] { "TenantId", "DiagnosticsQueryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantFeatureActivations_FeatureId",
                table: "TenantFeatureActivations",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantFeatureActivations_TenantId",
                table: "TenantFeatureActivations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantNavigation_NavigationMenuId",
                table: "TenantNavigation",
                column: "NavigationMenuId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantNavigation_PermissionId",
                table: "TenantNavigation",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_UniqueTenantMenu",
                table: "TenantNavigation",
                columns: new[] { "TenantId", "NavigationMenuId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_ParentTenantId",
                table: "Tenants",
                column: "ParentTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_TenantTypeId",
                table: "Tenants",
                column: "TenantTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_UniqueTenant",
                table: "Tenants",
                column: "TenantName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantSettings_TenantId",
                table: "TenantSettings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UQ_SettingsKey",
                table: "TenantSettings",
                columns: new[] { "SettingsKey", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantTypes_TenantTemplateId",
                table: "TenantTypes",
                column: "TenantTemplateId");

            migrationBuilder.CreateIndex(
                name: "UQ_TenantTypeName",
                table: "TenantTypes",
                column: "TenantTypeName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantUserRoles_TenantUserId",
                table: "TenantUserRoles",
                column: "TenantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UniqueUserRole",
                table: "TenantUserRoles",
                columns: new[] { "RoleId", "TenantUserId" },
                unique: true,
                filter: "[RoleId] IS NOT NULL AND [TenantUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsers_TenantId",
                table: "TenantUsers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsers_UserId",
                table: "TenantUsers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UQ_TrustedComponentType",
                table: "TrustedFullAccessComponents",
                columns: new[] { "FullQualifiedTypeName", "TargetQualifiedTypeName" },
                unique: true,
                filter: "[TargetQualifiedTypeName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TutorialStreamBlob_TutorialStreamId",
                table: "TutorialStreamBlob",
                column: "TutorialStreamId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TutorialStreams_VideoTutorialId",
                table: "TutorialStreams",
                column: "VideoTutorialId");

            migrationBuilder.CreateIndex(
                name: "IX_UserClaims_UserId",
                table: "UserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_UserId",
                table: "UserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UniqueUserProp",
                table: "UserProperties",
                columns: new[] { "UserId", "PropertyType", "PropertyName" },
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "Users",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Users_AuthenticationTypeId",
                table: "Users",
                column: "AuthenticationTypeId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "Users",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserWidgets_DashboardWidgetId",
                table: "UserWidgets",
                column: "DashboardWidgetId");

            migrationBuilder.CreateIndex(
                name: "IX_UserWidgets_TenantId",
                table: "UserWidgets",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UniquePluginConst",
                table: "WebPluginConstants",
                column: "NameUniqueness",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebPluginConstants_TenantId",
                table: "WebPluginConstants",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UniquePluginName",
                table: "WebPlugins",
                column: "PluginNameUniqueness",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebPlugins_TenantId",
                table: "WebPlugins",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UniqueDashboardLocaleDef",
                table: "WidgetLocales",
                columns: new[] { "DashboardWidgetId", "LocaleName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WidgetParams_DashboardWidgetId",
                table: "WidgetParams",
                column: "DashboardWidgetId");

            migrationBuilder.CreateIndex(
                name: "IX_UniqueDashboardDef",
                table: "Widgets",
                column: "SystemName",
                unique: true,
                filter: "[SystemName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Widgets_DiagnosticsQueryId",
                table: "Widgets",
                column: "DiagnosticsQueryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppPermissions");

            migrationBuilder.DropTable(
                name: "AssetTemplateFeatures");

            migrationBuilder.DropTable(
                name: "AssetTemplateGrants");

            migrationBuilder.DropTable(
                name: "AssetTemplatePathFilters");

            migrationBuilder.DropTable(
                name: "AuthenticationClaimMappings");

            migrationBuilder.DropTable(
                name: "ClientAppPermissions");

            migrationBuilder.DropTable(
                name: "ClientAppTemplatePermissions");

            migrationBuilder.DropTable(
                name: "ClientAppUsers");

            migrationBuilder.DropTable(
                name: "DiagnosticsQueryParameters");

            migrationBuilder.DropTable(
                name: "GenericPluginParams");

            migrationBuilder.DropTable(
                name: "GlobalSettings");

            migrationBuilder.DropTable(
                name: "HealthScripts");

            migrationBuilder.DropTable(
                name: "LocalizationCultureStrings");

            migrationBuilder.DropTable(
                name: "RoleClaims");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "Sequences");

            migrationBuilder.DropTable(
                name: "SharedAssetTenantFilters");

            migrationBuilder.DropTable(
                name: "SharedAssetUserFilters");

            migrationBuilder.DropTable(
                name: "SystemLog");

            migrationBuilder.DropTable(
                name: "TemplateModuleConfiguratorParameters");

            migrationBuilder.DropTable(
                name: "TemplateModuleScripts");

            migrationBuilder.DropTable(
                name: "TenantDiagnosticsQueries");

            migrationBuilder.DropTable(
                name: "TenantFeatureActivations");

            migrationBuilder.DropTable(
                name: "TenantNavigation");

            migrationBuilder.DropTable(
                name: "TenantSettings");

            migrationBuilder.DropTable(
                name: "TenantUserRoles");

            migrationBuilder.DropTable(
                name: "TrustedFullAccessComponents");

            migrationBuilder.DropTable(
                name: "TutorialStreamBlob");

            migrationBuilder.DropTable(
                name: "UserClaims");

            migrationBuilder.DropTable(
                name: "UserLogins");

            migrationBuilder.DropTable(
                name: "UserProperties");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "UserTokens");

            migrationBuilder.DropTable(
                name: "UserWidgets");

            migrationBuilder.DropTable(
                name: "WebPluginConstants");

            migrationBuilder.DropTable(
                name: "WidgetLocales");

            migrationBuilder.DropTable(
                name: "WidgetParams");

            migrationBuilder.DropTable(
                name: "AppPermissionSets");

            migrationBuilder.DropTable(
                name: "ClientAppTemplates");

            migrationBuilder.DropTable(
                name: "ClientApps");

            migrationBuilder.DropTable(
                name: "WebPlugins");

            migrationBuilder.DropTable(
                name: "LocalizationCultures");

            migrationBuilder.DropTable(
                name: "RoleRoles");

            migrationBuilder.DropTable(
                name: "SharedAssets");

            migrationBuilder.DropTable(
                name: "TemplateModuleConfigurators");

            migrationBuilder.DropTable(
                name: "Navigation");

            migrationBuilder.DropTable(
                name: "TenantUsers");

            migrationBuilder.DropTable(
                name: "TutorialStreams");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Widgets");

            migrationBuilder.DropTable(
                name: "Cultures");

            migrationBuilder.DropTable(
                name: "Localizations");

            migrationBuilder.DropTable(
                name: "SecurityRoles");

            migrationBuilder.DropTable(
                name: "AssetTemplates");

            migrationBuilder.DropTable(
                name: "TemplateModules");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Tutorials");

            migrationBuilder.DropTable(
                name: "DiagnosticsQueries");

            migrationBuilder.DropTable(
                name: "Features");

            migrationBuilder.DropTable(
                name: "AuthenticationTypes");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropTable(
                name: "TenantTypes");

            migrationBuilder.DropTable(
                name: "TenantTemplates");
        }
    }
}
