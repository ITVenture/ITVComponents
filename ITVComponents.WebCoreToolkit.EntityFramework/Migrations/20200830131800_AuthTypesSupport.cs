using Microsoft.EntityFrameworkCore.Migrations;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Migrations
{
    public partial class AuthTypesSupport : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AuthenticationTypeId",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AuthenticationTypes",
                columns: table => new
                {
                    AuthenticationTypeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuthenticationTypeName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthenticationTypes", x => x.AuthenticationTypeId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_AuthenticationTypeId",
                table: "Users",
                column: "AuthenticationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_UniqueAuthenticationType",
                table: "AuthenticationTypes",
                column: "AuthenticationTypeName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_AuthenticationTypes_AuthenticationTypeId",
                table: "Users",
                column: "AuthenticationTypeId",
                principalTable: "AuthenticationTypes",
                principalColumn: "AuthenticationTypeId",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_AuthenticationTypes_AuthenticationTypeId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "AuthenticationTypes");

            migrationBuilder.DropIndex(
                name: "IX_Users_AuthenticationTypeId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AuthenticationTypeId",
                table: "Users");
        }
    }
}
