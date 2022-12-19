using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Migrations
{
    public partial class SharedAssetManagement : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                        principalColumn: "FeatureId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetTemplates_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "PermissionId",
                        onDelete: ReferentialAction.Restrict);
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

            migrationBuilder.CreateIndex(
                name: "UQ_TrustedComponentType",
                table: "TrustedFullAccessComponents",
                column: "FullQualifiedTypeName",
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
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetTemplateFeatures");

            migrationBuilder.DropTable(
                name: "AssetTemplateGrants");

            migrationBuilder.DropTable(
                name: "AssetTemplatePathFilters");

            migrationBuilder.DropTable(
                name: "SharedAssetTenantFilters");

            migrationBuilder.DropTable(
                name: "SharedAssetUserFilters");

            migrationBuilder.DropTable(
                name: "SharedAssets");

            migrationBuilder.DropTable(
                name: "AssetTemplates");

            migrationBuilder.DropIndex(
                name: "UQ_TrustedComponentType",
                table: "TrustedFullAccessComponents");
        }
    }
}
