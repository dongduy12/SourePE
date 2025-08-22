// Hàm để ẩn tất cả các section
function hideAllSections() {
    const allSections = [
        "wait-re-move-mrb", "wait-re-move-mrb-result",
        "wait-mrb-confirm", "wait-mrb-confirm-result",
        "moved-mrb-form", "moved-mrb-form-result"
    ];

    allSections.forEach(id => {
        const el = document.getElementById(id);
        if (el) {
            el.classList.add("hidden");
            console.log(`Hidden element with ID: ${id}`);
        } else {
            console.warn(`Element with ID ${id} not found.`);
        }
    });
}

// Hàm hiển thị bảng với DataTable (tương tự Function.js)
function renderTableWithDataTable(data, tableId, checkboxName, selectAllId) {
    let selectedTasks = new Set(); // Bộ lưu trữ các task đã chọn cho mỗi bảng

    const tableBody = document.querySelector(`#${tableId} tbody`);
    if (!tableBody) {
        const tableHtml = `
            <table id="${tableId}" class="table table-bordered table-striped datatable-table" style="width:100%;">
                <thead>
                    <tr>
                        <th class="checkbox-column"><input type="checkbox" id="${selectAllId}"></th>
                        <th>Task Number</th>
                        <th>Apply Time</th>
                        <th>Total Qty</th>
                    </tr>
                </thead>
                <tbody></tbody>
            </table>
        `;
        document.getElementById(`${tableId}-result`.replace('-table', '')).innerHTML = tableHtml;
    }

    const tableBodyExisting = document.querySelector(`#${tableId} tbody`);
    tableBodyExisting.innerHTML = ""; // Xóa nội dung cũ

    data.forEach(item => {
        const isChecked = selectedTasks.has(item.taskNumber) ? 'checked' : '';
        const row = `
            <tr>
                <td class="checkbox-column"><input type="checkbox" name="${checkboxName}" value="${item.taskNumber}" ${isChecked}></td>
                <td data-bs-toggle="tooltip" data-bs-title="${item.taskNumber || 'N/A'}">${item.taskNumber || "N/A"}</td>
                <td data-bs-toggle="tooltip" data-bs-title="${item.applyTime || 'N/A'}">${item.applyTime || "N/A"}</td>
                <td data-bs-toggle="tooltip" data-bs-title="${item.totalQty || 'N/A'}">${item.totalQty || "N/A"}</td>
            </tr>
        `;
        tableBodyExisting.insertAdjacentHTML('beforeend', row);
    });

    // Khởi tạo DataTable
    const dataTable = $(`#${tableId}`).DataTable({
        pageLength: 10,
        lengthMenu: [10, 25, 50, 100],
        order: [],
        columnDefs: [
            { orderable: false, targets: 0 },
            { width: "40px", targets: 0 },
            { width: "300px", targets: [1, 2] }
        ],
        language: {
            search: "Tìm kiếm:",
            lengthMenu: "Hiển thị _MENU_ dòng mỗi trang",
            info: "Hiển thị _START_ đến _END_ của _TOTAL_ dòng",
            paginate: {
                first: "Đầu",
                last: "Cuối",
                next: "Tiếp",
                previous: "Trước"
            }
        },
        destroy: true,
        drawCallback: function () {
            const tooltipTriggerList = document.querySelectorAll(`#${tableId} [data-bs-toggle="tooltip"]`);
            tooltipTriggerList.forEach(tooltipTriggerEl => {
                new bootstrap.Tooltip(tooltipTriggerEl, {
                    placement: 'top',
                    trigger: 'hover'
                });
            });
        }
    });

    // Xử lý checkbox "Select All"
    const selectAllCheckbox = document.getElementById(selectAllId);
    if (selectAllCheckbox) {
        selectAllCheckbox.checked = data.length > 0 && data.every(item => selectedTasks.has(item.taskNumber));
        selectAllCheckbox.addEventListener("change", function () {
            const isChecked = this.checked;
            data.forEach(item => {
                if (isChecked) selectedTasks.add(item.taskNumber);
                else selectedTasks.delete(item.taskNumber);
            });
            $(`input[name="${checkboxName}"]`).prop("checked", isChecked);
        });
    }

    // Xử lý checkbox riêng lẻ
    $(document).off("change", `input[name="${checkboxName}"]`).on("change", `input[name="${checkboxName}"]`, function () {
        const taskNumber = this.value;
        if (this.checked) selectedTasks.add(taskNumber);
        else selectedTasks.delete(taskNumber);
        selectAllCheckbox.checked = data.every(item => selectedTasks.has(item.taskNumber));
    });
}

