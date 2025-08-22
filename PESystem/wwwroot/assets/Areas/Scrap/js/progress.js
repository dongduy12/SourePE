let selectedInternalTasks = new Set(); // Lưu các InternalTask đã chọn (cho TASK_STOCK_STATUS)
let selectedSNs = new Set(); // Lưu các SN đã chọn (cho SEARCH_STATUS)

// Hàm để ẩn tất cả các form và khu vực kết quả
function hideAllElements() {
    const forms = ["task-stock-status-form", "update-status-form", "search-status-form"];
    const results = ["task-stock-status-result", "update-status-result", "search-status-result"];

    forms.forEach(formId => {
        const form = document.getElementById(formId);
        if (form) {
            form.classList.add("hidden");
        } else {
            console.warn(`Form with ID ${formId} not found.`);
        }
    });

    results.forEach(resultId => {
        const result = document.getElementById(resultId);
        if (result) {
            result.classList.add("hidden");
        } else {
            console.warn(`Result with ID ${resultId} not found.`);
        }
    });

    // Xóa giá trị của các trường nhập liệu khi ẩn form
    const snStatusUpdate = document.getElementById("sn-status-update");
    const statusOptions = document.getElementById("status-options");
    const searchStatusUpdate = document.getElementById("search-status-update");
    const searchStatusOptions = document.getElementById("search-status-options");

    if (snStatusUpdate) snStatusUpdate.value = "";
    if (statusOptions) statusOptions.selectedIndex = 0;
    if (searchStatusUpdate) searchStatusUpdate.value = "";
    if (searchStatusOptions) searchStatusOptions.selectedIndex = 0;
}

// Hàm tạo và tải xuống file Excel
function exportToExcel(noInternalTaskData, hasInternalTaskNoTaskNumberData, hasTaskNumberData, filename) {
    const workbook = XLSX.utils.book_new();

    // Thêm sheet cho dữ liệu chưa có Internal Task
    if (noInternalTaskData.length > 0) {
        const noInternalTaskSheet = XLSX.utils.json_to_sheet(noInternalTaskData);
        XLSX.utils.book_append_sheet(workbook, noInternalTaskSheet, "NoInternalTask");
    }

    // Thêm sheet cho dữ liệu có Internal Task nhưng chưa có Task Number
    if (hasInternalTaskNoTaskNumberData.length > 0) {
        const hasInternalTaskNoTaskNumberSheet = XLSX.utils.json_to_sheet(hasInternalTaskNoTaskNumberData);
        XLSX.utils.book_append_sheet(workbook, hasInternalTaskNoTaskNumberSheet, "HasInternalNoTaskNumber");
    }

    // Thêm sheet cho dữ liệu đã có Task Number
    if (hasTaskNumberData.length > 0) {
        const hasTaskNumberSheet = XLSX.utils.json_to_sheet(hasTaskNumberData);
        XLSX.utils.book_append_sheet(workbook, hasTaskNumberSheet, "HasTaskNumber");
    }

    XLSX.writeFile(workbook, filename);
}

