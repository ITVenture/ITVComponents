using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.Workflow.EntityFramework.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class MessageOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Outbox",
                columns: table => new
                {
                    InstanceId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SignalName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrelationKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Broadcast = table.Column<bool>(type: "bit", nullable: false),
                    TargetInstanceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WaitingTokenId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReachedVariable = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    ClaimedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimedUntil = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outbox", x => new { x.InstanceId, x.Id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_ClaimedUntil",
                table: "Outbox",
                column: "ClaimedUntil");

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_CreatedUtc",
                table: "Outbox",
                column: "CreatedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Outbox");
        }
    }
}
