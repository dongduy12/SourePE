// CSS (thêm vào <head> hoặc tệp CSS riêng)
const style = document.createElement('style');
document.head.appendChild(style);

// Hàm hiển thị lỗi bằng SweetAlert2
function showError(message) {
    Swal.fire({
        icon: 'error',
        title: 'Lỗi',
        text: message,
        confirmButtonText: 'Đóng',
        confirmButtonColor: '#dc3545'
    });
}

function initializeDataTable() {
    if ($.fn.DataTable.isDataTable('#hassbi-table')) {
        $('#hassbi-table').DataTable().destroy();
    }
    const table = $('#hassbi-table').DataTable({
        paging: true,
        searching: true,
        ordering: false,
        info: true,
        pageLength: 16,
        lengthChange: true,
        autoWidth: false,
        order: [[3, "desc"]],
        dom: 'Bfrtip',//Loại bỏ show ... entries
        buttons: [
            {
                extend: 'excel',
                text: 'Xuất Excel',
                filename: 'HassBi_Data',
                exportOptions: { columns: ':visible' }
            }
        ],
        scrollX: true, // Bật cuộn ngang
        drawCallback: function () {
            this.api().columns.adjust();
        }
    });

    // Đồng bộ khi cửa sổ thay đổi kích thước
    $(window).on('resize', function () {
        table.columns.adjust();
    });

    return table;
}

window.addEventListener("load", function () {
    loadAllStatuses();
});

// Hàm tạo bảng HTML
function generateTableHtml(data) {
    let tableHtml = `
        <div class="table-wrapper">
            <table id="hassbi-table" class="table table-bordered datatable-table table-striped" border="1" cellspacing="0" cellpadding="5">
                <thead>
                    <tr>
                        <th>MoNumber</th>
                        <th>ModelName</th>
                        <th>Type</th>
                        <th>TimeStart</th>
                        <th>Status</th>
                        <th>Remark</th>
                        <th>NvConfirm</th>
                        <th>ConfirmTime</th>
                        <th>KeyPartNo</th>
                        <th>TargetQty</th>
                        <th>SampleSize</th>
                        <th>AQL_0.25</th>
                        <th>PassHassBi</th>
                        <th>FailHassBi</th>
                        <th>Pass_ICT<br>HassBi</th>
                        <th>Fail_ICT<br>HassBi</th>
                        <th>Pass_FT<br>HassBi</th>
                        <th>Fail_FT<br>HassBi</th>
                    </tr>
                </thead>
                <tbody>
    `;

    data.sort((a, b) => new Date(b.timeStart) - new Date(a.timeStart));

    data.forEach(item => {
        const statusClass =
            item.status === "Close" ? "status-close" :
                item.status === "Fail" ? "status-fail" :
                    item.status === "On-going" ? "status-on-going" : "";

        const oracleRows = item.oracleData.map(o => `
            <tr>
                <td class="monumber-cell">${item.moNumber}</td>
                <td>${o.modeL_NAME || "N/A"}</td>
                <td title="${item.type || "N/A"}" class="type-cell">${item.type}</td>
                <td title="${item.timeStart || ""}">${new Date(item.timeStart).toLocaleString()}</td>
                <td class="${statusClass}">${item.status}</td>
                <td title="${item.remark || ""}" class="remark-cell" data-monumber="${item.moNumber}" data-type="${item.type}">${item.remark || "N/A"}</td>
                <td class="nvconfirm-cell" data-monumber="${item.moNumber}" data-type="${item.type}">${item.nvConfirm || "N/A"}</td>
                <td title="${item.confirmTime ? new Date(item.confirmTime).toLocaleString() : "N/A"}" class="confirmtime-cell">${item.confirmTime ? new Date(item.confirmTime).toLocaleString() : "N/A"}</td>
                <td>${o.keY_PART_NO || "N/A"}</td>
                <td>${o.targeT_QTY || 0}</td>
                <td>${o.samplE_SIZE || 0}</td>
                <td>${o.majoR_REJECT_QTY || 0}</td>
                <td>${o.pasS_QTY || 0}</td>
                <td class="failqty-cell">${o.faiL_QTY || 0}</td>
                <td>${o.pasS_QTY1 || 0}</td>
                <td class="failqty1-cell">${o.faiL_QTY1 || 0}</td>
                <td>${o.pasS_QTY2 || 0}</td>
                <td class="failqty2-cell">${o.faiL_QTY2 || 0}</td>
            </tr>
        `).join("");

        tableHtml += oracleRows;
    });

    tableHtml += `
                </tbody>
            </table>
        </div>
    `;
    return tableHtml;
}

