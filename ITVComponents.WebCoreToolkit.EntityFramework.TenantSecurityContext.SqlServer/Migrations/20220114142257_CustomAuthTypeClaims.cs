using Microsoft.EntityFrameworkCore.Migrations;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Migrations
{
    public partial class CustomAuthTypeClaims : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationClaimMappings_AuthenticationTypeId",
                table: "AuthenticationClaimMappings",
                column: "AuthenticationTypeId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthenticationClaimMappings");
        }
    }
}
