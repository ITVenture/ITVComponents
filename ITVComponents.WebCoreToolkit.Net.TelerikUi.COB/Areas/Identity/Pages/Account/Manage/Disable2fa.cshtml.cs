﻿using System;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.COB.Areas.Identity.Pages.Account.Manage
{
    public class Disable2faModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly ILogger<Disable2faModel> _logger;
        private readonly IStringLocalizer<IdentityMessages> localizer;
        public Disable2faModel(
            UserManager<User> userManager,
            ILogger<Disable2faModel> logger,
            IStringLocalizer<IdentityMessages> localizer)
        {
            _userManager = userManager;
            _logger = logger;
            this.localizer = localizer;
        }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGet()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound(localizer["Unable to load user with ID '{0}'.", _userManager.GetUserId(User)]);
            }

            if (!await _userManager.GetTwoFactorEnabledAsync(user))
            {
                
                throw new InvalidOperationException(localizer["Cannot disable 2FA for user with ID '{0}' as it's not currently enabled.", _userManager.GetUserId(User)]);
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound(localizer["Unable to load user with ID '{0}'.", _userManager.GetUserId(User)]);
            }

            var disable2faResult = await _userManager.SetTwoFactorEnabledAsync(user, false);
            if (!disable2faResult.Succeeded)
            {
                throw new InvalidOperationException(localizer["Unexpected error occurred disabling 2FA for user with ID '{0}'.", _userManager.GetUserId(User)]);
            }

            _logger.LogInformation("User with ID '{UserId}' has disabled 2fa.", _userManager.GetUserId(User));
            StatusMessage = localizer["2fa has been disabled. You can reenable 2fa when you setup an authenticator app"];
            return RedirectToPage("./TwoFactorAuthentication");
        }
    }
}