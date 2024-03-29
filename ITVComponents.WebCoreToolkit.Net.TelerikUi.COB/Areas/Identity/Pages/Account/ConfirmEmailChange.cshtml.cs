﻿using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.COB.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ConfirmEmailChangeModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IStringLocalizer<IdentityMessages> localizer;

        public ConfirmEmailChangeModel(UserManager<User> userManager, SignInManager<User> signInManager, IStringLocalizer<IdentityMessages> localizer)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            this.localizer = localizer;
        }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(string userId, string email, string code)
        {
            if (userId == null || email == null || code == null)
            {
                return RedirectToPage("/Index");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(localizer["Unable to load user with ID '{0}'.", userId]);
            }

            code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            var result = await _userManager.ChangeEmailAsync(user, email, code);
            if (!result.Succeeded)
            {
                StatusMessage = localizer["Error changing email."];
                return Page();
            }

            // In our UI email and user name are one and the same, so when we update the email
            // we need to update the user name.
            var setUserNameResult = await _userManager.SetUserNameAsync(user, email);
            if (!setUserNameResult.Succeeded)
            {
                StatusMessage = localizer["Error changing user name."];
                return Page();
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = localizer["Thank you for confirming your email change."];
            return Page();
        }
    }
}
