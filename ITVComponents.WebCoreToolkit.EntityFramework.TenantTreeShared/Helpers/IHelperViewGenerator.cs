using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Helpers
{
    public interface IHelperViewGenerator
    {
        public void GenerateUpwardsTenantView(MigrationBuilder migrationBuilder);

        public void GenerateDownwardsTenantView(MigrationBuilder migrationBuilder);

        public void GenerateUpwardsRoleUserPermissionsView(MigrationBuilder migrationBuilder);
    }
}
