$(function () {
    $("#btnSend").click(function () {
        var payload = $("#ppInputPayload").val();

        if (payload == "") {
            $("#ppResponse").html("Please specify a payload.");
            return;
        }

        $.ajax({
            type: "POST",
            url: "/productionplan",
            data: payload,
            cache: false,
            contentType: 'application/json; charset=utf-8',
            dataType: "json",
            success: function (responseJSON) {
                var prettyfiedResponse = JSON.stringify(responseJSON, undefined, 4);
                $("#ppResponse").html(prettyfiedResponse);
            },
            error: function (errorText) {
                $("#ppResponse").html(errorText.responseText);
            }
        });
    });
});