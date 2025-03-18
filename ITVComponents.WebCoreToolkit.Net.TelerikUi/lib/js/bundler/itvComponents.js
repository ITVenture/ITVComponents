import * as $ from 'jquery';
import * as kendo from 'kendo';
import * as Dropzone from 'dropzone';
window.jQuery = $;
window.$ = $;
window.jquery = $;
/*require("jquery");
window.$ = window.jQuery = $;
*/

require("../ItvGlobalScript");
require("../Tools/AssemblyAnalyzerDataSource");
require("../Tools/DashboardWidgets");
require("../Tools/Dialog");
require("../Tools/DynamicData");
require("../Tools/EventBus");
require("../Tools/HtmlHelper");
require("../Tools/InlineBubbleHelper");
require("../Tools/InlineCheckboxHelper");
require("../Tools/InlineDiagHelper");
require("../Tools/JsonConfigExchange");
require("../Tools/KendoExtensionHelper");
require("../Tools/ListCallbackHelper");
require("../Tools/ModuleConfiguratorHandler");
require("../Tools/Notifications");
require("../Tools/RowReordering");
require("../Tools/TableHelper");
require("../Tools/Uploader");
require("../Tools/DialogExtensions/AssemblyAnalyzerPopup");
require("../Tools/RenderingDeferrer");
require("../Lang/DE");
require("../Lang/FR");
require("../Lang/IT");
require("../LocalViewScripts/AssemblyDiagnostics")