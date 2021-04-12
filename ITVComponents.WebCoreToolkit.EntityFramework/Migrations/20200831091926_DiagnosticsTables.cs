using Microsoft.EntityFrameworkCore.Migrations;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Migrations
{
    public partial class DiagnosticsTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Permissions",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

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
                name: "IX_TenantDiagnosticsQueries_DiagnosticsQueryId",
                table: "TenantDiagnosticsQueries",
                column: "DiagnosticsQueryId");

            migrationBuilder.CreateIndex(
                name: "IX_UniqueDiagnosticsTenantLink",
                table: "TenantDiagnosticsQueries",
                columns: new[] { "TenantId", "DiagnosticsQueryId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiagnosticsQueryParameters");

            migrationBuilder.DropTable(
                name: "TenantDiagnosticsQueries");

            migrationBuilder.DropTable(
                name: "DiagnosticsQueries");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Permissions");
        }
    }
}
