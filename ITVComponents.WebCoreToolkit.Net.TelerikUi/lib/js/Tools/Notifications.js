ITVenture.Tools.Notifications = {
    subscriptions: {},
    notifyHub: null,
    On: function (name, data, location) {
        var target = null;
        if (!ITVenture.Tools.Notifications.subscriptions.hasOwnProperty(name)) {
            ITVenture.Tools.Notifications.subscriptions[name] = [];
        }

        target = ITVenture.Tools.Notifications.subscriptions[name];
        if (typeof (data) === "function") {
            target.push(data);
        }
        else {
            var ev = {
                location: location,
                data: data,
                stop: false
            };
            ITVenture.Tools.Notifications.InvokeEventsAsync(name, ev, target);
        }
    },
    InvokeEventsAsync: async function (name, ev, target) {
        
        for (var i = 0; i < target.length; i++) {
            var fx = target[i];
            try {
                await fx.apply(document, [name, ev]);
                if (ev.stop) {
                    break;
                }
            }
            catch (e) {
                console.log(e);
            }
        }
    },
    InitHub: function () {
        var connection = ITVenture.Tools.Notifications.notifyHub = new signalR.HubConnectionBuilder().withUrl(ITVenture.Helpers.ResolveUrl("~/Util/GlobalNotificationHub")).withAutomaticReconnect().build();

        connection.on("Notify", ITVenture.Tools.Notifications.Notify);
        ITVenture.Tools.Notifications.On("ModuleData", ITVenture.Tools.Notifications.ModuleData);
        connection.start().catch(function (err) {
            return console.error(err.toString());
        });
    },
    Notify: function (location, topic, data) {
        ITVenture.Tools.Notifications.On(topic, data, location);
    },
    ModuleData: async function (topic, eventData) {
        var module = eventData.location;
        var dgQuery = "counter4".concat(module.replaceAll("/", "_"));
        var finalModule = ITVenture.Helpers.ResolveUrl("~".concat(module)).toLowerCase();;
        var moduleCounter = null;
        if (document.location.pathname.toLowerCase() != finalModule) {
            try {
                var w = await ITVenture.Ajax.ajaxGet("~/Diagnostics/".concat(dgQuery), "json");
                if (w != null && w.length != 0) {
                    moduleCounter = w[0].toString();
                }
            }
            catch (e) {
                console.log(e);
            }

            if (moduleCounter != null) {
                ITVenture.Tools.Notifications.On("NavCounter", moduleCounter, finalModule);
            }
        }
        else {
            if (typeof (eventData.data.listName) === "string") {
                ITVenture.Tools.Notifications.On("ListUpdate4".concat(eventData.data.listName), eventData.data, finalModule);
            }
            else if (typeof (eventData.data.target) === "string") {
                ITVenture.Tools.Notifications.On(eventData.data.target, eventData.data, finalModule);
            }
            else {
                ITVenture.Tools.Notifications.On("GenericEvent", eventData.data, finalModule);
            }
        }
    },
    DefaultListUpdate: function (name, eventData) {
        $("#".concat(eventData.data.listName)).data("kendoGrid").dataSource.read();
    }
};