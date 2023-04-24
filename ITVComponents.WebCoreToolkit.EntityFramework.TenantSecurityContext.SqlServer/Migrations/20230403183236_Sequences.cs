using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Migrations
{
    public partial class Sequences : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