// Tải toàn bộ trạng thái
async function loadAllStatuses() {
    const resultsContainer = document.getElementById("data-results");
    try {
        const response = await fetch("http://10.220.130.119:9090/api/HassBi/search-all-statuses", {
            method: "POST",
            headers: { "Content-Type": "application/json" }
        });

        if (!response.ok) throw new Error(await response.text());
        const data = await response.json();

        if (data.length === 0) {
            resultsContainer.innerHTML = "<p>Không tìm thấy dữ liệu.</p>";
            return;
        }

        resultsContainer.innerHTML = generateTableHtml(data);
        initializeDataTable();
    } catch (error) {
        showError("Có lỗi xảy ra khi tải dữ liệu: " + error.message);
    }
}

// Xử lý Remark với SweetAlert2
document.getElementById("data-results").addEventListener("click", function (event) {
    if (event.target.classList.contains("remark-cell")) {
        const moNumber = event.target.dataset.monumber;
        const type = event.target.dataset.type;
        const currentRemark = event.target.textContent.trim();

        Swal.fire({
            title: 'Chỉnh sửa Remark',
            input: 'textarea',
            inputValue: currentRemark,
            inputAttributes: {
                style: 'width: 400px; height: 200px;'
            },
            showCancelButton: true,
            confirmButtonText: 'Save',
            cancelButtonText: 'Cancel',
            confirmButtonColor: '#4caf50',
            cancelButtonColor: '#f44336',
            preConfirm: (updatedRemark) => {
                if (!updatedRemark) {
                    Swal.showValidationMessage('Remark không được để trống');
                }
                return updatedRemark;
            }
        }).then(async (result) => {
            if (result.isConfirmed) {
                const updatedRemark = result.value;
                try {
                    const response = await fetch("http://10.220.130.119:9090/api/HassBi/update-remark", {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ moNumber, type, remark: updatedRemark })
                    });

                    if (!response.ok) throw new Error(await response.text());
                    event.target.textContent = updatedRemark || "N/A";
                    Swal.fire({
                        icon: 'success',
                        title: 'Thành công',
                        text: 'Cập nhật Remark thành công!',
                        confirmButtonText: 'OK',
                        confirmButtonColor: '#4caf50'
                    });
                } catch (error) {
                    showError("Có lỗi xảy ra khi cập nhật Remark: " + error.message);
                }
            }
        });
    }
});

// Xử lý NvConfirm với SweetAlert2
document.addEventListener("DOMContentLoaded", function () {
    document.getElementById("data-results").addEventListener("click", function (event) {
        if (event.target.classList.contains("nvconfirm-cell")) {
            const cell = event.target;
            const moNumber = cell.dataset.monumber;
            const type = cell.dataset.type;
            const currentStatus = cell.textContent.trim();

            Swal.fire({
                title: 'Xác nhận NvConfirm',
                html: `
                    <label>
                        <input type="checkbox" id="swal-nvconfirm-checkbox" ${currentStatus === "Approve" ? "checked" : ""}>
                        Approve
                    </label>
                `,
                showCancelButton: true,
                confirmButtonText: 'Save',
                cancelButtonText: 'Cancel',
                confirmButtonColor: '#4caf50',
                cancelButtonColor: '#f44336'
            }).then(async (result) => {
                if (result.isConfirmed) {
                    const checkbox = document.getElementById("swal-nvconfirm-checkbox");
                    const newStatus = checkbox.checked ? "Approve" : "Not Approve";
                    try {
                        const response = await fetch("http://10.220.130.119:9090/api/HassBi/update-nvconfirm", {
                            method: "POST",
                            headers: { "Content-Type": "application/json" },
                            body: JSON.stringify({ moNumber, type, nvConfirm: newStatus })
                        });

                        if (!response.ok) throw new Error(await response.text());
                        const result = await response.json();
                        cell.textContent = newStatus;
                        cell.parentElement.querySelector(".confirmtime-cell").textContent = new Date(result.confirmTime).toLocaleString();
                        Swal.fire({
                            icon: 'success',
                            title: 'Thành công',
                            text: 'Cập nhật NvConfirm thành công!',
                            confirmButtonText: 'OK',
                            confirmButtonColor: '#4caf50'
                        });
                    } catch (error) {
                        showError("Có lỗi xảy ra khi cập nhật NvConfirm: " + error.message);
                    }
                }
            });
        }
    });
});

