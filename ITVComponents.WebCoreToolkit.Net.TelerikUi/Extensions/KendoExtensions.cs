    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Text;
    using System.Text.Encodings.Web;
    using System.Threading.Tasks;
    using System.Web;
    using ITVComponents.Helpers;
    using ITVComponents.WebCoreToolkit.Net.TelerikUi.Extensions;
    using ITVComponents.WebCoreToolkit.Net.TelerikUi.Helpers;
    using ITVComponents.WebCoreToolkit.Net.TelerikUi.Resources;
    using ITVComponents.WebCoreToolkit.Routing;
    using Kendo.Mvc.UI;
    using Kendo.Mvc.UI.Fluent;
    using Microsoft.AspNetCore.Mvc.Routing;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.Extensions.DependencyInjection;

    namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Extensions
    {
        public static class KendoExtensions
        {
            /// <summary>
            /// Enables the ForeignKey row to show a filter-textbox
            /// </summary>
            /// <param name="builder">the event-builder of the kendoGrid</param>
            /// <param name="additionalEditHandler">an additional handler that needs to be executed after enabling the textbox</param>
            /// <returns>the provided GridEventBuilder for method-chaining</returns>
            public static GridEventBuilder UseForeignKeyFilter(this GridEventBuilder builder,
                bool valuePrimitive = false, string additionalEditHandler = null)
            {
                return builder.Edit(
                    $"ITVenture.Tools.KendoExtensions.UseForeignKeyFilter({(valuePrimitive?"true":"false")},{additionalEditHandler})");
            }

            /// <summary>
            /// Enables the ForeignKey row to show a filter-textbox
            /// </summary>
            /// <param name="builder">the event-builder of the kendoGrid</param>
            /// <param name="additionalEditHandler">an additional handler that needs to be executed after enabling the textbox</param>
            /// <returns>the provided GridEventBuilder for method-chaining</returns>
            public static GridEventBuilder ForeignKeyValuePrimitive(this GridEventBuilder builder,
                string additionalEditHandler = null)
            {
                return builder.Edit($"ITVenture.Tools.KendoExtensions.ForeignKeyPrimitive({additionalEditHandler})");
            }

            /// <summary>
            /// Enables the Refresh-Button in the Command-Toolbar of the table
            /// </summary>
            /// <typeparam name="TModel">the model of the table</typeparam>
            /// <param name="target">the command-factory for the given table</param>
            /// <returns></returns>
            public static GridToolBarCustomCommandBuilder RefreshTable<TModel>(this GridToolBarCommandFactory<TModel> target) where TModel : class
            {
                return target.Custom().Name(CustomActionHelper.RandomName("refresh")).Text("\u200B").HtmlAttributes(new Dictionary<string, object> {{"onclick", "ITVenture.Tools.TableHelper.refreshTable(event)"}, {"class", "itv-tool-button itv-fa-tbx"}, {"title", TextsAndMessagesHelper.IWCN_KX_RTB_Caption}}).IconClass("fas fa-sync");
            }

            /// <summary>
            /// Enables the Refresh-Button in the Command-Toolbar of the table
            /// </summary>
            /// <typeparam name="TModel">the model of the table</typeparam>
            /// <param name="target">the command-factory for the given table</param>
            /// <returns></returns>
            public static GridToolBarCustomCommandBuilder SyncTable<TModel>(this GridToolBarCommandFactory<TModel> target) where TModel : class
            {
                return target.Custom().Name(CustomActionHelper.RandomName("saveChanges")).Text("\u200B").HtmlAttributes(new Dictionary<string, object> { { "onclick", "ITVenture.Tools.TableHelper.syncDataGrid(event)" }, { "class", "itv-tool-button itv-fa-tbx" }, { "title", TextsAndMessagesHelper.IWCN_KX_STB_Caption } }).IconClass("fas fa-floppy-disks");
            }

        /// <summary>
        /// Displays an inline-checkbox for instant-editing of simple on-off-attributes on a table
        /// </summary>
        /// <typeparam name="TModel">the model of the current table</typeparam>
        /// <param name="target">the column-factory for the current table</param>
        /// <param name="expression">the expression that identifies the bound column</param>
        /// <param name="pkName">the primary-key name of the target-model</param>
        /// <param name="customChangeHandler">a custom changed-handler</param>
        /// <param name="readOnly">indicates whether to put this on read-only</param>
        /// <returns>the column-builder for the bound column</returns>
        public static GridBoundColumnBuilder<TModel> InlineCheckbox<TModel>(this GridColumnFactory<TModel> target, Expression<Func<TModel, bool>> expression, string pkName, string customChangeHandler = null, bool readOnly = false)
                where TModel : class
            {
                var retVal = target.Bound(expression);
                string columnName = retVal.Column.Member;
                string idRaw = CustomActionHelper.RandomName(retVal.Column.Member);
                string id = $"{idRaw}#={pkName}#";
                string template = $@"<input id='{id}' class='itv-icb-marker' data-col-name='{columnName}' #if ({columnName}) {{ # checked='checked' # }} # type='checkbox' style='display:none;' />
    #{{
        ITVenture.Tools.KendoExtensions.InitInlineCheckbox('{pkName}','{idRaw}',{(readOnly ? "false" : "true")},{customChangeHandler}).apply(arguments[0]);
    }}#";
                /*if (clientTemplateReady)
                {
                    template = template.ToClientTemplate();
                }*/

                retVal.ClientTemplate(template);
                return retVal;
            }

            /// <summary>
            /// Displays a Multi-Select box for editing / displaying data
            /// </summary>
            /// <typeparam name="TModel">the target-model of the list</typeparam>
            /// <typeparam name="TValue">the value type of the bound field</typeparam>
            /// <param name="target">the column-factory of the list</param>
            /// <param name="expression">the expression targeting an enumerable field</param>
            /// <param name="repoName">the name of the repository that provides the fk-data</param>
            /// <param name="tableName">the name of the source table</param>
            /// <param name="pkName">the name of the primary-key</param>
            /// <param name="placeholder">a placeholder text that is being displayed when nothing is selected</param>
            /// <param name="alwaysActive">Indicates whether to keep the display-template editable, so that this column can be edited without entering edit-mode</param>
            /// <param name="customInlineChangedHandler">defines a custom handler that will be called, when an item was selected or deleted. If no value is provided, the default handler will be used and the grid will be synced on every modification action of the item.</param>
            /// <returns>the ColumnBuilder object for the bound field</returns>
            public static GridBoundColumnBuilder<TModel> MultiSelect<TModel, TValue>(this GridColumnFactory<TModel> target, Expression<Func<TModel, TValue>> expression, string repoName, string tableName, string pkName, string placeholder = "", bool alwaysActive = false, string customInlineChangedHandler = null, bool autoSaveOnChange = true) where TModel : class
                where TValue : IEnumerable
            {
                var retVal = target.Bound(expression);
                var route = target.Container.HtmlHelper.ViewContext.HttpContext.Request.RouteValues;
                string area = null;
                string changedHandler = customInlineChangedHandler ?? $"ITVenture.Tools.TableHelper.defaultMultiSelectCallback({(autoSaveOnChange?"true":"false")})";
                if (route.ContainsKey("area"))
                {
                    area = (string) route["area"];
                }

                var urlFormat = target.Container.HtmlHelper.ViewContext.HttpContext.RequestServices.GetService<IUrlFormat>();
            var url = new StringBuilder();
            url.Append(urlFormat != null ?
                urlFormat.FormatUrl($"[SlashPermissionScope]{(!string.IsNullOrEmpty(area) ? $"/{area}" : "")}") :
                $"{(!string.IsNullOrEmpty(area) ? $"/{area}" : "")}");
            url.Append($"/ForeignKey/{repoName}/{tableName}");
            string columnName = $"{retVal.Column.Member}";
                string displayTemplateName = $"{columnName}#={pkName}#";
                string idRaw = CustomActionHelper.RandomName(retVal.Column.Member);
                string id = $"{idRaw}#={pkName}#";
                var clientTemplate = @$"<select {(!alwaysActive?@"disabled=""disabled""":"")} id=""{id}"" multiple=""multiple"" style='display:none;' name=""{displayTemplateName}"" data-col-name='{columnName}'></select>
    #{{
        ITVenture.Tools.KendoExtensions.InitMultiSelect('{retVal.Column.Member}','{pkName}','{idRaw}',{(alwaysActive ? "true" : "false")},'{placeholder}','{url}',{changedHandler}).apply(arguments[0]);
    }}#";
                retVal.EditorTemplateName("MultiSelect").ClientTemplate(clientTemplate)
                    .EditorViewData(new Dictionary<string, object>
                    {
                        {$"{columnName}_RepoName", repoName},
                        {$"{columnName}_TableName", tableName},
                        {$"{columnName}_Area", area},
                        {$"{columnName}_Placeholder", placeholder}
                    });

                return retVal;
            }

            public static GridBoundColumnBuilder<TModel> InfoBubble<TModel, TValue>(this GridColumnFactory<TModel> target, Expression<Func<TModel, TValue>> expression, string pkName, string iconClass, string iconStyle, string entityName, string backgroundColor, string shadowColor, string contentProperty = null) where TModel : class
            {
                return InfoBubble<TModel, TValue>(target, expression, pkName, iconClass, iconStyle, entityName, $"background-color:{backgroundColor};text-shadow:0 -1px 0 {shadowColor};color:white", contentProperty);
            }

            public static GridBoundColumnBuilder<TModel> InfoBubble<TModel, TValue>(this GridColumnFactory<TModel> target,
                Expression<Func<TModel, TValue>> expression,
                string pkName,
                string iconClass,
                string iconStyle,
                string entityName,
                string bubbleStyle,
                string contentProperty = null) where TModel : class
            {
                var retVal = target.Bound(expression);
                var name = contentProperty ?? retVal.Column.Member;
                var template = $@"<span id='bc{pkName}{entityName}_#={pkName}#' contentProperty='{name}' bubbleStyle=""{bubbleStyle}""><span class=""{iconClass}"" style=""{iconStyle}""></span></span>
    #{{
        var nameId = {pkName}.toString();
        var name = ""bc{pkName}{entityName}_"".concat(nameId);
        $(function(){{
            ITVenture.Tools.InlineBubble.BubblePopupFor(name);
        }});
    }}#";
                retVal.ClientTemplate(template);
                retVal.EditorTemplateName("EmptyEditor");
                return retVal;
            }

            public static GridBoundColumnBuilder<TModel> InfoBubble<TModel, TValue>(this GridColumnFactory<TModel> target,
                Expression<Func<TModel, TValue>> expression,
                string pkName,
                string entityName) where TModel : class
            {
                var retVal = target.Bound(expression);
                var name = retVal.Column.Member;
                string template =
                    $@"<span id='xc{pkName}{entityName}_#={pkName}#' diagProperty='{name}'><span></span></span>
    #{{
        var nameId = {pkName}.toString();
        var name = ""xc{pkName}{entityName}_"".concat(nameId);
        $(function(){{
             ITVenture.Tools.InlineBubble.DiagPopupFor(name);
        }});
    }}#";

                retVal.ClientTemplate(template);
                retVal.EditorTemplateName("EmptyEditor");
                return retVal;
            }

            public static GridBoundColumnBuilder<TModel> DeciderInfo<TModel, TValue>(this GridColumnFactory<TModel> target,
                Expression<Func<TModel, TValue>> expression,
                string pkName,
                string entityName) where TModel : class
            {
                var retVal = target.Bound(expression);
                var name = retVal.Column.Member;
                string template =
                    $@"<span id='dc{pkName}{entityName}_#={pkName}#' diagProperty='{name}'><span></span></span>
    #{{
        var nameId = {pkName}.toString();
        var name = ""dc{pkName}{entityName}_"".concat(nameId);
        $(function(){{
            ITVenture.Tools.InlineDiagnostics.DiagPopupFor(name);
        }});
    }}#";

                retVal.ClientTemplate(template);
                retVal.EditorTemplateName("EmptyEditor");
                return retVal;
            }

            public static GridCustomActionCommandBuilder<TModel> PopupConfirmDelete<TModel>(
                this GridActionCommandFactory<TModel> target) where TModel : class
            {
                return
                    target.Custom(CustomActionHelper.RandomName("Delete"))
                        .Click("ITVenture.Tools.TableHelper.confirmDelete")
                        .HtmlAttributes(new {@class = "itv-grid-button", title = TextsAndMessagesHelper.IWCN_KX_PCD_Caption})
                        /*.IconClass("fa fa-trash")*/
                        .IconClass("k-icon k-i-delete")
                        .Text("\u200B");
            }

            public static GridBoundColumnBuilder<TModel> FileDownload<TModel, TValue>(this GridColumnFactory<TModel> target,
                Expression<Func<TModel, TValue>> expression,
                string displayText,
                string handlerModule,
                string uploadReason,
                string emptyText = null,
                string originalNameColumn = null,
                bool setOriginalOnlyIfNull = false,
                bool forceDownload = false,
                string uploadHintHandler = null,
                string customUploadCallback = null) where TModel : class
            {
                var retVal = target.Bound(expression);
                bool preserveOriginalName = originalNameColumn != null;
                string columnName = retVal.Column.Member;
                var urlHelper = new UrlHelper(target.Container.ViewContext);
                string template = $@"#if (typeof {columnName} === ""string"" && {columnName}.trim() !== """") {{#
        <a href='{(forceDownload ? $"{urlHelper.Content($"~/File/#={columnName}#")}" : "\\#")}' onclick='ITVenture.Tools.Uploader.showFile(ITVenture.Helpers.ResolveUrl(""~/File/#={columnName}#"")); return false;'>{displayText}</a>
    #}} else {{#
        <span>{emptyText}</span>
    #}}#";
                retVal.ClientTemplate(template).EditorTemplateName("Uploader").Filterable(false);
                target.Container.ViewContext.ViewData[$"{retVal.Column.Member}_uploadModule"] = handlerModule;
                target.Container.ViewContext.ViewData[$"{retVal.Column.Member}_uploadReason"] = uploadReason;
                target.Container.ViewContext.ViewData[$"{retVal.Column.Member}_preserveOriginal"] = preserveOriginalName;
                target.Container.ViewContext.ViewData[$"{retVal.Column.Member}_originalNameColumn"] = originalNameColumn;
                target.Container.ViewContext.ViewData[$"{retVal.Column.Member}_UpdateNullOriginalOnly"] = setOriginalOnlyIfNull;
                target.Container.ViewContext.ViewData[$"{retVal.Column.Member}_uploadHint"] = uploadHintHandler;
                target.Container.ViewContext.ViewData[$"{retVal.Column.Member}_customCallback"] = customUploadCallback;
                return retVal;
            }

            /// <summary>
            /// Creates a foreign-key column in a kendo-grid
            /// </summary>
            /// <param name="target">the target-object for which to create the foreign-key column</param>
            /// <param name="tableName">the name of the table that contains the foreignkey-data</param>
            /// <param name="pkName">the pk-name of the target-entity of the current grid</param>
            /// <param name="minSearchLength">the minimum-search length</param>
            /// <param name="nameBuffer">a name-buffer object that is requried if there are cascading foreign-keys</param>
            /// <param name="cascadeFrom">the cascade-parent of the current foreign-key</param>
            /// <param name="columnName">the name of the foreign-key column</param>
            /// <param name="repoName">the name of the repository from where to read the data</param>
            /// <param name="filterable">indicates whether this column must be made filterable</param>
            /// <param name="type">the foreign-key type</param>
            /// <returns>A GridColumnBuilder object representing this foreign-key</returns>
            public static GridBoundColumnBuilder<object> AjaxFk(this GridColumnFactory<object> target, Type type, string columnName, string repoName, string tableName, string pkName, int minSearchLength = 0, Dictionary<string, string> nameBuffer = null, string cascadeFrom = null, bool filterable = false, string emptyLabel = "", string dataCallback = "")
            {
                var retVal = target.Bound( type, columnName);
                //string columnName = retVal.Column.Member;
                string id = CustomActionHelper.RandomName($"FK_{repoName}_{tableName}_{columnName}");
                nameBuffer?.Add(retVal.Column.Member, id);
                if (cascadeFrom != null && (nameBuffer?.ContainsKey(cascadeFrom) ?? false))
                {
                    cascadeFrom = nameBuffer[cascadeFrom];
                }

                var route = target.Container.HtmlHelper.ViewContext.HttpContext.Request.RouteValues;
                string area = null;
                if (route.ContainsKey("area"))
                {
                    area = (string) route["area"];
                }
                
                string filterScriptName = null;
                string filterScriptBody = null;
                if (filterable)
                {
                    filterScriptName = CreateFilterScriptFor(repoName, tableName, columnName, area, minSearchLength, dataCallback, out filterScriptBody);
                }

                string template =
                    $@"<span id='fk{repoName}_{tableName}_{columnName}_#=ITVenture.Tools.ListCallbackHelper.normalizeFkValue({columnName})#_#=ITVenture.Tools.ListCallbackHelper.normalizeFkValue({pkName})#' ></span>
    #{{
        var $$tmp = function(colVal,pkVal,data){{
            var tmpPk = ITVenture.Tools.ListCallbackHelper.normalizeFkValue(pkVal);
            var tmpFk = ITVenture.Tools.ListCallbackHelper.normalizeFkValue(colVal);
            var prnt = null;
            try{{
                prnt = data.uid;
            }}catch{{}}
            var st = ""fk{repoName}_{tableName}_{columnName}_"".concat(tmpFk).concat(""_"").concat(tmpPk);
            var colValue = colVal;
            $(function(){{
                ITVenture.Tools.ListCallbackHelper.ShowPkValue(""{repoName}"",""{tableName}"",colValue,st, ""{emptyLabel}"", ""{area}"", prnt);
            }});
        }}({columnName},{pkName},data);
    }}#";

                target.Container.ViewContext.ViewData[$"{retVal.Column.Member}_RepoName"] = repoName;
                target.Container.ViewContext.ViewData[$"{retVal.Column.Member}_TableName"] = tableName;
                target.Container.ViewContext.ViewData[$"{retVal.Column.Member}_ID"] = id;
                target.Container.ViewContext.ViewData[$"{retVal.Column.Member}_minLength"] = minSearchLength;
                target.Container.ViewContext.ViewData[$"{retVal.Column.Member}_CascadeTarget"] = cascadeFrom;
                target.Container.ViewContext.ViewData[$"{retVal.Column.Member}_DataCallback"] = dataCallback;
                target.Container.ViewContext.ViewData[$"{retVal.Column.Member}_Area"] = area;
                retVal.ClientTemplate(template);
                if (filterable)
                {
                    retVal.Filterable(f => f.UI($"ITVenture.Tools.ListCallbackHelper.getFilterScript('{filterScriptName}')"));
                }
                
                retVal.EditorTemplateName("ApiForeignKey");
                target.Container.HtmlHelper.Raw($@"<script>
                {filterScriptBody}
    </script>").WriteTo(target.Container.ViewContext.Writer, HtmlEncoder.Default);
                return retVal;
            }

            /// <summary>
            /// Credates a foreign-key column in a kendo-grid
            /// </summary>
            /// <typeparam name="TModel">the model-type</typeparam>
            /// <typeparam name="TValue">the value-type</typeparam>
            /// <param name="target">the target-object for which to create the foreign-key column</param>
            /// <param name="expression">the expression to apply on the model</param>
            /// <param name="tableName">the name of the table that contains the foreignkey-data</param>
            /// <param name="pkName">the pk-name of the target-entity of the current grid</param>
            /// <param name="minSearchLength">the minimum-search length</param>
            /// <param name="nameBuffer">a name-buffer object that is requried if there are cascading foreign-keys</param>
            /// <param name="cascadeFrom">the cascade-parent of the current foreign-key</param>
            /// <param name="repoName">the name of the repository from where to read the data</param>
            /// <param name="filterable">indicates whether this column must be made filterable</param>
            /// <returns>A GridColumnBuilder object representing this foreign-key</returns>
            public static GridBoundColumnBuilder<TModel> AjaxFk<TModel, TValue>(this GridColumnFactory<TModel> target, Expression<Func<TModel, TValue>> expression, string repoName, string tableName, string pkName, object customFilterData, int minSearchLength = 0, Dictionary<string, string> nameBuffer = null, string cascadeFrom = null, bool filterable = false, string emptyLabel = "") where TModel : class
            {
                var retVal = target.Bound(expression);
                string columnName = retVal.Column.Member;
                string id = CustomActionHelper.RandomName($"FK_{repoName}_{tableName}_{columnName}");
                nameBuffer?.Add(retVal.Column.Member, id);
                if (cascadeFrom != null && (nameBuffer?.ContainsKey(cascadeFrom) ?? false))
                {
                    cascadeFrom = nameBuffer[cascadeFrom];
                }

                var route = target.Container.HtmlHelper.ViewContext.HttpContext.Request.RouteValues;
                string area = null;
                if (route.ContainsKey("area"))
                {
                    area = (string) route["area"];
                }
                
                string filterScriptName = null;
                string filterScriptBody = null;
                string dataCallback = null;
                string dataCallbackBody = null;
                if (customFilterData != null)
                {
                    dataCallback = $"ITVenture.Tools.ListCallbackHelper.dataCallbacks.{HtmlExtensions.CreateDataScriptFor(repoName, tableName, columnName, customFilterData, out dataCallbackBody)}";
                }
                if (filterable)
                {
                    filterScriptName = CreateFilterScriptFor(repoName, tableName, columnName, area, minSearchLength, dataCallback, out filterScriptBody);
                }

                string template =
                    $@"<span id='fk{repoName}_{tableName}_{columnName}_#=ITVenture.Tools.ListCallbackHelper.normalizeFkValue({columnName})#_#=ITVenture.Tools.ListCallbackHelper.normalizeFkValue({pkName})#' ></span>
    #{{
        var $$tmp = function(colVal,pkVal,data){{
            var tmpFk = ITVenture.Tools.ListCallbackHelper.normalizeFkValue(colVal);
            var tmpPk = ITVenture.Tools.ListCallbackHelper.normalizeFkValue(pkVal);
            var prnt = null;
            try{{
                prnt = data.uid;
            }}catch{{}}
            var st = ""fk{repoName}_{tableName}_{columnName}_"".concat(tmpFk).concat(""_"").concat(tmpPk);
            var colValue = colVal;
            $(function(){{
                ITVenture.Tools.ListCallbackHelper.ShowPkValue(""{repoName}"",""{tableName}"",colValue,st, ""{emptyLabel}"", ""{area}"", prnt);
            }});
        }}({columnName},{pkName},data);
    }}#";
                target.Container.ViewContext.ViewData[$"{retVal.Column.Member}_RepoName"] = repoName;
                target.Container.ViewContext.ViewData[$"{retVal.Column.Member}_TableName"] = tableName;
                target.Container.ViewContext.ViewData[$"{retVal.Column.Member}_ID"] = id;
                target.Container.ViewContext.ViewData[$"{retVal.Column.Member}_minLength"] = minSearchLength;
                target.Container.ViewContext.ViewData[$"{retVal.Column.Member}_CascadeTarget"] = cascadeFrom;
                target.Container.ViewContext.ViewData[$"{retVal.Column.Member}_DataCallback"] = dataCallback;
                target.Container.ViewContext.ViewData[$"{retVal.Column.Member}_Area"] = area;
                retVal.ClientTemplate(template);
                if (filterable)
                {
                    retVal.Filterable(f => f.UI($"ITVenture.Tools.ListCallbackHelper.getFilterScript('{filterScriptName}')"));
                }
                
                retVal.EditorTemplateName("ApiForeignKey");
                target.Container.HtmlHelper.Raw($@"<script>
                {dataCallbackBody}
                {filterScriptBody}
    </script>").WriteTo(target.Container.ViewContext.Writer, HtmlEncoder.Default);
                return retVal;
            }

            /// <summary>
            /// Credates a foreign-key column in a kendo-grid
            /// </summary>
            /// <typeparam name="TModel">the model-type</typeparam>
            /// <typeparam name="TValue">the value-type</typeparam>
            /// <param name="target">the target-object for which to create the foreign-key column</param>
            /// <param name="expression">the expression to apply on the model</param>
            /// <param name="tableName">the name of the table that contains the foreignkey-data</param>
            /// <param name="pkName">the pk-name of the target-entity of the current grid</param>
            /// <param name="minSearchLength">the minimum-search length</param>
            /// <param name="nameBuffer">a name-buffer object that is requried if there are cascading foreign-keys</param>
            /// <param name="cascadeFrom">the cascade-parent of the current foreign-key</param>
            /// <param name="repoName">the name of the repository from where to read the data</param>
            /// <param name="filterable">indicates whether this column must be made filterable</param>
            /// <returns>A GridColumnBuilder object representing this foreign-key</returns>
            public static GridBoundColumnBuilder<TModel> AjaxFk<TModel, TValue>(this GridColumnFactory<TModel> target, Expression<Func<TModel, TValue>> expression, string repoName, string tableName, string pkName, int minSearchLength = 0, Dictionary<string, string> nameBuffer = null, string cascadeFrom = null, bool filterable = false, string emptyLabel = "", string dataCallback = "") where TModel : class
            {
                var retVal = target.Bound(expression);
                string columnName = retVal.Column.Member;
                string id = CustomActionHelper.RandomName($"FK_{repoName}_{tableName}_{columnName}");
                nameBuffer?.Add(retVal.Column.Member, id);
                if (cascadeFrom != null && (nameBuffer?.ContainsKey(cascadeFrom) ?? false))
                {
                    cascadeFrom = nameBuffer[cascadeFrom];
                }

                var route = target.Container.HtmlHelper.ViewContext.HttpContext.Request.RouteValues;
                string area = null;
                if (route.ContainsKey("area"))
                {
                    area = (string) route["area"];
                }
                
                string filterScriptName = null;
                string filterScriptBody = null;
                if (filterable)
                {
                    filterScriptName = CreateFilterScriptFor(repoName, tableName, columnName, area, minSearchLength, dataCallback, out filterScriptBody);
                }

                string template =
                    $@"<span id='fk{repoName}_{tableName}_{columnName}_#=ITVenture.Tools.ListCallbackHelper.normalizeFkValue({columnName})#_#=ITVenture.Tools.ListCallbackHelper.normalizeFkValue({pkName})#' ></span>
    #{{
        var $$tmp = function(colVal,pkVal,data){{
            var tmpPk = ITVenture.Tools.ListCallbackHelper.normalizeFkValue(pkVal);
            var tmpFk = ITVenture.Tools.ListCallbackHelper.normalizeFkValue(colVal);
            var prnt = null;
            try{{
                prnt = data.uid;
            }}catch{{}}
            var st = ""fk{repoName}_{tableName}_{columnName}_"".concat(tmpFk).concat(""_"").concat(tmpPk);
            var colValue = colVal;
            $(function(){{
                ITVenture.Tools.ListCallbackHelper.ShowPkValue(""{repoName}"",""{tableName}"",colValue,st, ""{emptyLabel}"", ""{area}"", prnt);
                {filterScriptBody}
            }});
        }}({columnName},{pkName},data);
    }}#";

                target.Container.ViewContext.ViewData[$"{retVal.Column.Member}_RepoName"] = repoName;
                target.Container.ViewContext.ViewData[$"{retVal.Column.Member}_TableName"] = tableName;
                target.Container.ViewContext.ViewData[$"{retVal.Column.Member}_ID"] = id;
                target.Container.ViewContext.ViewData[$"{retVal.Column.Member}_minLength"] = minSearchLength;
                target.Container.ViewContext.ViewData[$"{retVal.Column.Member}_CascadeTarget"] = cascadeFrom;
                target.Container.ViewContext.ViewData[$"{retVal.Column.Member}_DataCallback"] = dataCallback;
                target.Container.ViewContext.ViewData[$"{retVal.Column.Member}_Area"] = area;
                retVal.ClientTemplate(template);
                if (filterable)
                {
                    retVal.Filterable(f => f.UI($"ITVenture.Tools.ListCallbackHelper.getFilterScript('{filterScriptName}')"));
                }
                
                retVal.EditorTemplateName("ApiForeignKey");
                target.Container.HtmlHelper.Raw($@"<script>
                {filterScriptBody}
    </script>").WriteTo(target.Container.ViewContext.Writer, HtmlEncoder.Default);
                return retVal;
            }

            private static string CreateFilterScriptFor(string repoName, string tableName, string memberName, string area, int minSearchLength, string dataCallback, out string filter)
            {
                string filterFunction = CustomActionHelper.RandomName($"filterFx_{repoName}_{tableName}_{memberName}");
                filter = $@"ITVenture.Tools.ListCallbackHelper.filterScripts.{filterFunction} = function(element) {{
                $(element).kendoDropDownList({{
                    dataSource: {{
                        type: ""aspnetmvc-ajax"",
                        transport:{{
                            read: {{
                                url:ITVenture.Helpers.ResolveUrl(""~{(!string.IsNullOrEmpty(area)?$"/{area}":"")}/ForeignKey/{repoName}/{tableName}"")
                            }},
                            prefix:""""{(!string.IsNullOrEmpty(dataCallback)?$@",
                            data: ""{dataCallback}""":"")}
                        }},
                        serverFiltering: true,
                        schema:{{
                            data: ""Data"",
                            total: ""Total"",
                            errors: ""Errors""
                        }}
                    }},
                    dataValueField: ""Key"",
                    dataTextField: ""Label"",
                    optionLabel: ""--Select Value--"",
                    minLength: {minSearchLength},
                    autoBind: true,
                    filter: ""contains"",
                    valuePrimitive: true
                }});
            }};";

                return filterFunction;
            }
        }
    }
