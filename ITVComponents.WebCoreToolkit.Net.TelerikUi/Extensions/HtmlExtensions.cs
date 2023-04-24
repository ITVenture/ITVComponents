using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.Encodings.Web;
using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.Net.Extensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Helpers;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.WebUtilities;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Extensions
{
    public static class HtmlExtensions
    {
        /// <summary>
        /// Generates a script to lazy-load the ClientDetailTemplate view of a kendo-grid
        /// </summary>
        /// <param name="html">the html-helper of a razor-view</param>
        /// <param name="scriptId">the name of the script that is referred to by the grid</param>
        /// <param name="dummyDivId">the id of the dummy-div that will be replaced (or filled) with the downloaded content</param>
        /// <param name="viewAction">the view-action where to get the lazy-content</param>
        /// <param name="replaceDummy">indicates whether to replace the dummy-div</param>
        /// <returns>the generated content</returns>
        public static IHtmlContent DetailViewScript(this IHtmlHelper html, string scriptId, string dummyDivId, string viewAction, bool replaceDummy = true)
        {
            return html.Raw(@$"<script id=""{scriptId}"" type=""text/x-kendo-template"">
                <div id=""{dummyDivId}"" viewSrc=""{viewAction}""></div>
                <script type=""text/javascript"">
                $(""\\#{dummyDivId}"").loadPartial({replaceDummy.ToString().ToLower()});
                <\/script>

                </script>");
        }

        /// <summary>
        /// Adds a reference to the ITVComponents-Base-script
        /// </summary>
        /// <param name="html"></param>
        /// <param name="version">the version of the current application running</param>
        /// <returns></returns>
        public static IHtmlContent ItvScriptRef(this IHtmlHelper html, string version = null)
        {
            return html.Raw($@"<script src=""{"/_content/ITVComponents.WebCoreToolkit.Net.TelerikUi/js/itvComponents.min.js".ExtendUrlWithVersion()}""></script>
<script src=""{"/_content/ITVComponents.WebCoreToolkit.Net.TelerikUi/js/itvJqPlugs.min.js".ExtendUrlWithVersion()}""></script>");
        }

        public static IHtmlContent ItvCustomBootstrapV4(this IHtmlHelper html, string version= null)
        {
            return html.Raw($@"<link href=""{"/_content/ITVComponents.WebCoreToolkit.Net.TelerikUi/css/itvComponentsBS4.min.css".ExtendUrlWithVersion()}"" type=""text/css"" rel=""stylesheet"">");
        }

        public static IHtmlContent Uploader(this IHtmlHelper target, UploadMode mode, string uploaderModule, string uploadReason, string callbackMethod, string errorCallbackMethod = null, int height = 0, int width = 0, Dictionary<string, string> customAttributes = null)
        {
            string uniqueDivId;
            uniqueDivId = CustomActionHelper.RandomName("uploadDiv");
            string template = $@"<div purpose='{mode}' id='{uniqueDivId}' nameTarget='{uniqueDivId}' uploadModule='{uploaderModule}' uploadReason='{uploadReason}' class='dropzone dropzone-flexi' {(height != 0 || width != 0 ? $"style=\"display:table-cell;position:relative;overflow-y:scroll;{(height != 0 ? $"height:{height}px;" : "")}{(width != 0 ? $"width:{width}px;" : "")}\"" : "")} {(customAttributes != null ? string.Join(" ", from t in customAttributes select $"{t.Key}='{t.Value}'") : "")}></div>
<script>ITVenture.Tools.Uploader.prepareUploadRegion({{
    {uniqueDivId}: {callbackMethod},
    {uniqueDivId}_ERROR: function(file,message){{
        var retVal = false;
        {(!string.IsNullOrEmpty(errorCallbackMethod)?$"retVal={errorCallbackMethod}(file,message);":"")}
        return retVal;
    }}
}});
</script>";

            return target.Raw(template);
        }


        public static IHtmlContent AjaxFkFor<TModel, TResult>(this IHtmlHelper<TModel> target, Expression<Func<TModel, TResult>> expression, string repoName, string tableName, object customFilterData = null, int minSearchLength = 0, Dictionary<string, string> nameBuffer = null, string cascadeFrom = null) where TModel : class
        {
            string dataCallback = null;
            string columnName = target.DisplayNameFor(expression);
            string id = CustomActionHelper.RandomName($"FK_{repoName}_{tableName}_{columnName}");
            nameBuffer?.Add(columnName, id);
            string dataCallbackBody = null;
            var route = target.ViewContext.HttpContext.Request.RouteValues;
            string area = null;
            if (route.ContainsKey("area"))
            {
                area = (string) route["area"];
            }

            if (customFilterData != null)
            {
                dataCallback = $"ITVenture.Tools.ListCallbackHelper.dataCallbacks.{CreateDataScriptFor(repoName, tableName, columnName, customFilterData, out dataCallbackBody)}";
            }
            
            target.Raw($@"<script>
            {dataCallbackBody}
</script>").WriteTo(target.ViewContext.Writer, HtmlEncoder.Default);
            return target.EditorFor(expression, "ApiForeignKey", new Dictionary<string, object>
            {
                {$"{columnName}_RepoName", repoName},
                {$"{columnName}_TableName", tableName},
                {$"{columnName}_ID", id},
                {$"{columnName}_minLength", minSearchLength},
                {$"{columnName}_CascadeTarget", cascadeFrom},
                {$"{columnName}_DataCallback", dataCallback},
                {$"{columnName}_Area", area}
            });
        }

        public static IHtmlContent AjaxFkLabelFor<TModel, TResult>(this IHtmlHelper<TModel> target,
            Expression<Func<TModel, TResult>> expression, string targetElement, string repoName, string tableName, string area = null, string emptyLabel = null)
        {
            string columnName = target.DisplayNameFor(expression);
            var prefix = CustomActionHelper.RandomName("FkFilterArg");
            return target.Raw($@"
<div id=""{targetElement}_{prefix}""></div>
<script>
    var {prefix}_id = $(""targetElement"").closest("".k-popup-edit-form"").data(""kendoEditable"").options.model[""{columnName}""];
    var {prefix}_repo = ""{repoName}"";
    var {prefix}_emptyLabel = ""{emptyLabel??"Not Found"}"";
    var {prefix}_table = ""{tableName}"";
    var {prefix}_area = ""{area}"";
    $(async function(){{
        var content = await ITVenture.Tools.ListCallbackHelper.ResolveFk({prefix}_repo, {prefix}_table, {prefix}_id, {prefix}_emptyLabel, {prefix}_area);
        $(""#{targetElement}_{prefix}"").html(content);
    }});
</script>");
        }

        public static IHtmlContent MultiSelectFor<TModel, TResult>(this IHtmlHelper<TModel> target, Expression<Func<TModel, TResult>> expression, string repoName, string tableName, string placeholder = "") where TModel : class
        {
           //var retVal = target.Bound(expression);
            string columnName = target.DisplayNameFor(expression);
            var route = target.ViewContext.HttpContext.Request.RouteValues;
            string area = null;
            if (route.ContainsKey("area"))
            {
                area = (string) route["area"];
            }
            
            return target.EditorFor(expression, "MultiSelect", new Dictionary<string, object>
            {
                {$"{columnName}_RepoName", repoName},
                {$"{columnName}_TableName", tableName},
                {$"{columnName}_Area", area},
                {$"{columnName}_Placeholder", placeholder}
            });
        }

        public static IHtmlContent UploaderFor<TModel, TResult>(this IHtmlHelper<TModel> target,
                Expression<Func<TModel, TResult>> expression,
                UploadMode mode,
                string uploaderModule,
                string uploadReason,
                bool preserveOriginal,
                string originalFieldName,
                string uploadHint,
                string customUploadCallback,
                bool setOriginalOnlyIfNull,
                string errorHandler)
            {
                string uniqueDivId, uniqueInputId, uniqueOrigId, uniqueDummyId;
                uniqueDivId = CustomActionHelper.RandomName("uploadDiv");
                uniqueInputId = CustomActionHelper.RandomName("valueBox");
                uniqueOrigId = CustomActionHelper.RandomName("oriValueBox");
                uniqueDummyId = CustomActionHelper.RandomName("dummyDiv");
                var notNullOrig = $@"""var $ori = $(\""\\#{uniqueOrigId}\"").val();""
.concat(        ""if ($ori === null || $ori.trim() === \""\""){{"")
.concat(""$(\""\\#{uniqueOrigId}\"").val(oriValue).change();"")
.concat(""}}"")";
                var additionalCallback = $@"""{(!string.IsNullOrEmpty(customUploadCallback)?$"{customUploadCallback}(value{(preserveOriginal ? ", oriValue" : "")});":"")}""";
                string template = $@"<div id=""{uniqueDummyId}""></div>
<script>
var tmpl = ""\u003Cdiv purpose=\""{mode}\"" nameTarget=\""{uniqueDivId}\"" uploadModule=\""{uploaderModule}\"" uploadReason=\""{uploadReason}\"" uploadHint=\""{uploadHint}\"" class=\""dropzone\""\u003E""
.concat(""\u003C/div\u003E"")
.concat(""{target.HiddenFor(expression, new {id = uniqueInputId}).GetString().Replace("#", @"\\#").Replace(@"""", @"\""").Replace(@"<", @"\u003C").Replace(@">", @"\u003E")}"")
.concat({(preserveOriginal ? $@"""\u003Cinput data-bind=\""value:{originalFieldName}\"" id=\""{uniqueOrigId}\"" name=\""{originalFieldName}\"" type=\""hidden\"" value=\""\""  /\u003E""" : "\"\"")})
.concat(""\u003Cscript>ITVenture.Tools.Uploader.prepareUploadRegion({{"")
.concat(    ""{uniqueDivId}: function(value{(preserveOriginal ? ", oriValue" : "")}){{"")
.concat(        ""$(\""\\#{uniqueInputId}\"").val(value).change();"")
.concat({(preserveOriginal ? $@"{(setOriginalOnlyIfNull ? notNullOrig : $@"""$(\""\\#{uniqueOrigId}\"").val(oriValue).change();""")}":"\"\"")})
.concat({additionalCallback})
.concat(""}},"")
.concat(    ""{uniqueDivId}_ERROR: function(file, message){{"")
.concat(    ""var retVal = false;"")
.concat(    ""{(!string.IsNullOrEmpty(errorHandler)?$"retVal={errorHandler}(file,message);":"")}"")
.concat(    ""return retVal;"")
.concat(""}},"")
.concat(""}});\u003C/script\u003E"");
var template = kendo.template(tmpl);
var data = $(""#{uniqueDummyId}"").parent().parent().data(""kendoEditable"").options.model;
var result = template(data);
$(""#{uniqueDummyId}"").replaceWith(result);
</script>";
                return target.Raw(template);
            }
        
        
        internal static string CreateDataScriptFor(string repoName, string tableName, string memberName, object customDataFilter, out string dataFilter)
        {
            string filterFunction = CustomActionHelper.RandomName($"dataCbFx_{repoName}_{tableName}_{memberName}");
            dataFilter = $@"ITVenture.Tools.ListCallbackHelper.dataCallbacks.{filterFunction} = function(dataRequest){{
            var obj = {JsonHelper.ToJson(customDataFilter)};
            var retVal = $.extend({{}},dataRequest,obj);

            return retVal;
}};";
            return filterFunction;
        }
    }
}
