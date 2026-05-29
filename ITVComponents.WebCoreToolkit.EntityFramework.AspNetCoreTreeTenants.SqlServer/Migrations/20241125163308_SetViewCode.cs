using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.SqlServer.SyntaxHelper;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class SetViewCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            SqlColumnsSyntaxHelper.ConfigureViews(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
