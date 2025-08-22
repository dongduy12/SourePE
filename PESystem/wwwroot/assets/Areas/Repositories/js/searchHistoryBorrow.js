$(document).ready(function () {
    let dataTable = $("#history-borrow-table").DataTable({
        columns: [
            { title: "Serial Number", data: "serialNumber" },
            { title: "Product Line", data: "productLine" },
            { title: "Model Name", data: "modelName" },
            { title: "Borrow Date", data: "borrowDate" },
            { title: "Borrow Person", data: "borrowPerson" }
        ],
        order: [[0, "asc"]],
        responsive: true,
        searching: true,
        paging: false,
        info: false,
        lengthMenu: [5, 10, 20, 50],
    });

    $("#search-history-borrow-btn").click(function () {
        $("#search-results").hide(); // ẩn bảng
        // Lấy danh sách Serial Numbers từ textarea
        let serialNumbers = $("#history-borrow-sn-input").val()
            .split("\n") // Tách từng dòng
            .map(sn => sn.trim()) // Loại bỏ khoảng trắng thừa
            .filter(sn => sn.length > 0); // Bỏ dòng trống

        if (serialNumbers.length === 0) {
            showWarning("Nhập ít nhất một Serial Number!");
            return;
        }

        // Gửi API tìm kiếm
        $.ajax({
            url: "http://10.220.130.119:9090/api/Borrow/Search", // Thay bằng địa chỉ API thực tế
            type: "POST",
            contentType: "application/json",
            data: JSON.stringify(serialNumbers), // ĐÚNG ĐỊNH DẠNG JSON
            success: function (response) {
                if (response.success) {
                    $("#result-history").show(); // Hiển thị bảng
                   
                    updateDataTable(response.data);
                } else {
                    $("#result-history").hide(); // Ẩn bảng nếu không có kết quả
                    showError(response.message);
                }
            },
            error: function (xhr) {
                showError("Lỗi " + xhr.responseText);
            }
        });
    });

    // Hàm cập nhật DataTable với dữ liệu mới
    function updateDataTable(data) {
        dataTable.clear();
        data.forEach(item => {
            dataTable.row.add({
                serialNumber: item.serialNumber,
                productLine: item.productLine,
                modelName: item.modelName,
                borrowDate: item.borrowDate ? new Date(item.borrowDate).toLocaleString() : "N/A",
                borrowPerson: item.borrowPerson || "N/A"
            });
        });
        dataTable.draw();
    }
});
