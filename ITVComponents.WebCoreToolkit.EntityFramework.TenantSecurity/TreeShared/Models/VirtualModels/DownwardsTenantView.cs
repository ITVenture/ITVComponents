using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.VirtualModels
{
    public class DownwardsTenantView
    {
        public int TopmostTenantId { get; set; }

        public string TopmostTenantName { get; set; }

        public int ChildTenantId { get; set; }

        public string ChildTenantName { get; set; }

        public int ChildLevel { get; set; }
    }
}



/*
--downwards
with r as (select rolepermissionid as camefromid, rolepermissionid, roleid, permissionid, tenantid, originid, roleroleid, 1 as lvl  from rolepermissions where tenantid=24
union all
select r.camefromid, u.rolepermissionid, u.roleid, u.permissionid, u.tenantid, u.originid, u.roleroleid, r.lvl+1 as lvl  from rolepermissions u inner join r on u.OriginId = r.RolePermissionId)
select * from r where permissionid = 48





--upwards
with r as (select rolepermissionid as camefromid, rolepermissionid, roleid, permissionid, tenantid, originid, roleroleid, 1 as lvl from rolepermissions
union all
select r.camefromid, u.rolepermissionid, u.roleid, u.permissionid, u.tenantid, u.originid, u.roleroleid, r.lvl+1 as lvl  from rolepermissions u inner join r on r.originid = u.rolepermissionid)
select * from r where camefromid = 318
*/