// Hàm hiển thị bảng với phân trang
function renderTableWithPagination(data, resultDiv, tableHeaders, rowTemplate, extraHtml = "", tableIdPrefix = "") {
    const rowsPerPage = 10; // Số dòng mỗi trang
    let currentPage = 1; // Trang hiện tại
    const totalRows = data.length; // Tổng số dòng
    const totalPages = Math.ceil(totalRows / rowsPerPage); // Tổng số trang

    // Hàm hiển thị dữ liệu cho trang hiện tại
    function displayPage(page) {
        const start = (page - 1) * rowsPerPage;
        const end = start + rowsPerPage;
        const paginatedData = data.slice(start, end);

        let tableHtml = `
            <div class="table-container">
                <table>
                    <thead>
                        <tr>
                            ${tableHeaders}
                        </tr>
                    </thead>
                    <tbody>
        `;

        paginatedData.forEach(item => {
            tableHtml += rowTemplate(item);
        });

        tableHtml += `
                    </tbody>
                </table>
            </div>
        `;

        // Thêm nút phân trang nếu cần
        if (totalRows > rowsPerPage) {
            const prevButtonId = tableIdPrefix ? `${tableIdPrefix}-prev-page` : "prev-page";
            const nextButtonId = tableIdPrefix ? `${tableIdPrefix}-next-page` : "next-page";
            tableHtml += `
                <div class="pagination" style="margin-top: 10px; text-align: center;">
                    <button id="${prevButtonId}" class="btn btn-sm btn-secondary" ${currentPage === 1 ? 'disabled' : ''}>Previous</button>
                    <span style="margin: 0 10px;">Page ${currentPage} of ${totalPages}</span>
                    <button id="${nextButtonId}" class="btn btn-sm btn-secondary" ${currentPage === totalPages ? 'disabled' : ''}>Next</button>
                </div>
            `;
        }

        // Thêm HTML bổ sung (nếu có, ví dụ: thông báo SN không tìm thấy)
        tableHtml += extraHtml;

        resultDiv.innerHTML = tableHtml;

        // Thêm sự kiện cho nút phân trang (sau khi HTML được render)
        if (totalRows > rowsPerPage) {
            const prevButtonId = tableIdPrefix ? `${tableIdPrefix}-prev-page` : "prev-page";
            const nextButtonId = tableIdPrefix ? `${tableIdPrefix}-next-page` : "next-page";
            const prevButton = document.getElementById(prevButtonId);
            const nextButton = document.getElementById(nextButtonId);

            if (prevButton) {
                prevButton.addEventListener("click", () => {
                    if (currentPage > 1) {
                        currentPage--;
                        displayPage(currentPage);
                    }
                });
            }

            if (nextButton) {
                nextButton.addEventListener("click", () => {
                    if (currentPage < totalPages) {
                        currentPage++;
                        displayPage(currentPage);
                    }
                });
            }
        }
    }

    // Hiển thị trang đầu tiên
    displayPage(currentPage);
}

// Hàm gọi API và hiển thị bảng dữ liệu cho TASK_STOCK_STATUS
async function loadFindBoardStatus() {
    const resultDiv = document.getElementById("task-stock-status-result");

    // Hiển thị thông báo "đang tải dữ liệu"
    resultDiv.innerHTML = `
        <div class="alert alert-info">
            <strong>Thông báo:</strong> Đang tải dữ liệu...
        </div>
    `;

    try {
        // Gọi API để lấy dữ liệu FindBoardStatus
        const response = await fetch("http://10.220.130.119:9090/api/Scrap/get-find-board-status", {
            method: "GET",
            headers: {
                "Content-Type": "application/json"
            }
        });

        const result = await response.json();

        if (response.ok) {
            // Sắp xếp dữ liệu theo CreateTime (từ cũ đến mới)
            const sortedData = result.sort((a, b) => new Date(a.createTime) - new Date(b.createTime));

            const tableHeaders = `
                <th>Internal Task</th>
                <th>Approve Scrap Person</th>
                <th>Description</th>
                <th>After/Before</th>
                <th>Create Time</th>
                <th>Total Q'ty</th>
                <th>Qty Scraped</th>
                <th>Qty Find Ok</th>
                <th>Qty Wait Find</th>
                <th>Status</th>
            `;

            const rowTemplate = (item) => {
                let statusBackgroundColor = "";
                if (item.status === "close") {
                    statusBackgroundColor = "#28a745"; // Màu xanh lá cây (Bootstrap success)
                } else if (item.status === "on-going") {
                    statusBackgroundColor = "#ffc107"; // Màu vàng (Bootstrap warning)
                }

                return `
                    <tr>
                        <td>${item.internalTask}</td>
                        <td>${item.approveScrapPerson}</td>
                        <td>${item.description}</td>
                        <td>${item.afterBefore}</td>
                        <td>${item.createTime}</td>
                        <td>${item.totalQty}</td>
                        <td>${item.qtyScraped}</td>
                        <td>${item.qtyFindOk}</td>
                        <td>${item.qtyWaitFind}</td>
                        <td style="background-color: ${statusBackgroundColor}; color: ${statusBackgroundColor === "#ffc107" ? "#000" : "#fff"};">
                            ${item.status}
                        </td>
                    </tr>
                `;
            };

            renderTableWithPagination(sortedData, resultDiv, tableHeaders, rowTemplate, "", "task-stock");
        } else {
            resultDiv.innerHTML = `
                <div class="alert alert-danger">
                    <strong>Lỗi:</strong> ${result.message}
                </div>
            `;
        }
    } catch (error) {
        resultDiv.innerHTML = `
            <div class="alert alert-danger">
                <strong>Lỗi:</strong> Không thể kết nối đến API. Vui lòng kiểm tra lại.
            </div>
        `;
        console.error("Error:", error);
    }
}