// Hàm gọi API và hiển thị dữ liệu
async function fetchAndRenderData(status, resultDivId, tableId, checkboxName, selectAllId) {
    const resultDiv = document.getElementById(resultDivId);
    resultDiv.innerHTML = `<div class="alert alert-info"><strong>Thông báo:</strong> Đang tải dữ liệu...</div>`;

    try {
        const response = await fetch(`http://10.220.130.119:9090/api/Scrap/get-task-by-status?status=${status}`, {
            method: "GET",
            headers: { "Content-Type": "application/json" }
        });

        const result = await response.json();
        if (response.ok) {
            const sortedData = result.sort((a, b) => new Date(a.applyTime) - new Date(b.applyTime));
            renderTableWithDataTable(sortedData, tableId, checkboxName, selectAllId);
        } else {
            resultDiv.innerHTML = `<div class="alert alert-danger"><strong>Lỗi:</strong> ${result.message}</div>`;
        }
    } catch (error) {
        resultDiv.innerHTML = `<div class="alert alert-danger"><strong>Lỗi:</strong> Không thể kết nối đến API. Vui lòng kiểm tra lại.</div>`;
        console.error("Error:", error);
    }
}

// Hàm hiển thị popup để nhập SN
function showConfirmPopup(selectedTaskNumbers, buttonId) {
    // Tạo HTML cho popup
    const popupHtml = `
        <div class="modal fade" id="confirmPopup" tabindex="-1" aria-labelledby="confirmPopupLabel" aria-hidden="true">
            <div class="modal-dialog">
                <div class="modal-content">
                    <div class="modal-header">
                        <h5 class="modal-title" id="confirmPopupLabel">Vui lòng xác nhận lại SN thực tế</h5>
                        <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                    </div>
                    <div class="modal-body">
                        <textarea id="snInput" class="form-control" rows="5" placeholder="Nhập danh sách SN, mỗi SN trên một dòng"></textarea>
                    </div>
                    <div class="modal-footer">
                        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Hủy</button>
                        <button type="button" class="btn btn-primary" id="confirmBtn">Xác nhận</button>
                    </div>
                </div>
            </div>
        </div>
    `;

    // Thêm popup vào body
    document.body.insertAdjacentHTML('beforeend', popupHtml);

    // Hiển thị popup
    const modal = new bootstrap.Modal(document.getElementById('confirmPopup'));
    modal.show();

    // Xử lý khi nhấn nút "Xác nhận"
    document.getElementById('confirmBtn').addEventListener('click', async () => {
        const snText = document.getElementById('snInput').value.trim();
        const snList = snText.split('\n').map(sn => sn.trim()).filter(sn => sn.length > 0);

        if (snList.length === 0) {
            alert('Vui lòng nhập ít nhất một SN.');
            return;
        }

        // Gửi yêu cầu đến API
        const payload = {
            sNs: snList,
            taskNumbers: selectedTaskNumbers
        };

        try {
            const response = await fetch('http://10.220.130.119:9090/api/Scrap/confirm-move-mrb', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });

            const result = await response.json();
            alert(result.message); // Hiển thị thông báo từ API
            modal.hide(); // Đóng popup sau khi xử lý
            // Làm mới dữ liệu bảng sau khi cập nhật thành công
            if (buttonId === 'move-mrb-btn' && document.getElementById('wait-re-move-mrb-result')) {
                fetchAndRenderData(5, 'wait-re-move-mrb-result', 'wait-re-move-mrb-table', 'mrb-checkbox-5', 'select-all-5');
            } else if (buttonId === 'confirm-mrb-btn' && document.getElementById('wait-mrb-confirm-result')) {
                fetchAndRenderData(6, 'wait-mrb-confirm-result', 'wait-mrb-confirm-table', 'mrb-checkbox-6', 'select-all-6');
            }
        } catch (error) {
            alert('Đã xảy ra lỗi khi gửi yêu cầu: ' + error.message);
        }

        // Xóa popup sau khi đóng
        document.getElementById('confirmPopup').remove();
    });

    // Xóa popup khi đóng bằng nút "Hủy" hoặc nút close
    modal._element.addEventListener('hidden.bs.modal', () => {
        document.getElementById('confirmPopup').remove();
    });
}