// Xử lý FailQty với SweetAlert2
document.getElementById("data-results").addEventListener("click", async function (event) {
    if (event.target.classList.contains("failqty-cell") || event.target.classList.contains("failqty1-cell") || event.target.classList.contains("failqty2-cell")) {
        const value = parseInt(event.target.textContent.trim());
        if (value === 0) return;

        const row = event.target.closest("tr");
        const moNumber = row.querySelector(".monumber-cell").textContent.trim();
        let type = row.querySelector(".type-cell").textContent.trim();
        const status = "1";

        type = type === "HASS_SAMPLING" ? "HASS" : type === "BI_SAMPLING" ? "BI" : type;

        try {
            const response = await fetch("http://10.220.130.119:9090/api/HassBi/get-failed-serial-numbers", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ moNumber, type, status })
            });

            if (!response.ok) throw new Error(await response.text());
            const data = await response.json();

            data.forEach(item => {
                if (item.data1 && item.data1.length > 150) item.data1 = item.data1.substring(0, 150) + "...";
            });

            const tableHtml = `
                <div style="overflow: auto; max-height: 400px; border: 1px solid #ddd;">
                    <table style="width: 100%; border-collapse: collapse; font-size: 14px;">
                        <thead style="background: #e9ecef;">
                            <tr>
                                <th style="border: 1px solid #ddd; padding: 5px;">Serial Number</th>
                                <th style="border: 1px solid #ddd; padding: 5px;">Model Name</th>
                                <th style="border: 1px solid #ddd; padding: 5px;">Mo Number</th>
                                <th style="border: 1px solid #ddd; padding: 5px;">Group Name</th>
                                <th style="border: 1px solid #ddd; padding: 5px;">Fail Time</th>
                                <th style="border: 1px solid #ddd; padding: 5px;">Test Code</th>
                                <th style="border: 1px solid #ddd; padding: 5px;">Data</th>
                            </tr>
                        </thead>
                        <tbody>
                            ${data.map(row => `
                                <tr>
                                    <td style="border: 1px solid #ddd; padding: 5px;">${row.serialNumber}</td>
                                    <td style="border: 1px solid #ddd; padding: 5px;">${row.modelName}</td>
                                    <td style="border: 1px solid #ddd; padding: 5px;">${row.moNumber}</td>
                                    <td style="border: 1px solid #ddd; padding: 5px;">${row.groupName}</td>
                                    <td style="border: 1px solid #ddd; padding: 5px;">${row.failTime}</td>
                                    <td style="border: 1px solid #ddd; padding: 5px;">${row.testCode}</td>
                                    <td style="border: 1px solid #ddd; padding: 5px;">${row.data1}</td>
                                </tr>
                            `).join("")}
                        </tbody>
                    </table>
                </div>
            `;

            Swal.fire({
                title: 'Dữ liệu Serial Number bị Fail',
                html: tableHtml,
                width: '90%',
                showCancelButton: false,
                confirmButtonText: 'Close',
                confirmButtonColor: '#dc3545'
            });
        } catch (error) {
            showError("Có lỗi xảy ra khi lấy dữ liệu FailQty: " + error.message);
        }
    }
});

