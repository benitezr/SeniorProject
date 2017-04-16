$(function () {
    var modalID;
    $("#upload-list li").click(function () {
        var id = $(this).attr("id");
        if (!modalID || modalID !== id) {
            $.ajax({
                url: "/Chart/UploadModal/" + id,
                success: function (data) {
                    $("#modal-wrapper").html(data);
                    modalID = id;
                },
                complete: function () {
                    $("#submit").on("click", function () {
                        alert("submit button clicked");
                    });

                    $(":file").filestyle({
                        buttonText: "Find CSV",
                        icon: false,
                        placeholder: "No file chosen",
                        buttonName: "btn-primary"
                    });

                    $("#upload-modal").modal();
                }
            });
        } else {
            $("#upload-modal").modal("toggle");
        }
    });

    function fileSubmit() {

    }
});