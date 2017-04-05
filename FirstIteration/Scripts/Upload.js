$("#upload-list li").click(function () {
    var id = $(this).attr("id");
    $.ajax({
        url: "/Chart/UploadModal/" + id,
        success: function (data) {
            $("#modal-wrapper").html(data);
            $("#upload-modal").modal();
        }
    });
});