// Hàm xuất dữ liệu bảng thành file Excel
function exportTableToExcel(tableId, filename) {
    const table = document.getElementById(tableId);
    const wb = XLSX.utils.table_to_book(table, { sheet: "Sheet1" });
    XLSX.writeFile(wb, `${filename}_${new Date().toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }).replace(/[\/:]/g, '-')}.xlsx`);
}

// Gọi hàm ẩn ngay lập tức và khi trang tải
console.log("MRB.js loaded and hiding sections...");
hideAllSections(); // Ẩn ngay khi file được tải
document.addEventListener("DOMContentLoaded", function () {
    console.log("DOM fully loaded, hiding sections again...");
    hideAllSections(); // Đảm bảo ẩn lại khi DOM sẵn sàng

    // Sự kiện change cho search-mrb-options
    const searchMrbOptions = document.getElementById("search-mrb-options");
    if (searchMrbOptions) {
        searchMrbOptions.addEventListener("change", function () {
            const value = this.value;
            console.log(`Dropdown changed to: ${value}`);

            // Ẩn toàn bộ trước
            hideAllSections();

            // Hiện thẻ và gọi API phù hợp với giá trị đã chọn
            if (value === "5") {
                document.getElementById("wait-re-move-mrb")?.classList.remove("hidden");
                document.getElementById("wait-re-move-mrb-result")?.classList.remove("hidden");
                fetchAndRenderData(5, "wait-re-move-mrb-result", "wait-re-move-mrb-table", "mrb-checkbox-5", "select-all-5");
                console.log("Showing wait-re-move-mrb and result, fetching data for status 5");
            } else if (value === "6") {
                document.getElementById("wait-mrb-confirm")?.classList.remove("hidden");
                document.getElementById("wait-mrb-confirm-result")?.classList.remove("hidden");
                fetchAndRenderData(6, "wait-mrb-confirm-result", "wait-mrb-confirm-table", "mrb-checkbox-6", "select-all-6");
                console.log("Showing wait-mrb-confirm and result, fetching data for status 6");
            } else if (value === "7") {
                document.getElementById("moved-mrb-form")?.classList.remove("hidden");
                document.getElementById("moved-mrb-form-result")?.classList.remove("hidden");
                fetchAndRenderData(7, "moved-mrb-form-result", "moved-mrb-form-table", "mrb-checkbox-7", "select-all-7");
                console.log("Showing moved-mrb-form and result, fetching data for status 7");
            }
        });
    } else {
        console.error("Element with ID 'search-mrb-options' not found.");
    }

    // Sự kiện cho nút "Move MRB"
    document.getElementById('move-mrb-btn')?.addEventListener('click', () => {
        const selectedTaskNumbers = Array.from(document.querySelectorAll('input[name="mrb-checkbox-5"]:checked'))
            .map(checkbox => checkbox.value);
        if (selectedTaskNumbers.length === 0) {
            alert('Vui lòng chọn ít nhất một Task Number.');
            return;
        }
        showConfirmPopup(selectedTaskNumbers, 'move-mrb-btn');
    });

    // Sự kiện cho nút "Confirm"
    document.getElementById('confirm-mrb-btn')?.addEventListener('click', () => {
        const selectedTaskNumbers = Array.from(document.querySelectorAll('input[name="mrb-checkbox-6"]:checked'))
            .map(checkbox => checkbox.value);
        if (selectedTaskNumbers.length === 0) {
            alert('Vui lòng chọn ít nhất một Task Number.');
            return;
        }
        showConfirmPopup(selectedTaskNumbers, 'confirm-mrb-btn');
    });

    // Sự kiện cho nút "Load data" (xuất Excel)
    document.getElementById('moved-mrb-btn')?.addEventListener('click', () => {
        const table = document.getElementById('moved-mrb-form-table');
        if (table && table.rows.length > 1) { // Kiểm tra bảng có dữ liệu (ít nhất có header và 1 dòng)
            exportTableToExcel('moved-mrb-form-table', 'Moved_MRB_Data');
        } else {
            alert('Không có dữ liệu để xuất thành file Excel.');
        }
    });
});