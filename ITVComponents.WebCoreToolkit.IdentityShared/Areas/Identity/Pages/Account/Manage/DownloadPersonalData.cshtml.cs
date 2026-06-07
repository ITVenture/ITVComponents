using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
    public class DownloadPersonalDataModel : PageModel
    {
        private readonly IPageHandlerProvider<DownloadPersonalDataModel, IDownloadPersonalDataHandler> handlerImpl;
        private readonly ILogger<DownloadPersonalDataModel> _logger;
        private readonly IStringLocalizer<IdentityMessages> localizer;

        public DownloadPersonalDataModel(
            IPageHandlerProvider<DownloadPersonalDataModel, IDownloadPersonalDataHandler> handlerImpl,
            ILogger<DownloadPersonalDataModel> logger,
            IStringLocalizer<IdentityMessages> localizer)
        {
            this.handlerImpl = handlerImpl;
            _logger = logger;
            this.localizer = localizer;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (handlerImpl.Handler.UsePage)
            {
                var user = await handlerImpl.Handler.FetchUser(User);
                var uid = handlerImpl.Handler.GetUserId(User);
                if (!user.UserExists)
                {
                    return NotFound(localizer["Unable to load user with ID '{0}'.", uid]);
                }

                try
                {
                    _logger.LogInformation("User with ID '{UserId}' asked for their personal data.", uid);

                    // Only include personal data for download
                    var personalData = await handlerImpl.Handler.FetchPersonalData(user);

                    Response.Headers.Add("Content-Disposition", "attachment; filename=PersonalData.json");
                    return new FileContentResult(JsonSerializer.SerializeToUtf8Bytes(personalData), "application/json");
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
