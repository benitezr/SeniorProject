var modalID;
$("#upload-list li").click(function () {
    var id = $(this).attr("id");
    if(!modalID || modalID !== id){
        $.ajax({
            url: "/Chart/UploadModal/" + id,
            success: function (data) {
                $("#modal-wrapper").html(data);
                $("#upload-modal").modal();
                //$(document).find("#submit").attr("onclick", "");
                modalID = id;
            }
        });   
    } else {
        $("#upload-modal").modal("toggle");
    }
});