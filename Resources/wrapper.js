// Should package assets :/
$(document).ready(function () {
    $(".navbar-fixed-bottom").remove();
    $(".navbar-brand").remove();
    $("a[ng-click='shutdown()']").parent().remove();
    $("#settings .col-md-6").first().removeClass("col-md-6").addClass("col-md-12").append($("#UREnabled").closest(".form-group").detach());
    $("#settings .col-md-6").last().remove();
    $("a[href*=http]").attr("href", "");

    $("<style type='text/css'>").html(".modal-backdrop.in { opacity: 0; }").appendTo("head");
});
