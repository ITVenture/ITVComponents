using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding;
using ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
    
namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.COB.Areas.Identity.Pages.Account.Manage
{
    public partial class CreateTenantModel: PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IStringLocalizer<IdentityMessages> localizer;
        private readonly ISecurityContextWithOnboarding dbContext;
        private readonly IGlobalSettings<TenantSetupOptions> setupOptions;
        private readonly ITenantTemplateHelper tenantInitializer;
        //private readonly ITenantTemplateHelper<ISecurityContextWithOnboarding> tenantInitializer;

        public CreateTenantModel(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IStringLocalizer<IdentityMessages> localizer,
            ISecurityContextWithOnboarding dbContext,
            IGlobalSettings<TenantSetupOptions> setupOptions,
            ITenantTemplateHelper tenantInitializer)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            this.localizer = localizer;
            this.dbContext = dbContext;
            this.setupOptions = setupOptions;
            this.tenantInitializer = tenantInitializer;
        }

        public string ReturnUrl { get; set; }

        [BindProperty]
        public CompanyInput Input { get; set; }

        public class CompanyInput
        {
            [Required, MaxLength(256), Display(Name = "Company E-Mail"), EmailAddress(ErrorMessage = "ITV:DataTypeAttribute.EmailAddress_ValidationError")]
            public string Email { get; set; }

            [MaxLength(100), Display(Name = "Company Phone"), Phone(ErrorMessage = "ITV:DataTypeAttribute.PhoneNumber")]
            public string PhoneNumber { get; set; }

            [MaxLength(1024), Required, Display(Name = "Company Name")]
            public string Name { get; set; }

            [MaxLength(256), Display(Name = "Addition1")]
            public string Addition1 { get; set; }

            [MaxLength(256), Display(Name = "Addition2")]
            public string Addition2 { get; set; }

            [MaxLength(256), Display(Name = "Street")]
            public string Street { get; set; }

            [MaxLength(256), Display(Name = "Number")]
            public string Number { get; set; }

            [MaxLength(10), Required, Display(Name = "Zip")]
            public string Zip { get; set; }

            [MaxLength(256), Required, Display(Name = "City")]
            public string City { get; set; }

            [Display(Name = "UseInvoiceAddr")]
            public bool UseInvoiceAddr { get; set; }

            [ConditionalRequired(BackEndCondition = "UseInvoiceAddr", ClientCondition = "ITVenture.Pages.Identity.Register.RequireInvoiceFields", ErrorMessage = "RequiredForInvoiceAddr")]
            [Display(Name = "Invoicee Name")]
            public string InvoiceName { get; set; }

            [MaxLength(256), Display(Name = "Addition1")]
            public string InvoiceAddition1 { get; set; }

            [MaxLength(256), Display(Name = "Addition2")]
            public string InvoiceAddition2 { get; set; }

            [MaxLength(256), Display(Name = "Street")]
            public string InvoiceStreet { get; set; }

            [MaxLength(256), Display(Name = "Number")]
            public string InvoiceNumber { get; set; }

            [MaxLength(10), Display(Name = "Zip")]
            [ConditionalRequired(BackEndCondition = "UseInvoiceAddr", ClientCondition = "ITVenture.Pages.Identity.Register.RequireInvoiceFields", ErrorMessage = "RequiredForInvoiceAddr")]
            public string InvoiceZip { get; set; }

            [MaxLength(256), Display(Name = "City")]
            [ConditionalRequired(BackEndCondition = "UseInvoiceAddr", ClientCondition = "ITVenture.Pages.Identity.Register.RequireInvoiceFields", ErrorMessage = "RequiredForInvoiceAddr")]
            public string InvoiceCity { get; set; }

            [ForceTrue(ErrorMessage = "Must accept the terms of Service."), Display(Name = "AcceptTOS")]
            public bool AcceptTOS { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(string returnUrl=null)
        {
            ReturnUrl = returnUrl;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var tenant = new Tenant
            {
                DisplayName = Input.Name, TenantName = Guid.NewGuid().ToString("N"),
                TenantPassword = Convert.ToBase64String(AesEncryptor.CreateKey())
            };
            dbContext.Tenants.Add(tenant);
            var admin = new TenantUser { Enabled = true, Tenant = tenant, UserId = user.Id };
            dbContext.TenantUsers.Add(admin);
            var company = new CompanyInfo
            {
                DefaultAddress = new DefaultAddress
                {
                    Name = Input.Name,
                    Addition1 = Input.Addition1,
                    Addition2 = Input.Addition2,
                    City = Input.City,
                    Number = Input.Number,
                    Street = Input.Street,
                    Zip = Input.Zip
                },
                Email = Input.Email,
                OwnerUserId = user.Id,
                PhoneNumber = Input.PhoneNumber,
                Tenant = tenant,
                Admin = admin,
                UseInvoiceAddr = Input.UseInvoiceAddr,
                InvoiceAddress = Input.UseInvoiceAddr
                    ? new InvoiceAddress
                    {
                        Name = Input.InvoiceName,
                        Addition1 = Input.InvoiceAddition1,
                        Addition2 = Input.InvoiceAddition2,
                        Street = Input.InvoiceStreet,
                        Number = Input.InvoiceNumber,
                        City = Input.InvoiceCity,
                        Zip = Input.InvoiceZip
                    }
                    : null
            };

            dbContext.Companies.Add(company);
            dbContext.Employees.Add(new Employee
            {
                InvitationStatus = InvitationStatus.Committed,
                EMail = user.Email,
                Company = company,
                User = user,
                TenantUser = admin,
                FirstName = "Admin",
                LastName = "Admin",
                Tenant = tenant
            });

            await dbContext.SaveChangesAsync();
            var cfg = setupOptions.ValueOrDefault;
            if (!string.IsNullOrEmpty(cfg.BasicTenantTemplate))
            {
                var tmpl = dbContext.TenantTemplates.First(n => n.Name == cfg.BasicTenantTemplate);
                var mku = JsonHelper.FromJsonString<TenantTemplateMarkup>(tmpl.Markup);
                tenantInitializer.ApplyTemplate(tenant, mku, ct =>
                {
                    var ctx = ct as ISecurityContextWithOnboarding;
                    if (!string.IsNullOrEmpty(cfg.AdminUserRole))
                    {
                        var rl = ctx.SecurityRoles.First(n =>
                            n.TenantId == tenant.TenantId && n.RoleName == cfg.AdminUserRole);
                        ctx.TenantUserRoles.Add(new UserRole
                        {
                            Role = rl,
                            User = admin
                        });

                        ctx.SaveChanges();
                    }
                });
            }

            return LocalRedirect(returnUrl);
        }
    }
}
