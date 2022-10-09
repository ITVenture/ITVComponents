ITVenture.Tools.ModuleConfiguratorHandler = {
    handlers: {},
    modules: {},
    ReadHandlerConfig: async function (name) {
        var data = await ITVenture.Ajax.ajaxGet("~/Util/FeatureModule/".concat(name), "json");
        if (!ITVenture.Tools.ModuleConfiguratorHandler.modules.hasOwnProperty(name)) {
            ITVenture.Tools.ModuleConfiguratorHandler.modules[name] = []
        }
        else {
            ITVenture.Tools.ModuleConfiguratorHandler.modules.length = 0;
        }
        for (var prop in data) {
            var handlerName = name.concat("_").concat(prop);
            if (data.hasOwnProperty(prop) && ITVenture.Tools.ModuleConfiguratorHandler.handlers.hasOwnProperty(handlername)) {
                ITVenture.Tools.ModuleConfiguratorHandler.handlers[handlerName].SetValue(data[prop]);
                ITVenture.Tools.ModuleConfiguratorHandler.modules[name].push(prop);
            }
        }
    },
    UpdateHandlerConfig: async function (name) {
        var postData = {};
        if (ITVenture.Tools.ModuleConfiguratorHandler.modules.hasOwnProperty(name)) {
            for (var i = 0; i < ITVenture.Tools.ModuleConfiguratorHandler.modules[name].length; i++) {
                var prop = ITVenture.Tools.ModuleConfiguratorHandler.modules[name][i];
                var handlerName = name.concat("_").concat(prop);
                if (ITVenture.Tools.ModuleConfiguratorHandler.handlers.hasOwnProperty(handlername)) {
                    var handler = ITVenture.Tools.ModuleConfiguratorHandler.handlers[handlerName];
                    postData[prop] = JSON.stringify(handler.GetValue());
                }
            }
        }

        await ITVenture.Ajax.ajaxFormPost("~Util/FeatureModule/".concat(name), JSON.stringify(postData), "text", "application/json");
        await ITVenture.Tools.ModuleConfiguratorHandler.ReadHandlerConfig(name);
    }
}