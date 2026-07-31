using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.Workflow.EntityFramework.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class BoundaryTimers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BoundaryIteration",
                table: "Tokens",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BoundaryOwnerTokenId",
                table: "Tokens",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BoundaryIteration",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "BoundaryOwnerTokenId",
                table: "Tokens");
        }
    }
}
