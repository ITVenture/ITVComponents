using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewComponents
{
    public class ConfigurationDiffViewer : ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync(string postTarget, string uploadModuleName, string handlerReason = null, string uploadHintCallback = null, string resultCallback = null, string handlerTarget = null, string idExtension = null, string uploaderDivId = null, string resultTab = null)
        {
            ViewData["PostTarget"] = postTarget; 
            ViewData["IdExtension"] = idExtension;
            ViewData["UploadModuleName"] = uploadModuleName;
            ViewData["UploaderDivId"] = uploaderDivId;
            ViewData["UploadHintCallback"] = uploadHintCallback;
            ViewData["HandlerTarget"] = handlerTarget;
            ViewData["ResultTab"] = resultTab;
            ViewData["HandlerReason"] = handlerReason;
            ViewData["ResultCallback"] = resultCallback;
            return View();
        }
    }
}
