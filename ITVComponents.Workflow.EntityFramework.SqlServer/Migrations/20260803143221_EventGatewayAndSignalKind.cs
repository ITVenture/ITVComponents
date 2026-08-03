using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.Workflow.EntityFramework.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class EventGatewayAndSignalKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RaceTokenId",
                table: "Tokens",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WaitingCorrelation",
                table: "Tokens",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WaitingKind",
                table: "Tokens",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_WaitingCorrelation",
                table: "Tokens",
                column: "WaitingCorrelation");

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_WaitingSignal_WaitingKind",
                table: "Tokens",
                columns: new[] { "WaitingSignal", "WaitingKind" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tokens_WaitingCorrelation",
                table: "Tokens");

            migrationBuilder.DropIndex(
                name: "IX_Tokens_WaitingSignal_WaitingKind",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "RaceTokenId",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "WaitingCorrelation",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "WaitingKind",
                table: "Tokens");
        }
    }
}
