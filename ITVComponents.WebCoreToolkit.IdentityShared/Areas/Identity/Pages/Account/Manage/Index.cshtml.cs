using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Manage;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Manage.Impl;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity.Pages.Account.Manage
{
    public partial class IndexModel : PageModel
    {
        private readonly IPageHandlerProvider<IndexModel, IIndexHandler> handlerImpl;
        private readonly IStringLocalizer<IdentityMessages> localizer;

        public IndexModel(
            IPageHandlerProvider<IndexModel, IIndexHandler> handlerImpl, 
            IStringLocalizer<IdentityMessages> localizer)
        {
            this.handlerImpl = handlerImpl;
            this.localizer = localizer;
        }

        [Display(Name = "Username")]
        public string Username { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Phone]
            [Display(Name = "Phone number")]
            public string PhoneNumber { get; set; }
        }

        private void LoadAsync(UserQueryTicket user)
        {
            var userName = user.UserName;
            var phoneNumber = user.PhoneNumber;

            Username = userName;

            Input = new InputModel
            {
                PhoneNumber = phoneNumber
            };
        }

        public async Task<IActionResult> OnGetAsync()
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
                    LoadAsync(user);
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
                    if (!ModelState.IsValid)
                    {
                        LoadAsync(user);
                        return Page();
                    }

                    var phoneNumber = user.PhoneNumber;
                    if (Input.PhoneNumber != phoneNumber)
                    {
                        var setPhoneResult = await handlerImpl.Handler.ChangePhoneNumber(user, Input.PhoneNumber);
                        if (!setPhoneResult.Succeeded)
                        {
                            StatusMessage = localizer["Unexpected error when trying to set phone number."];
                            return RedirectToPage();
                        }
                    }

                    await handlerImpl.Handler.RefreshSignIn(user);
                    StatusMessage = localizer["Your profile has been updated"];
                    return RedirectToPage();
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
