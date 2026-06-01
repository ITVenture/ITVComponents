ITVenture.Tools.RenderingDeferrer = {
    handlers: {},
    statusObjects: {},
    nextId: 0,
    maxRnd:10000,
    registerHandler: function (group, handler) {
        if (!ITVenture.Tools.RenderingDeferrer.handlers.hasOwnProperty(group)) {
            ITVenture.Tools.RenderingDeferrer.handlers[group] = [];
        }

        ITVenture.Tools.RenderingDeferrer.handlers[group].push(handler);
    },
    pushHandlerGroup: function (group) {
        if (ITVenture.Tools.RenderingDeferrer.handlers.hasOwnProperty(group)) {
            var gp = ITVenture.Tools.RenderingDeferrer.handlers[group];
            delete ITVenture.Tools.RenderingDeferrer.handlers[group];
            for (var i = 0; i < gp.length; i++) {
                var hd = gp[i];
                hd.invokeRender();
            }
        }
    },
    pushHandlerGroupAsync: async function (group) {
        if (ITVenture.Tools.RenderingDeferrer.handlers.hasOwnProperty(group)) {
            var gp = ITVenture.Tools.RenderingDeferrer.handlers[group];
            delete ITVenture.Tools.RenderingDeferrer.handlers[group];
            for (var i = 0; i < gp.length; i++) {
                var hd = gp[i];
                await hd.invokeRender();
            }
        }
    },
    resetGroups: function () {
        for (var name in ITVenture.Tools.RenderingDeferrer.handlers) {
            if (ITVenture.Tools.RenderingDeferrer.handlers.hasOwnProperty(name)) {
                delete ITVenture.Tools.RenderingDeferrer.handlers[name];
            }
        }
    },
    registerStatusObject: function (targetObj) {
        var objectName = `${ITVenture.Tools.RenderingDeferrer.incrementObjectId()}_REGOBJ_${Math.floor(Math.random() * ITVenture.Tools.RenderingDeferrer.maxRnd)}`;
        ITVenture.Tools.RenderingDeferrer.statusObjects[objectName] = targetObj;
        return objectName;
    },
    getStatusObject: function (objectId) {
        var retVal = null;
        if (ITVenture.Tools.RenderingDeferrer.statusObjects.hasOwnProperty(objectId)) {
            retVal = ITVenture.Tools.RenderingDeferrer.statusObjects[objectId];
            delete ITVenture.Tools.RenderingDeferrer.statusObjects[objectId];
        }

        return retVal;
    },
    incrementObjectId: function () {
        var id = ITVenture.Tools.RenderingDeferrer.nextId++;
        return id;
    }
}