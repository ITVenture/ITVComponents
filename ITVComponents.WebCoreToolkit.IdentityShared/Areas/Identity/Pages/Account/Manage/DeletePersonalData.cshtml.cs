using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Manage;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity.Pages.Account.Manage
{
    public class DeletePersonalDataModel : PageModel
    {
        private readonly IPageHandlerProvider<DeletePersonalDataModel, IDeletePersonalDataHandler> handlerImpl;
        private readonly ILogger<DeletePersonalDataModel> _logger;
        private readonly IStringLocalizer<IdentityMessages> localizer;
        public DeletePersonalDataModel(
            IPageHandlerProvider<DeletePersonalDataModel,IDeletePersonalDataHandler> handlerImpl,
            ILogger<DeletePersonalDataModel> logger, 
            IStringLocalizer<IdentityMessages> localizer)
        {
            this.handlerImpl = handlerImpl;
            _logger = logger;
            this.localizer = localizer;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }
        }

        public bool RequirePassword { get; set; }

        public async Task<IActionResult> OnGet()
        {
            if (handlerImpl.Handler.UsePage)
            {
                var user = await handlerImpl.Handler.FetchUser(User);
                if (!user.UserExists)
                {
                    return NotFound(
                        localizer["Unable to load user with ID '{0}'.", handlerImpl.Handler.GetUserId(User)]);
                }

                try
                {
                    RequirePassword = user.HasPassword;
                    return Page();
                }
                finally
                {
                    handlerImpl.Handler.ReleaseUser(user);
                }
            }

            return NotFound();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (handlerImpl.Handler.UsePage)
            {
                var user = await handlerImpl.Handler.FetchUser(User);
                if (!user.UserExists)
                {
                    return NotFound(
                        localizer["Unable to load user with ID '{0}'.", handlerImpl.Handler.GetUserId(User)]);
                }

                try
                {
                    RequirePassword = user.HasPassword;
                    if (RequirePassword)
                    {
                        if (!await handlerImpl.Handler.CheckPassword(user, Input.Password))
                        {
                            ModelState.AddModelError(string.Empty, localizer["Incorrect password."]);
                            return Page();
                        }
                    }

                    var dropResult = await handlerImpl.Handler.DeleteAccount(user);
                    if (!dropResult.UserDeleted)
                    {
                        throw new InvalidOperationException(
                            localizer["Unexpected error occurred deleting user with ID '{0}'.", dropResult.UserId]);
                    }

                    _logger.LogInformation("User with ID '{UserId}' deleted themselves.", dropResult.UserId);

                    return Redirect("~/");
                }
                finally
                {
                    handlerImpl.Handler.ReleaseUser(user);
                }
            }

            return NotFound();
        }
    }
}