// Hàm gọi API và hiển thị bảng dữ liệu cho SEARCH_STATUS
// Hàm gọi API và hiển thị bảng dữ liệu cho SEARCH_STATUS
async function searchStatus(searchType, searchValues) {
    const resultDiv = document.getElementById("search-status-result");

    // Hiển thị thông báo "đang tải dữ liệu"
    resultDiv.innerHTML = `
        <div class="alert alert-info">
            <strong>Thông báo:</strong> Đang tìm kiếm dữ liệu...
        </div>
    `;

    const requestData = {
        internalTasks: searchType === "2" ? searchValues : [],
        sNs: searchType === "1" ? searchValues : [],
        taskNumber: searchType === "3" ? searchValues : []
    };

    try {
        // Gọi API /api/Scrap/detail-task-status
        const response = await fetch("http://10.220.130.119:9090/api/Scrap/detail-task-status", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(requestData)
        });

        const result = await response.json();

        if (response.ok) {
            // Sắp xếp dữ liệu theo CreateTime (từ cũ đến mới)
            const sortedData = result.data.sort((a, b) => new Date(a.createTime) - new Date(b.createTime));

            // Nếu tìm kiếm theo SN (searchType === "1"), so sánh danh sách SN gửi vào với SN trả về
            let unmatchedSNs = [];
            if (searchType === "1") {
                const returnedSNs = sortedData.map(item => item.sn);
                unmatchedSNs = searchValues.filter(sn => !returnedSNs.includes(sn));
            }

            // Chia dữ liệu thành 3 nhóm
            const noInternalTaskData = sortedData.filter(item => !item.internalTask || item.internalTask === "N/A");
            const hasInternalTaskNoTaskNumberData = sortedData.filter(item => item.internalTask && item.internalTask !== "N/A" && (!item.taskNumber || item.taskNumber === "N/A"));
            const hasTaskNumberData = sortedData.filter(item => item.taskNumber && item.taskNumber !== "N/A");

            // Định nghĩa tableHeaders không có cột checkbox
            const tableHeaders = `
                <th>SN</th>
                <th>Internal Task</th>
                <th>Description</th>
                <th>Approve Scrap Person</th>
                <th>KanBan Status</th>
                <th>Sloc</th>
                <th>Task Number</th>
                <th>PO</th>
                <th>Cost</th>
                <th>Remark</th>
                <th>Created By</th>
                <th>Create Time</th>
                <th>Apply Time</th>
                <th>Apply Task Status</th>
                <th>Find Board Status</th>
                <th>Purpose</th>
                <th>Category</th>
            `;

            // Định nghĩa rowTemplate không có cột checkbox
            const rowTemplate = (item) => `
                <tr>
                    <td>${item.sn ?? "N/A"}</td>
                    <td>${item.internalTask ?? "N/A"}</td>
                    <td>${item.description ?? "N/A"}</td>
                    <td>${item.approveScrapPerson ?? "N/A"}</td>
                    <td>${item.kanBanStatus ?? "N/A"}</td>
                    <td>${item.sloc ?? "N/A"}</td>
                    <td>${item.taskNumber ?? "N/A"}</td>
                    <td>${item.po ?? "N/A"}</td>
                    <td>${item.cost ?? "N/A"}</td>
                    <td>${item.remark ?? "N/A"}</td>
                    <td>${item.createdBy ?? "N/A"}</td>
                    <td>${item.createTime ?? "N/A"}</td>
                    <td>${item.applyTime ?? "N/A"}</td>
                    <td>${item.applyTaskStatus ?? "N/A"}</td>
                    <td>${item.findBoardStatus ?? "N/A"}</td>
                    <td>${item.purpose ?? "N/A"}</td>
                    <td>${item.category ?? "N/A"}</td>
                </tr>
            `;

            // Tạo HTML cho thông báo SN không tìm thấy (nếu có)
            let unmatchedSNsHtml = "";
            if (unmatchedSNs.length > 0) {
                unmatchedSNsHtml = `
                    <div class="alert alert-warning mt-3">
                        <strong>Cảnh báo:</strong> Có ${unmatchedSNs.length} SN không tồn tại trong ScrapList: ${unmatchedSNs.join(", ")}
                    </div>
                `;
            }

            // Xóa nội dung cũ và thêm các div con với ID duy nhất, bao gồm số lượng
            resultDiv.innerHTML = `
                <h6>Chưa có Internal Task (${noInternalTaskData.length} bản ghi)</h6>
                <div id="search-status-result-no-internal-task"></div>
                <h6>Đã có Internal Task nhưng chưa có Task Number (${hasInternalTaskNoTaskNumberData.length} bản ghi)</h6>
                <div id="search-status-result-has-internal-no-task-number"></div>
                <h6>Đã có Task Number (${hasTaskNumberData.length} bản ghi)</h6>
                <div id="search-status-result-has-task-number"></div>
                ${unmatchedSNsHtml}
            `;

            // Render từng bảng vào các div con
            const noInternalTaskDiv = document.getElementById("search-status-result-no-internal-task");
            const hasInternalTaskNoTaskNumberDiv = document.getElementById("search-status-result-has-internal-no-task-number");
            const hasTaskNumberDiv = document.getElementById("search-status-result-has-task-number");

            renderTableWithPagination(noInternalTaskData, noInternalTaskDiv, tableHeaders, rowTemplate, "", "no-internal-task");
            renderTableWithPagination(hasInternalTaskNoTaskNumberData, hasInternalTaskNoTaskNumberDiv, tableHeaders, rowTemplate, "", "has-internal-no-task-number");
            renderTableWithPagination(hasTaskNumberData, hasTaskNumberDiv, tableHeaders, rowTemplate, "", "has-task-number");

            // Lưu dữ liệu đã chia nhóm vào một biến toàn cục để sử dụng khi xuất Excel
            window.searchStatusData = {
                noInternalTaskData: noInternalTaskData.map(item => ({
                    SN: item.sn ?? "N/A",
                    InternalTask: item.internalTask ?? "N/A",
                    Description: item.description ?? "N/A",
                    ApproveScrapPerson: item.approveScrapPerson ?? "N/A",
                    KanBanStatus: item.kanBanStatus ?? "N/A",
                    Sloc: item.sloc ?? "N/A",
                    TaskNumber: item.taskNumber ?? "N/A",
                    PO: item.po ?? "N/A",
                    Cost: item.cost ?? "N/A",
                    Remark: item.remark ?? "N/A",
                    CreatedBy: item.createdBy ?? "N/A",
                    CreateTime: item.createTime ?? "N/A",
                    ApplyTime: item.applyTime ?? "N/A",
                    ApplyTaskStatus: item.applyTaskStatus ?? "N/A",
                    FindBoardStatus: item.findBoardStatus ?? "N/A",
                    Purpose: item.purpose ?? "N/A",
                    Category: item.category ?? "N/A"
                })),
                hasInternalTaskNoTaskNumberData: hasInternalTaskNoTaskNumberData.map(item => ({
                    SN: item.sn ?? "N/A",
                    InternalTask: item.internalTask ?? "N/A",
                    Description: item.description ?? "N/A",
                    ApproveScrapPerson: item.approveScrapPerson ?? "N/A",
                    KanBanStatus: item.kanBanStatus ?? "N/A",
                    Sloc: item.sloc ?? "N/A",
                    TaskNumber: item.taskNumber ?? "N/A",
                    PO: item.po ?? "N/A",
                    Cost: item.cost ?? "N/A",
                    Remark: item.remark ?? "N/A",
                    CreatedBy: item.createdBy ?? "N/A",
                    CreateTime: item.createTime ?? "N/A",
                    ApplyTime: item.applyTime ?? "N/A",
                    ApplyTaskStatus: item.applyTaskStatus ?? "N/A",
                    FindBoardStatus: item.findBoardStatus ?? "N/A",
                    Purpose: item.purpose ?? "N/A",
                    Category: item.category ?? "N/A"
                })),
                hasTaskNumberData: hasTaskNumberData.map(item => ({
                    SN: item.sn ?? "N/A",
                    InternalTask: item.internalTask ?? "N/A",
                    Description: item.description ?? "N/A",
                    ApproveScrapPerson: item.approveScrapPerson ?? "N/A",
                    KanBanStatus: item.kanBanStatus ?? "N/A",
                    Sloc: item.sloc ?? "N/A",
                    TaskNumber: item.taskNumber ?? "N/A",
                    PO: item.po ?? "N/A",
                    Cost: item.cost ?? "N/A",
                    Remark: item.remark ?? "N/A",
                    CreatedBy: item.createdBy ?? "N/A",
                    CreateTime: item.createTime ?? "N/A",
                    ApplyTime: item.applyTime ?? "N/A",
                    ApplyTaskStatus: item.applyTaskStatus ?? "N/A",
                    FindBoardStatus: item.findBoardStatus ?? "N/A",
                    Purpose: item.purpose ?? "N/A",
                    Category: item.category ?? "N/A"
                }))
            };
        } else {
            resultDiv.innerHTML = `
                <div class="alert alert-danger">
                    <strong>Lỗi:</strong> ${result.message}
                </div>
            `;
        }
    } catch (error) {
        resultDiv.innerHTML = `
            <div class="alert alert-danger">
                <strong>Lỗi:</strong> Không thể kết nối đến API. Vui lòng kiểm tra lại.
            </div>
        `;
        console.error("Error:", error);
    }
}

