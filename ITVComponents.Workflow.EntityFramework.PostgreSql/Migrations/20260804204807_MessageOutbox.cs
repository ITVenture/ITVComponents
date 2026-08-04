using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.Workflow.EntityFramework.PostgreSql.Migrations
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
                    InstanceId = table.Column<string>(type: "text", nullable: false),
                    Id = table.Column<string>(type: "text", nullable: false),
                    SignalName = table.Column<string>(type: "text", nullable: true),
                    CorrelationKey = table.Column<string>(type: "text", nullable: true),
                    Broadcast = table.Column<bool>(type: "boolean", nullable: false),
                    TargetInstanceId = table.Column<string>(type: "text", nullable: true),
                    PayloadJson = table.Column<string>(type: "text", nullable: true),
                    WaitingTokenId = table.Column<string>(type: "text", nullable: true),
                    ReachedVariable = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    ClaimedBy = table.Column<string>(type: "text", nullable: true),
                    ClaimedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
