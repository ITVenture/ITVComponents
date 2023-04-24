using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.PostgreSql.Migrations
{
    public partial class Sequences : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sequences",
                columns: table => new
                {
                    SequenceId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    SequenceName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    MinValue = table.Column<int>(type: "integer", nullable: false),
                    MaxValue = table.Column<int>(type: "integer", nullable: false),
                    Cycle = table.Column<bool>(type: "boolean", nullable: false),
                    StepSize = table.Column<int>(type: "integer", nullable: false),
                    CurrentValue = table.Column<int>(type: "integer", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_Sequences_TenantId",
                table: "Sequences",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UQ_SequenceName",
                table: "Sequences",
                columns: new[] { "SequenceName", "TenantId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sequences");
        }
    }
}