// Ẩn tất cả các form và khu vực kết quả ngay lập tức khi trang tải
hideAllElements();

// Xử lý sự kiện khi trang tải lần đầu
document.addEventListener("DOMContentLoaded", function () {
    console.log("DOMContentLoaded triggered");
    hideAllElements();

    // Xử lý sự kiện thay đổi giá trị trong dropdown
    document.getElementById("search-options").addEventListener("change", async function () {
        console.log("Dropdown changed to:", this.value);
        hideAllElements();

        // Xóa các Set khi chuyển đổi form để tránh trạng thái cũ
        selectedInternalTasks.clear();
        selectedSNs.clear();

        const selectedValue = this.value;

        if (selectedValue === "TASK_STOCK_STATUS") {
            document.getElementById("task-stock-status-form").classList.remove("hidden");
            document.getElementById("task-stock-status-result").classList.remove("hidden");

            // Gọi API và hiển thị bảng ngay khi chọn TASK_STOCK_STATUS
            await loadFindBoardStatus();
        } else if (selectedValue === "UPDATE_STATUS") {
            document.getElementById("update-status-form").classList.remove("hidden");
            document.getElementById("update-status-result").classList.remove("hidden");
        } else if (selectedValue === "SEARCH_STATUS") {
            document.getElementById("search-status-form").classList.remove("hidden");
            document.getElementById("search-status-result").classList.remove("hidden");
        }
    });

    // Xử lý sự kiện khi nhấn nút "Load List" trong TASK_STOCK_STATUS
    document.getElementById("task-stock-status-btn").addEventListener("click", async function () {
        const resultDiv = document.getElementById("task-stock-status-result");

        // Hiển thị thông báo "đang tải dữ liệu"
        resultDiv.innerHTML = `
        <div class="alert alert-info">
            <strong>Thông báo:</strong> Đang tải dữ liệu để xuất Excel...
        </div>
    `;

        try {
            // Gọi API để lấy dữ liệu FindBoardStatus
            const response = await fetch("http://10.220.130.119:9090/api/Scrap/get-find-board-status", {
                method: "GET",
                headers: {
                    "Content-Type": "application/json"
                }
            });

            const result = await response.json();

            if (response.ok) {
                // Sắp xếp dữ liệu theo CreateTime (từ cũ đến mới)
                const sortedData = result.sort((a, b) => new Date(a.createTime) - new Date(b.createTime));

                // Chuyển dữ liệu thành định dạng cho file Excel
                const excelData = sortedData.map(item => ({
                    "Internal Task": item.internalTask ?? "N/A",
                    "Approve Scrap Person": item.approveScrapPerson ?? "N/A",
                    "Description": item.description ?? "N/A",
                    "After/Before": item.afterBefore ?? "N/A",
                    "Create Time": item.createTime ?? "N/A",
                    "Total Q'ty": item.totalQty ?? "N/A",
                    "Qty Scraped": item.qtyScraped ?? "N/A",
                    "Qty Find Ok": item.qtyFindOk ?? "N/A",
                    "Qty Wait Find": item.qtyWaitFind ?? "N/A",
                    "Status": item.status ?? "N/A"
                }));

                // Tạo và tải xuống file Excel
                const now = new Date();
                const filename = `TaskStockStatus_${now.toISOString().replace(/[:.]/g, '-')}.xlsx`;
                const workbook = XLSX.utils.book_new();
                const worksheet = XLSX.utils.json_to_sheet(excelData);
                XLSX.utils.book_append_sheet(workbook, worksheet, "TaskStockStatus");
                XLSX.writeFile(workbook, filename);

                // Hiển thị thông báo thành công
                resultDiv.innerHTML = `
                <div class="alert alert-success">
                    <strong>Thành công:</strong> File Excel đã được tải xuống.
                </div>
            `;
            } else {
                resultDiv.innerHTML = `
                <div class="alert alert-danger">
                    <strong>Lỗi:</strong> ${result.message}
                </div>
            `;
            }
        } catch (error) {
            resultDiv.innerHTML = `
            <div class="alert alert-danger">
                <strong>Lỗi:</strong> Không thể kết nối đến API. Vui lòng kiểm tra lại.
            </div>
        `;
            console.error("Error:", error);
        }
    });

    // Xử lý sự kiện khi nhấn nút "Update" trong form UPDATE_STATUS
    document.getElementById("update-status-btn").addEventListener("click", async function () {
        const resultDiv = document.getElementById("update-status-result");

        // Lấy danh sách SN từ textarea
        const snInput = document.getElementById("sn-status-update").value.trim();
        const sNs = snInput.split(/\r?\n/).map(sn => sn.trim()).filter(sn => sn);

        // Lấy trạng thái từ dropdown
        const status = document.getElementById("status-options").value;

        // Kiểm tra dữ liệu đầu vào
        if (!sNs.length) {
            resultDiv.innerHTML = `
                <div class="alert alert-warning">
                    <strong>Cảnh báo:</strong> Vui lòng nhập ít nhất một Serial Number hợp lệ.
                </div>
            `;
            return;
        }

        if (!status || (status !== "1" && status !== "2")) {
            resultDiv.innerHTML = `
                <div class="alert alert-warning">
                    <strong>Cảnh báo:</strong> Vui lòng chọn trạng thái hợp lệ (Đã tìm thấy hoặc Đã chuyển kho phế).
                </div>
            `;
            return;
        }

        // Hiển thị thông báo "đang xử lý"
        resultDiv.innerHTML = `
            <div class="alert alert-info">
                <strong>Thông báo:</strong> Đang cập nhật trạng thái...
            </div>
        `;

        const requestData = {
            sNs: sNs,
            status: status
        };

        try {
            // Gọi API /api/Scrap/update-status-find-board
            const response = await fetch("http://10.220.130.119:9090/api/Scrap/update-status-find-board", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json"
                },
                body: JSON.stringify(requestData)
            });

            const result = await response.json();

            if (response.ok) {
                resultDiv.innerHTML = `
                    <div class="alert alert-success">
                        <strong>Thành công:</strong> ${result.message}
                    </div>
                `;
            } else {
                resultDiv.innerHTML = `
                    <div class="alert alert-danger">
                        <strong>Lỗi:</strong> ${result.message}
                    </div>
                `;
            }
        } catch (error) {
            resultDiv.innerHTML = `
                <div class="alert alert-danger">
                    <strong>Lỗi:</strong> Không thể kết nối đến API. Vui lòng kiểm tra lại.
                </div>
            `;
            console.error("Error:", error);
        }
    });

    // Xử lý sự kiện khi nhấn nút "Search" trong form SEARCH_STATUS
    document.getElementById("search-status-btn").addEventListener("click", async function () {
        const resultDiv = document.getElementById("search-status-result");

        // Lấy danh sách SN hoặc InternalTask từ textarea
        const searchInput = document.getElementById("search-status-update").value.trim();
        const searchValues = searchInput.split(/\r?\n/).map(value => value.trim()).filter(value => value);

        // Lấy loại tìm kiếm từ dropdown
        const searchType = document.getElementById("search-status-options").value;

        // Kiểm tra dữ liệu đầu vào
        if (!searchValues.length) {
            resultDiv.innerHTML = `
                <div class="alert alert-warning">
                    <strong>Cảnh báo:</strong> Vui lòng nhập ít nhất một SN hoặc Internal Task hợp lệ.
                </div>
            `;
            return;
        }

        if (!searchType || (searchType !== "1" && searchType !== "2" && searchType !== "3")) {
            resultDiv.innerHTML = `
                <div class="alert alert-warning">
                    <strong>Cảnh báo:</strong> Vui lòng chọn loại tìm kiếm hợp lệ (Tìm kiếm theo SN hoặc Internal Task).
                </div>
            `;
            return;
        }

        // Gọi hàm tìm kiếm
        await searchStatus(searchType, searchValues);
    });

    // Xử lý sự kiện khi nhấn nút "Load List" trong form SEARCH_STATUS
    document.getElementById("load-status-btn").addEventListener("click", function () {
        const resultDiv = document.getElementById("search-status-result");

        // Kiểm tra xem dữ liệu đã được lưu từ searchStatus chưa
        if (!window.searchStatusData) {
            resultDiv.innerHTML = `
                <div class="alert alert-warning">
                    <strong>Cảnh báo:</strong> Vui lòng thực hiện tìm kiếm trước khi xuất Excel.
                </div>
            `;
            return;
        }

        // Hiển thị thông báo "đang tải dữ liệu"
        resultDiv.innerHTML = `
            <div class="alert alert-info">
                <strong>Thông báo:</strong> Đang tạo file Excel...
            </div>
        `;

        try {
            const { noInternalTaskData, hasInternalTaskNoTaskNumberData, hasTaskNumberData } = window.searchStatusData;

            // Tạo và tải xuống file Excel
            const now = new Date();
            const filename = `SearchStatusDetails_${now.toISOString().replace(/[:.]/g, '-')}.xlsx`;
            exportToExcel(noInternalTaskData, hasInternalTaskNoTaskNumberData, hasTaskNumberData, filename);

            // Hiển thị thông báo thành công
            resultDiv.innerHTML = `
                <div class="alert alert-success">
                    <strong>Thành công:</strong> File Excel đã được tải xuống với 3 sheet: Chưa có Internal Task, Đã có Internal Task nhưng chưa có Task Number, Đã có Task Number.
                </div>
            `;
        } catch (error) {
            resultDiv.innerHTML = `
                <div class="alert alert-danger">
                    <strong>Lỗi:</strong> Không thể tạo file Excel. Vui lòng thử lại.
                </div>
            `;
            console.error("Error:", error);
        }
    });
});