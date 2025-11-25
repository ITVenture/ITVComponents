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
            var ignoreCancel = typeof (eventArgs) === "undefined" || eventArgs == null || !eventArgs.hasOwnProperty("defaultPrevented");
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

        ITVenture.Tools.EventBus.MiniAwaitable.prototype.then = function (success, fail) {
            if (!this.completed) {
                if (typeof (success) === "function") {
                    this.callbacks.addEventListener("completed", success);
                }

                if (typeof (fail) === "function") {
                    this.callbacks.addEventListener("error", fail);
                }
            }
            else {
                if (!this.failed) {
                    success(this.result);
                }
                else {
                    fail(this.result);
                }
            }
        }

        ITVenture.Tools.EventBus.MiniAwaitable.prototype.resolve = function (result) {
            this.completed = true;
            this.result = result;
            this.callbacks.dispatchEvent("completed", result);
        }

        ITVenture.Tools.EventBus.MiniAwaitable.prototype.reject = function (error) {
            this.completed = true;
            this.failed = true;
            this.result = error;
            this.callbacks.dispatchEvent("error", error);
        }
    },
    MiniAwaitable: function () {
        this.callbacks = new ITVenture.Tools.EventBus.Reactor();
        this.callbacks.registerEvent("completed");
        this.callbacks.registerEvent("error");
        this.completed = false;
        this.failed = false;
        this.result = null;
    }
};

ITVenture.Tools.EventBus.InitEventBus();