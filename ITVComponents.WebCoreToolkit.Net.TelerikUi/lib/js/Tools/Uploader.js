Dropzone.autoDiscover = false;
ITVenture.Tools.Uploader = {
    dropzoneConfigured: false,
    configureDropzone: function () {
        Dropzone.prototype.defaultOptions.dictDefaultMessage = ITVenture.Text.getText("Drop files here to upload");
        Dropzone.prototype.defaultOptions.dictFallbackMessage = ITVenture.Text.getText("Your browser does not support drag'n'drop file uploads.");
        Dropzone.prototype.defaultOptions.dictFallbackText = ITVenture.Text.getText("Please use the fallback form below to upload your files like in the olden days.");
        Dropzone.prototype.defaultOptions.dictFileTooBig = ITVenture.Text.getText("File is too big.");
        Dropzone.prototype.defaultOptions.dictInvalidFileType = ITVenture.Text.getText("You can't upload files of this type.");
        Dropzone.prototype.defaultOptions.dictResponseError = ITVenture.Text.getText("Server responded with code");
        Dropzone.prototype.defaultOptions.dictCancelUpload = ITVenture.Text.getText("Cancel upload");
        Dropzone.prototype.defaultOptions.dictCancelUploadConfirmation = ITVenture.Text.getText("Are you sure you want to cancel this upload?");
        Dropzone.prototype.defaultOptions.dictRemoveFile = ITVenture.Text.getText("Remove file");
        Dropzone.prototype.defaultOptions.dictMaxFilesExceeded = ITVenture.Text.getText("You can not upload any more files.");
    },
    prepareUploadRegion: function (config, parent) {
        if (!ITVenture.Tools.Uploader.dropzoneConfigured) {
            ITVenture.Tools.Uploader.configureDropzone();
            ITVenture.Tools.Uploader.dropzoneConfigured = true;
        }
        var getUploadedMethod = function (e) {
            var tmp = e[0].attributes.nameTarget.value;
            var retVal = function (file, response) {
                retVal.config[retVal.target](response.Message, response.OriginalFileName, e, file, response);
                $(file.previewElement).children("div[class='dz-image']").hide();
                $(file.previewElement).children("div[class='dz-success-mark']").hide();
                $(file.previewElement).children("div[class='dz-error-mark']").hide();
                $(file.previewElement).parent().children("div[class='dz-default dz-message']").hide();
                $(file.previewElement).children("div[class='dz-details']").html("<i>" + ITVenture.Text.getText("ITVTools_Uploader_Success_Message", "File successfully uploaded") + "</i>");
            };

            retVal.target = tmp;
            retVal.config = config;
            return retVal;
        };

        var getMultiUploadedMethod = function (e) {
            var tmp = e[0].attributes.nameTarget.value;
            var retVal = function (file, response) {
                retVal.config[retVal.target](response.Message, response.OriginalFileName, e);
                $(file.previewElement).remove();
            };

            retVal.target = tmp;
            retVal.config = config;
            return retVal;
        };

        var sl = null;
        var ml = null;
        if (typeof parent === "undefined" || parent === null) {
            sl = $("div[purpose='Upload']");
            ml = $("div[purpose='MultiUpload']");
        } else {
            sl = parent.find("div[purpose='Upload']");
            ml = parent.find("div[purpose='MultiUpload']");
        }

        sl.each(function (e, f) {
            var theHandler = getUploadedMethod($(f));
            var div = $(f);
            div.removeClass("dropzone");
            div.html("<div class='dropzone'></div>");
            var uploadDiv = $($(f).children("div")[0]);
            var uploadHint = "";
            var uploadAsset = "";
            if (typeof (div[0].attributes.uploadHint) !== "undefined") {
                uploadHint = div[0].attributes.uploadHint.value;
            }
            if (typeof (div[0].attributes.sharedAsset) !== "undefined") {
                uploadAsset = div[0].attributes.sharedAsset.value;
            }
            var queryArg = [];
            if (uploadHint !== "") {
                try {
                    uploadHint = eval(uploadHint)(div);
                } catch (ex) {
                    console.log(ex);
                }

                queryArg.push("UploadHint=".concat(encodeURIComponent(uploadHint)));
            }

            if (uploadAsset !== "") {
                try {
                    uploadAsset = eval(uploadAsset)(div);
                } catch (ex) {
                    console.log(ex);
                }

                queryArg.push("AssetKey=".concat(encodeURIComponent(uploadAsset)));
            }

            var query = ITVenture.Tools.Uploader.buildQuery(queryArg);
            var dz = uploadDiv.dropzone({
                url: ITVenture.Helpers.ResolveUrl("~/File/".concat(div[0].attributes.uploadModule.value).concat("/").concat(div[0].attributes.uploadReason.value).concat(query)),
                maxFiles: 1,
                init: function () {
                    this.on("success", theHandler);
                    this.on("error", function (file, message) {
                        $(file.previewElement).children("div[class='dz-error-message']").html("<span>" + message + "</span>");
                    });
                }
            });
            var wrapper = config[theHandler.target + "_wrapper"] = {
                dropzone: dz,
                handler: theHandler
            };

            wrapper.reset = function () {
                wrapper.dropzone[0].dropzone.previewsContainer.children[1].remove();
                $(wrapper.dropzone[0].dropzone.element).removeClass("dz-max-files-reached");
                $(wrapper.dropzone[0].dropzone.element).removeClass("dz-started");
                $(wrapper.dropzone[0].dropzone.element.children[0]).css("display", "inline");
                wrapper.dropzone[0].dropzone.files = [];
            };

            div.removeAttr("purpose");
            div.data("itvUploader", wrapper);
        });

        ml
            .each(function (e, f) {
                var theHandler = getMultiUploadedMethod($(f));
                var div = $(f);
                div.removeClass("dropzone");
                div.html("<div class='dropzone'></div>");
                var uploadDiv = $($(f).children("div")[0]);
                var uploadHint = "";
                var uploadAsset = "";
                if (typeof (div[0].attributes.uploadHint) !== "undefined") {
                    uploadHint = div[0].attributes.uploadHint.value;
                }
                if (typeof (div[0].attributes.sharedAsset) !== "undefined") {
                    uploadAsset = div[0].attributes.sharedAsset.value;
                }
                var queryArg = [];
                if (uploadHint !== "") {
                    try {
                        uploadHint = eval(uploadHint)(div);
                    } catch (ex) {
                        console.log(ex);
                    }

                    queryArg.push("UploadHint=".concat(encodeURIComponent(uploadHint)));
                }

                if (uploadAsset !== "") {
                    try {
                        uploadAsset = eval(uploadAsset)(div);
                    } catch (ex) {
                        console.log(ex);
                    }

                    queryArg.push("AssetKey=".concat(encodeURIComponent(uploadAsset)));
                }

                var query = ITVenture.Tools.Uploader.buildQuery(queryArg);
                var dz = uploadDiv.dropzone({
                    url: ITVenture.Helpers.ResolveUrl("~/File/".concat(div[0].attributes.uploadModule.value).concat("/").concat(div[0].attributes.uploadReason.value).concat(query)),
                    maxFiles: 256,
                    init: function () {
                        this.on("success", theHandler);
                        this.on("error", function (file, message) {
                            console.log(message);
                            $(file.previewElement).children("div[class='dz-error-message']").html("<span>" + message + "</span>");
                        });
                    }
                });
                uploadDiv.attr("style", "width:100%;height:100%");
                config[theHandler.target + "_wrapper"] = {
                    dropzone: dz,
                    handler: theHandler
                };

                div.removeAttr("purpose");
            });
    },
    buildQuery: function(queryArray) {
        var retVal = "";
        for (var i = 0; i < queryArray.length; i++) {
            if (i === 0) {
                retVal = retVal.concat("?").concat(queryArray[i]);
            } else {
                retVal = retVal.concat("&").concat(queryArray[i]);
            }
        }

        return retVal;
    },
    showFile: function (url) {
        ITVenture.Tools.Popup.Open("pdf", url);
    }
};