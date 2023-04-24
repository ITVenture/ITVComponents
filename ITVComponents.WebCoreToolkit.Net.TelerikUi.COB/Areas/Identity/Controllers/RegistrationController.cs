using System;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EmailDnsValidation;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding;
using ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.Helpers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.COB.Areas.Identity.DTO;
using ITVComponents.WebCoreToolkit.Options;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using User = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.User;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.COB.Areas.Identity.Controllers
{
    [Area("Identity"), AllowAnonymous, ConstructedGenericControllerConvention]
    public class RegistrationController<TContext> : Controller
    where TContext: DbContext, ISecurityContextWithOnboarding
    {
        private readonly UserManager<User> userManager;
        private readonly SignInManager<User> signInManager;
        private readonly ILogger<RegistrationController<TContext>> logger;
        private readonly IEmailSender emailSender;
        private readonly IStringLocalizer<IdentityMessages> localizer;
        private readonly TContext dbContext;
        private readonly IGlobalSettings<TenantSetupOptions> setupOptions;
        private readonly ITenantTemplateHelper tenantInitializer;
        private readonly IOptions<AuthenticationHandlerOptions> availableAuthenticators;
        private readonly ISecurityRepository securityRepo;

        public RegistrationController(UserManager<User> userManager,
            SignInManager<User> signInManager,
            ILogger<RegistrationController<TContext>> logger,
            IEmailSender emailSender,
            IStringLocalizer<IdentityMessages> localizer,
            TContext dbContext,
            IGlobalSettings<TenantSetupOptions> setupOptions,
            ITenantTemplateHelper tenantInitializer,
            IOptions<AuthenticationHandlerOptions> availableAuthenticators,
            ISecurityRepository securityRepo)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.logger = logger;
            this.emailSender = emailSender;
            this.localizer = localizer;
            this.dbContext = dbContext;
            this.setupOptions = setupOptions;
            this.tenantInitializer = tenantInitializer;
            this.availableAuthenticators = availableAuthenticators;
            this.securityRepo = securityRepo;
        }
        public IActionResult Index(string returnUrl = null)
        {
            ViewData["returnUrl"] = returnUrl ?? "/";
            return View();
        }

        public IActionResult _RegisterCompany(string returnUrl = null)
        {
            ViewData["returnUrl"] = returnUrl ?? "/";
            return PartialView();
        }

        public async Task<IActionResult> _RegisterUser(string returnUrl = null)
        {
            ViewData["returnUrl"] = returnUrl;
            var tmp = (await signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            var providers = (from t in tmp
                join a in availableAuthenticators.Value.AuthenticationHandlers
                    on t.Name equals a.AuthenticationSchemeName into j
                from c in j.DefaultIfEmpty()
                where c?.DisplayInHandlerSelection ?? true
                select c ?? new AuthenticationHandlerDefinition
                {
                    AuthenticationSchemeName = t.Name,
                    DisplayInHandlerSelection = true,
                    DisplayName = t.DisplayName,
                    LogoFile = $"/images/logo/login-logo{t.DisplayName}.png"
                }).ToList();
            ViewData["externalLogins"] = providers;
            return PartialView();
        }

        public async Task<IActionResult> LoginExternal(string provider, string returnUrl = null)
        {
            var redirectUrl = Url.Action("_ExternalLoginCallback", values: new { returnUrl });
            var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return new ChallengeResult(provider, properties);
        }

        [HttpGet]
        public async Task<IActionResult> _ExternalLoginCallback(string returnUrl = null, string remoteError = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");
            if (remoteError != null)
            {
                ViewData["errorMessage"] = localizer["Error from external provider: {0}", remoteError];
                return RedirectToPage("/Account/Login", new { ReturnUrl = returnUrl, area="Identity" });
            }
            var info = await signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ViewData["errorMessage"] = localizer["Error loading external login information."];
                return RedirectToPage("/Account/Login", new { ReturnUrl = returnUrl, area = "Identity" });
            }

            // Sign in the user with this external login provider if the user already has a login.
            var result = await signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
            if (result.Succeeded)
            {
                var rs = await signInManager.UpdateExternalAuthenticationTokensAsync(info);
                logger.LogInformation("{Name} logged in with {LoginProvider} provider.", info.Principal.Identity.Name, info.LoginProvider);
                return LocalRedirect(returnUrl);
            }
            if (result.IsLockedOut)
            {
                return RedirectToPage("/Account/Lockout", new {area="Identity"});
            }
            else
            {

                // If the user does not have an account, then ask the user to create an account.
                ViewData["returnUrl"] = returnUrl;
                ViewData["providerDisplayName"] = info.ProviderDisplayName;
                //info.AuthenticationProperties.Items
                var model = new ExternalUserInputModel();
                if (info.Principal.HasClaim(c => c.Type == System.Security.Claims.ClaimTypes.Email))
                {
                    model.Email = info.Principal.FindFirstValue(System.Security.Claims.ClaimTypes.Email);
                }
                else if (info.Principal.HasClaim(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier))
                {
                    var firstVal = info.Principal.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
                    try
                    {
                        var ml = new EmailAddress(firstVal);
                        if (ml.FormatOk)
                        {
                            model.Email = firstVal;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error checking E-Mail address");
                    }
                }

                //HttpContext.
                return View(model);
            }
        }

        [HttpPost,ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateExternalUser(ExternalUserInputModel externalUser, string provider, string returnUrl = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");
            // Get the information about the user from the external login provider
            var info = await signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ViewData["errorMessage"] = localizer["Error loading external login information during confirmation."];
                return RedirectToPage("/Account/Login", new { area = "Identity", ReturnUrl = returnUrl });
            }

            if (ModelState.IsValid)
            {
                var mail = new EmailAddress(externalUser.Email, new MailDomainValidator());
                if (mail.FormatOk && mail.DomainOk)
                {
                    var user = new User { UserName = externalUser.Email, Email = externalUser.Email };
                    var result = await userManager.CreateAsync(user);
                    if (result.Succeeded)
                    {
                        result = await userManager.AddLoginAsync(user, info);
                        if (result.Succeeded)
                        {
                            logger.LogInformation("User created an account using {Name} provider.", info.LoginProvider);

                            var userId = await userManager.GetUserIdAsync(user);
                            var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
                            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                            var callbackUrl = Url.Page(
                                "/Account/ConfirmEmail",
                                pageHandler: null,
                                values: new { area = "Identity", userId = userId, code = code },
                                protocol: Request.Scheme);

                            await emailSender.SendEmailAsync(externalUser.Email, localizer["Confirm your email"],
                                localizer["MailConfirmMsgBody",
                                    HtmlEncoder.Default.Encode(callbackUrl)]);

                            // If account confirmation is required, we need to show the link if we don't have a real email sender
                            if (userManager.Options.SignIn.RequireConfirmedAccount)
                            {
                                return RedirectToPage("/Account/RegisterConfirmation",
                                    new { area = "Identity", Email = externalUser.Email });
                            }

                            await signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);

                            return Json(returnUrl);
                        }
                    }

                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
                else
                {
                    if (!mail.FormatOk)
                    {
                        ModelState.AddModelError(string.Empty, localizer["InvalidMailFormat"]);
                    }
                    else if (!mail.DomainOk)
                    {
                        ModelState.AddModelError(string.Empty, localizer["InvalidMailDomain"]);
                    }
                }
            }

            return
                Json(ModelState); //RedirectToPage("Account/Login", new { area = "Identity", ReturnUrl = returnUrl });
        }

        public async Task<IActionResult> CreateUser(UserInputModel userInput,string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            if (ModelState.IsValid)
            {
                var user = new User { UserName = userInput.Email, Email = userInput.Email };
                var confirmation = await CreateUser(user, userInput.Password, returnUrl, true);
                if (confirmation.Success)
                {
                    if (!confirmation.LoggedIn)
                    {
                        return Json(Url.Page("/Account/RegisterConfirmation", new { email = userInput.Email, returnUrl = returnUrl, area="Identity" }));
                    }
                    else
                    {
                        return Json(returnUrl);
                    }
                }
            }

            return Json(ModelState); // RedirectToAction("Index");
        }

        public async Task<IActionResult> CreateCompany(CompanyInputModel companyInput, string returnUrl=null)
        {
            returnUrl ??= Url.Content("~/");
            if (ModelState.IsValid)
            {
                var user = new User { UserName = companyInput.Email, Email = companyInput.Email };
                var confirmation = await CreateUser(user, companyInput.Password, returnUrl, false);
                if (confirmation.Success)
                {
                    var tenant = new Tenant
                    {
                        DisplayName = companyInput.Name, TenantName = Guid.NewGuid().ToString("N"),
                        TenantPassword = Convert.ToBase64String(AesEncryptor.CreateKey())
                    };
                    dbContext.Tenants.Add(tenant);
                    var admin = new TenantUser { Enabled = true, Tenant = tenant, UserId = user.Id };
                    dbContext.TenantUsers.Add(admin);
                    var company = new CompanyInfo
                    {
                        DefaultAddress = new DefaultAddress
                        {
                            Name = companyInput.Name,
                            Addition1 = companyInput.Addition1,
                            Addition2 = companyInput.Addition2,
                            City = companyInput.City,
                            Number = companyInput.Number,
                            Street = companyInput.Street,
                            Zip = companyInput.Zip
                        },
                        Email = companyInput.Email,
                        OwnerUserId = user.Id,
                        PhoneNumber = companyInput.PhoneNumber,
                        Tenant = tenant,
                        Admin = admin,
                        UseInvoiceAddr = companyInput.UseInvoiceAddr,
                        InvoiceAddress = companyInput.UseInvoiceAddr
                            ? new InvoiceAddress
                            {
                                Name = companyInput.InvoiceName,
                                Addition1 = companyInput.InvoiceAddition1,
                                Addition2 = companyInput.InvoiceAddition2,
                                Street = companyInput.InvoiceStreet,
                                Number = companyInput.InvoiceNumber,
                                City = companyInput.InvoiceCity,
                                Zip = companyInput.InvoiceZip
                            }
                            : null
                    };

                    dbContext.Companies.Add(company);
                    dbContext.Employees.Add(new Employee
                    {
                        InvitationStatus = InvitationStatus.Committed,
                        EMail = companyInput.Email,
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

                    if (!confirmation.LoggedIn)
                    {
                        return Json(Url.Page("/Account/RegisterConfirmation", new { email = companyInput.Email, returnUrl = returnUrl, area = "Identity" }));
                    }
                    else
                    {
                        await signInManager.SignInAsync(user, isPersistent: false);
                        return Json(returnUrl);
                    }
                }
            }

            return Json(ModelState); //RedirectToAction("Index");
        }

        private async Task<UserCreationConfirmation> CreateUser(User user, string password, string returnUrl, bool logUserIn)
        {
            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                logger.LogInformation("User created a new account with password.");

                var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page(
                    "/Account/ConfirmEmail",
                    pageHandler: null,
                    values: new { area = "Identity", userId = user.Id, code = code, returnUrl = returnUrl },
                    protocol: Request.Scheme);

                await emailSender.SendEmailAsync(
                    user.Email,
                    localizer["Confirm your email"],
                    localizer["Please confirm your account by <a href='{0}'>clicking here</a>.", HtmlEncoder.Default.Encode(callbackUrl)]);

                var retVal = new UserCreationConfirmation
                {
                    Success = true
                };

                if (userManager.Options.SignIn.RequireConfirmedAccount)
                {
                    retVal.LoggedIn = false;
                }
                else
                {
                    if (logUserIn)
                    {
                        await signInManager.SignInAsync(user, isPersistent: false);
                    }

                    retVal.LoggedIn = true;
                }

                return retVal;
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return new UserCreationConfirmation { Success = false };
        }
    }
}
