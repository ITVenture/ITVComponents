using System.Linq;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Flat;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Flat.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Onboarding.Areas.Identity.Pages.Account.Manage
{
    public class MyTenantsModel : PageModel
    {
        private readonly ISecurityContextWithOnboarding dbContext;
        private readonly UserManager<User> userManager;
        private readonly IStringLocalizer<MyTenantsModel> localizer;
        private readonly ISecurityAccessProvider securityAccessProvider;

        public MyTenantsModel(ISecurityContextWithOnboarding dbContext, UserManager<User> userManager, IStringLocalizer<MyTenantsModel> localizer, ISecurityAccessProvider securityAccessProvider)
        {
            this.dbContext = dbContext;
            this.userManager = userManager;
            this.localizer = localizer;
            this.securityAccessProvider = securityAccessProvider;
        }

        public ParticipatingTenantViewModel[] MyTenants { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var usr = await userManager.GetUserAsync(HttpContext.User);
            if (usr != null)
            {
                MyTenants = (from t in dbContext.BillingProfiles
                    join u in dbContext.Employees
                        on new { t.BillingProfileId, Email = usr.Email } equals new { u.BillingProfileId, Email = u.EMail }
                    select new ParticipatingTenantViewModel
                    {
                        BillingProfileId = t.BillingProfileId,
                        DefaultEmail = t.Email,
                        Name = t.DefaultAddress != null ? t.DefaultAddress.Name : "",
                        StatusText = localizer[$"TUStatusTxt_{u.InvitationStatus}"],
                        TenantCreated = t.TenantId != null,
                        Status = (int)u.InvitationStatus
                    }).ToArray();

                return Page();
            }

            return Unauthorized();
        }

        public async Task<IActionResult> OnPostAcceptInvitationAsync(TenantInfoShort info)
        {
            var usr = await userManager.GetUserAsync(HttpContext.User);
            if (usr != null)
            {
                var tmp = (from t in dbContext.BillingProfiles
                    join u in dbContext.Employees on new { t.BillingProfileId, Email = usr.Email } equals new
                        { u.BillingProfileId, Email = u.EMail }
                    where t.BillingProfileId == info.BillingProfileId && u.InvitationStatus == InvitationStatus.Pending
                    select u).FirstOrDefault();
                if (tmp != null)
                {
                    AcceptInvitation(tmp, usr);
                    await dbContext.SaveChangesAsync();
                    return Content(localizer["IVA_OK"]);
                }
            }

            return NotFound(localizer["No pending Invitation found!"]);
        }

        private void AcceptInvitation(Employee tmp, User usr)
        {

            using var a =securityAccessProvider.CreateForCaller(dbContext, new BaseTenantContextSecurityTrustConfig{ShowAllTenants= true, HideGlobals=true});
            var tenant = tmp.Tenant;
            if (!dbContext.TenantUsers.Any(n => n.UserId == usr.Id))
            {
                var tn = new TenantUser
                {
                    Enabled = true,
                    Tenant = tenant,
                    User = usr
                };
                dbContext.TenantUsers.Add(tn);
                foreach (var i in from r in dbContext.SecurityRoles
                         join t in dbContext.Tenants on r.TenantId equals t.TenantId
                         join er in dbContext.EmployeeRoles on r.RoleId equals er.RoleId
                         where t.TenantId == tenant.TenantId && er.EmployeeId == tmp.EmployeeId
                         select r)
                {
                    dbContext.TenantUserRoles.Add(new UserRole
                    {
                        Role = i,
                        User = tn
                    });
                }

                tmp.User = usr;
                tmp.TenantUser = tn;
            }

            tmp.InvitationStatus = InvitationStatus.Committed;
        }

        public class ParticipatingTenantViewModel
        {
            public int BillingProfileId { get; set; }
            public string DefaultEmail { get; set; }
            public string Name { get; set; }
            public string StatusText { get; set; }
            public int Status { get; set; }
            public bool TenantCreated { get; set; }
        }

        public class TenantInfoShort
        {
            public int BillingProfileId { get; set; }
        }
    }
}
