ITVenture.Tools.EventBus = {
    Event: function (name) {
        this.name = name;
        this.callbacks = [];
    },
    Reactor: function () {
        this.events = {};
    },
    EventArgs: function (target) {
        this.target = target;
        this.defaultPrevented = false;
    },
    InitEventBus: function () {
        ITVenture.Tools.EventBus.Event.prototype.registerCallback = function (callback) {
            this.callbacks.push(callback);
        };

        ITVenture.Tools.EventBus.Reactor.prototype.registerEvent = function (eventName) {
            if (!this.events.hasOwnProperty(eventName)) {
                var event = new ITVenture.Tools.EventBus.Event(eventName);
                this.events[eventName] = event;
            }
        };

        ITVenture.Tools.EventBus.Reactor.prototype.dispatchEvent = function (eventName, eventArgs) {
            var ignoreCancel = !eventArgs.hasOwnProperty("defaultPrevented");
            var retVal = true;
            this.events[eventName].callbacks.forEach(function (callback) {
                if (ignoreCancel || !eventArgs.defaultPrevented) {
                    callback(eventArgs);
                }
                else {
                    retVal = false;
                }
            });

            return retVal;
        };

        ITVenture.Tools.EventBus.Reactor.prototype.addEventListener = function (eventName, callback) {
            this.events[eventName].registerCallback(callback);
        }

        ITVenture.Tools.EventBus.EventArgs.prototype.preventDefault = function () {
            this.defaultPrevented = true;
        }
    }
};

ITVenture.Tools.EventBus.InitEventBus();