// Xử lý nút cập nhật MO/Status với SweetAlert2
document.getElementById("Update-MO-status-btn").addEventListener("click", async function () {
    const result = await Swal.fire({
        title: 'Xác nhận',
        text: 'Bạn có chắc muốn cập nhật MO/Status?',
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: 'Có',
        cancelButtonText: 'Không',
        confirmButtonColor: '#4caf50',
        cancelButtonColor: '#f44336'
    });

    if (!result.isConfirmed) return;

    try {
        const response = await fetch("http://10.220.130.119:9090/api/HassBi/update-mo-status", {
            method: "POST"
        });

        if (!response.ok) throw new Error(await response.text());
        Swal.fire({
            icon: 'success',
            title: 'Thành công',
            text: await response.text(),
            confirmButtonText: 'OK',
            confirmButtonColor: '#4caf50'
        }).then(() => {
            loadAllStatuses();
        });
    } catch (error) {
        showError("Có lỗi xảy ra khi cập nhật MO/Status: " + error.message);
    }
});

$(document).ready(function () {
    // Khởi tạo DataTable (chỉ cấu hình cơ bản, dữ liệu sẽ được nạp sau)
    let table = $('#skippedMoTable').DataTable({
        responsive: true,
        language: {
            emptyTable: "Không có dữ liệu",
            loadingRecords: "Đang tải...",
            processing: "Đang xử lý...",
            search: "Tìm kiếm:",
            lengthMenu: "Hiển thị _MENU_ bản ghi",
            info: "Hiển thị _START_ đến _END_ của _TOTAL_ bản ghi",
            infoEmpty: "Hiển thị 0 đến 0 của 0 bản ghi",
            paginate: {
                first: "Đầu",
                last: "Cuối",
                next: "Tiếp",
                previous: "Trước"
            }
        }, columnDefs: [
            { width: '15%', targets: [0, 1, 2, 3] }, // 4 cột đầu 15%
            { width: '40%', targets: 4 }              // Cột cuối 40%
        ]
    });

    // Xử lý click button MO SKIP
    $('#skip-btn').on('click', function () {
        // Hiển thị modal
        $('#skippedMoModal').modal('show');

        // Gọi API và nạp dữ liệu vào DataTable
        $.ajax({
            url: 'http://10.220.130.119:9090/api/HassBi/get-skipped-mos', // URL của API
            method: 'GET',
            dataType: 'json',
            success: function (data) {
                // Xóa dữ liệu cũ trong DataTable
                table.clear();

                // Thêm dữ liệu mới từ API
                if (data && Array.isArray(data)) {
                    table.rows.add(data.map(item => [
                        item.modeL_NAME || '',
                        item.mO_NUMBER || '',
                        item.type || '',
                        item.systeM_NAME || '',
                        item.description || ''
                    ]));
                }

                // Vẽ lại bảng
                table.draw();
            },
            error: function (xhr) {
                // Xử lý lỗi
                let errorMessage = 'Lỗi khi tải dữ liệu';
                if (xhr.status === 404) {
                    errorMessage = 'Không tìm thấy MO nào bị Skipped';
                } else if (xhr.responseJSON && xhr.responseJSON.message) {
                    errorMessage = xhr.responseJSON.message;
                }
                alert(errorMessage);
                // Xóa dữ liệu bảng nếu lỗi
                table.clear().draw();
            }
        });
    });
});


// xử lý nút xuất Excel
document.getElementById('HassBi-ExportExcel-btn').addEventListener('click', function () {
    // Lấy instance của DataTable
    const table = $('#hassbi-table').DataTable();

    // Kiểm tra nếu bảng tồn tại
    if (!table) {
        alert("Không có dữ liệu để xuất!");
        return;
    }

    // Lấy tất cả dữ liệu từ DataTable (bao gồm tất cả các trang)
    const tableData = table.data().toArray().map(row => {
        // Chuyển mỗi hàng thành mảng các giá trị
        return Array.from(row).map(cell => String(cell).replace(/,/g, '')); // Loại bỏ dấu phẩy
    });

    // Thêm tiêu đề cột (header) vào dữ liệu
    const headers = table.columns().header().toArray().map(header => header.textContent.replace(/,/g, ''));
    tableData.unshift(headers);

    // Chuyển dữ liệu thành định dạng CSV
    const csvContent = tableData.map(row => row.join(',')).join('\n');

    // Tạo Blob chứa dữ liệu CSV
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);

    // Tạo thẻ <a> để tải file
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', 'HassBi.csv');
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);

    // Thông báo thành công
    alert("Xuất dữ liệu ra file Excel thành công!");
});