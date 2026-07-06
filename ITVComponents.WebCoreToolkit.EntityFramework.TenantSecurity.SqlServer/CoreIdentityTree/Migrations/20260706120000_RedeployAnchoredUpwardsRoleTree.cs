using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.SqlServer.SyntaxHelper;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.SqlServer.Migrations
{
    /// <summary>
    /// Redeploys all tree security views/procedures/functions (AP2): the four <c>GetUpwardsRoleTree*</c>
    /// functions now anchor-parametrize their recursive <c>r</c> CTE (a <c>WHERE</c> on the leaf tenant in the
    /// anchor), so viewpoint-scoped role resolution stops materializing the whole role graph. No table-schema
    /// change — <see cref="SqlColumnsSyntaxHelper.ConfigureViews"/> drops-if-exists and recreates every object, so
    /// this is idempotent and picks up the new function bodies on an already-migrated database.
    /// </summary>
    public partial class RedeployAnchoredUpwardsRoleTree : Migration
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
