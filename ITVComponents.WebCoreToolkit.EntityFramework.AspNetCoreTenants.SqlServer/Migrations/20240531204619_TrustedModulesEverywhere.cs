using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Migrations
{
    public partial class TrustedModulesEverywhere : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_TrustedComponentType",
                table: "TrustedFullAccessComponents");

            migrationBuilder.DropColumn(
                name: "TrustedForAllTenants",
                table: "TrustedFullAccessComponents");

            migrationBuilder.DropColumn(
                name: "TrustedForGlobals",
                table: "TrustedFullAccessComponents");

            migrationBuilder.AddColumn<string>(
                name: "TargetQualifiedTypeName",
                table: "TrustedFullAccessComponents",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrustLevelConfig",
                table: "TrustedFullAccessComponents",
                type: "nvarchar(max)",
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "UQ_TrustedComponentType",
                table: "TrustedFullAccessComponents",
                columns: new[] { "FullQualifiedTypeName", "TargetQualifiedTypeName" },
                unique: true,
                filter: "[TargetQualifiedTypeName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Cultures_Name",
                table: "Cultures",
                column: "Name",
                unique: true);

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
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocalizationCultureStrings");

            migrationBuilder.DropTable(
                name: "LocalizationCultures");

            migrationBuilder.DropTable(
                name: "Cultures");

            migrationBuilder.DropTable(
                name: "Localizations");

            migrationBuilder.DropIndex(
                name: "UQ_TrustedComponentType",
                table: "TrustedFullAccessComponents");

            migrationBuilder.DropColumn(
                name: "TargetQualifiedTypeName",
                table: "TrustedFullAccessComponents");

            migrationBuilder.DropColumn(
                name: "TrustLevelConfig",
                table: "TrustedFullAccessComponents");

            migrationBuilder.AddColumn<bool>(
                name: "TrustedForAllTenants",
                table: "TrustedFullAccessComponents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TrustedForGlobals",
                table: "TrustedFullAccessComponents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "UQ_TrustedComponentType",
                table: "TrustedFullAccessComponents",
                column: "FullQualifiedTypeName",
                unique: true);
        }
    }
}
