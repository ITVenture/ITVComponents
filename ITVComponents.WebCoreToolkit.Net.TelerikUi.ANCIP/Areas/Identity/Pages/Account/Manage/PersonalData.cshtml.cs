using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Manage;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Areas.Identity.Pages.Account.Manage
{
    public class PersonalDataModel : PageModel
    {
        private readonly IPageHandlerProvider<PersonalDataModel, IPersonalDataHandler> handlerImpl;
        private readonly ILogger<PersonalDataModel> _logger;
        private readonly IStringLocalizer<IdentityMessages> localizer;

        public PersonalDataModel(
            IPageHandlerProvider<PersonalDataModel,IPersonalDataHandler> handlerImpl,
            ILogger<PersonalDataModel> logger,
            IStringLocalizer<IdentityMessages> localizer)
        {
            this.handlerImpl = handlerImpl;
            _logger = logger;
            this.localizer = localizer;
        }

        public async Task<IActionResult> OnGet()
        {
            if (handlerImpl.Handler.UsePage)
            {
                var userExists = await handlerImpl.Handler.UserExists(User);
                if (!userExists)
                {
                    return NotFound(
                        localizer["Unable to load user with ID '{0}'.", handlerImpl.Handler.GetUserId(User)]);
                }

                return Page();
            }

            return NotFound();
        }
    }
}