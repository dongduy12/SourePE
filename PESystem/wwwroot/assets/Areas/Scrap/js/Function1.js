// Hàm để ẩn tất cả các form và khu vực kết quả
function hideAllElements() {
    const forms = ["input-sn-1-form", "custom-form"];
    const results = ["input-sn-1-result", "sn-wait-approve-result"];

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
    const snInput = document.getElementById("sn-input-1");
    const descriptionInput = document.getElementById("description-input-1");

    if (snInput) snInput.value = "";
    if (descriptionInput) descriptionInput.value = "";
}

// Ẩn tất cả các form và khu vực kết quả ngay lập tức khi trang tải
hideAllElements();

/** 🔥 Lấy currentUsername từ thẻ HTML */
function getCurrentUsername() {
    const usernameElement = document.querySelector(".d-none.d-md-block.ps-2");
    return usernameElement ? usernameElement.textContent.trim() : null;
}

// Hàm hiển thị bảng với phân trang
function displayTableWithPagination(data, resultDiv, itemsPerPage = 10) {
    if (!data || data.length === 0) {
        resultDiv.innerHTML = `
            <div class="alert alert-warning">
                <strong>Cảnh báo:</strong> Không tìm thấy dữ liệu với ApplyTaskStatus = 2.
            </div>
        `;
        return;
    }

    // Tính toán phân trang
    const totalItems = data.length;
    const totalPages = Math.ceil(totalItems / itemsPerPage);
    let currentPage = 1;

    // Hàm hiển thị dữ liệu cho trang hiện tại
    function renderPage(page) {
        const start = (page - 1) * itemsPerPage;
        const end = start + itemsPerPage;
        const pageData = data.slice(start, end);

        let tableHTML = `
            <div class="table-container">
                <table>
                    <thead>
                        <tr>
                            <th>SN</th>
                            <th>Description</th>
                            <th>Create Time</th>
                            <th>Apply Task Status</th>
                            <th>Type Bonepile</th>
                            <th>Create By</th>
                        </tr>
                    </thead>
                    <tbody>
        `;

        // Thêm dữ liệu vào bảng
        pageData.forEach(item => {
            tableHTML += `
                <tr>
                    <td>${item.sn || "N/A"}</td>
                    <td>${item.description || "N/A"}</td>
                    <td>${item.createTime || "N/A"}</td>
                    <td>${item.applyTaskStatus}</td>
                    <td>${item.remark}</td>
                    <td>${item.createBy || "N/A"}</td>
                </tr>
            `;
        });

        tableHTML += `
                    </tbody>
                </table>
            </div>
        `;

        // Thêm phân trang
        if (totalPages > 1) {
            tableHTML += `
                <div class="pagination">
                    <button id="prev-page" ${currentPage === 1 ? "disabled" : ""}>Previous</button>
                    <span>Page ${currentPage} of ${totalPages}</span>
                    <button id="next-page" ${currentPage === totalPages ? "disabled" : ""}>Next</button>
                </div>
            `;
        }

        resultDiv.innerHTML = tableHTML;

        // Gắn sự kiện cho nút Previous và Next
        if (totalPages > 1) {
            document.getElementById("prev-page").addEventListener("click", () => {
                if (currentPage > 1) {
                    currentPage--;
                    renderPage(currentPage);
                }
            });

            document.getElementById("next-page").addEventListener("click", () => {
                if (currentPage < totalPages) {
                    currentPage++;
                    renderPage(currentPage);
                }
            });
        }
    }

    // Hiển thị trang đầu tiên
    renderPage(currentPage);
}

// Hàm tải dữ liệu từ API và hiển thị
async function loadScrapStatusTwo(resultDiv) {
    resultDiv.innerHTML = `
        <div class="alert alert-info">
            <strong>Thông báo:</strong> Đang tải danh sách SN chờ SPE approve...
        </div>
    `;

    try {
        const response = await fetch("http://10.220.130.119:9090/api/Scrap/get-scrap-status-two", {
            method: "GET",
            headers: {
                "Content-Type": "application/json"
            }
        });

        const result = await response.json();

        if (response.ok) {
            displayTableWithPagination(result, resultDiv);
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

// Hàm tải file Excel
function downloadExcel(data) {
    // Chuẩn bị dữ liệu cho file Excel
    const worksheetData = data.map(item => ({
        SN: item.sn || "N/A",
        Description: item.description || "N/A",
        "Create Time": item.createTime || "N/A",
        "Apply Task Status": item.applyTaskStatus,
        "Create By": item.createBy || "N/A"
    }));

    // Tạo worksheet
    const worksheet = XLSX.utils.json_to_sheet(worksheetData);
    const workbook = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(workbook, worksheet, "SN_Wait_SPE_Approve");

    // Tải file Excel
    XLSX.writeFile(workbook, "SN_Wait_SPE_Approve.xlsx");
}

// Xử lý sự kiện khi trang tải lần đầu
document.addEventListener("DOMContentLoaded", function () {
    console.log("DOMContentLoaded triggered for Function1");
    hideAllElements();

    // Xử lý sự kiện thay đổi giá trị trong dropdown
    document.getElementById("search-options").addEventListener("change", function () {
        console.log("Dropdown changed to:", this.value);
        hideAllElements();

        const selectedValue = this.value;

        if (selectedValue === "INPUT_SN_1") {
            document.getElementById("input-sn-1-form").classList.remove("hidden");
            document.getElementById("input-sn-1-result").classList.remove("hidden");
        } else if (selectedValue === "SN_WAIT_SPE_APPROVE") {
            const resultDiv = document.getElementById("sn-wait-approve-result");
            document.getElementById("custom-form").classList.remove("hidden");
            document.getElementById("sn-wait-approve-result").classList.remove("hidden");

            // Tải dữ liệu ngay khi form được hiển thị
            loadScrapStatusTwo(resultDiv);
        }
    });

    // Xử lý sự kiện khi nhấn nút "INPUT SN" trong form INPUT_SN_1
    document.getElementById("input-sn-btn").addEventListener("click", async function () {
        const resultDiv = document.getElementById("input-sn-1-result");

        // Lấy danh sách SN từ textarea
        const snInput = document.getElementById("sn-input-1").value.trim();
        const sNs = snInput.split(/\r?\n/).map(sn => sn.trim()).filter(sn => sn);

        // Lấy mô tả từ input
        const description = document.getElementById("description-input-1").value.trim();

        // Lấy thông tin loại Bonepile
        const typeBonepile = document.getElementById("bp-options").value;

        // Lấy thông tin loại Approve
        const typeApprove = document.getElementById("approve-options").value;

        // Lấy thông tin người dùng hiện tại
        const createdBy = getCurrentUsername();

        // Kiểm tra dữ liệu đầu vào
        if (!sNs.length) {
            resultDiv.innerHTML = `
            <div class="alert alert-warning">
                <strong>Cảnh báo:</strong> Vui lòng nhập ít nhất một Serial Number hợp lệ!
            </div>
        `;
            return;
        }

        if (!description) {
            resultDiv.innerHTML = `
            <div class="alert alert-warning">
                <strong>Cảnh báo:</strong> Vui lòng nhập mô tả!
            </div>
        `;
            return;
        }

        if (!["BP-10", "BP-20"].includes(typeBonepile)) {
            resultDiv.innerHTML = `
            <div class="alert alert-warning">
                <strong>Cảnh báo:</strong> Vui lòng chọn loại BonePile!
            </div>
        `;
            return;
        }

        if (!["2", "4"].includes(typeApprove)) {
            resultDiv.innerHTML = `
            <div class="alert alert-warning">
                <strong>Cảnh báo:</strong> Vui lòng chọn loại Approve!
            </div>
        `;
            return;
        }

        // Hiển thị thông báo "đang xử lý"
        resultDiv.innerHTML = `
        <div class="alert alert-info">
            <strong>Thông báo:</strong> Đang lưu danh sách SN...
        </div>
    `;

        // Định dạng sn_list thành chuỗi cách nhau bởi dấu phẩy
        const snListString = sNs.join(",");

        try {
            // Gọi API /api/Scrap/input-sn-wait-spe-approve trước
            const requestData = {
                sNs: sNs,
                description: description,
                remark: typeBonepile,
                approve: typeApprove,
                createdBy: createdBy
            };

            const inputSnResponse = await fetch("http://10.220.130.119:9090/api/Scrap/input-sn-wait-spe-approve", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json"
                },
                body: JSON.stringify(requestData)
            });

            const inputSnResult = await inputSnResponse.json();

            if (inputSnResponse.ok) {
                // Gọi API https://sfc-portal.cns.myfiinet.com/SfcSmartRepair/api/repair_scrap
                const repairScrapData = {
                    type: "insert",
                    sn_list: snListString,
                    type_bp: typeBonepile,
                    status: typeApprove,
                    task: null
                };

                const repairScrapResponse = await fetch("https://sfc-portal.cns.myfiinet.com/SfcSmartRepair/api/repair_scrap", {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json"
                    },
                    body: JSON.stringify(repairScrapData)
                });

                const repairScrapResult = await repairScrapResponse.text();

                if (repairScrapResponse.ok && repairScrapResult === "\"OK\"") {
                    // Gọi API /api/Product/UpdateScrap
                    const updateResponse = await fetch("http://10.220.130.119:9090/api/Product/UpdateScrap", {
                        method: "PUT",
                        headers: {
                            "Content-Type": "application/json"
                        },
                        body: JSON.stringify({
                            serialNumbers: sNs,
                            scrapStatus: "Đã gửi NV xin báo phế"
                        })
                    });

                    const updateResult = await updateResponse.json();

                    if (updateResponse.ok) {
                        // Nếu cả ba API đều thành công
                        resultDiv.innerHTML = `
                        <div class="alert alert-success">
                            <strong>Thành công:</strong> ${inputSnResult.message}
                        </div>
                    `;
                    } else {
                        console.warn("UpdateProduct API failed:", updateResult.message);
                        resultDiv.innerHTML = `
                        <div class="alert alert-warning">
                            <strong>Cảnh báo:</strong> Lưu SN và repair_scrap thành công nhưng cập nhật sản phẩm trong kho thất bại: ${updateResult.message}
                        </div>
                    `;
                        return;
                    }
                } else {
                    console.warn("repair_scrap API failed:", repairScrapResult);
                    resultDiv.innerHTML = `
                    <div class="alert alert-warning">
                        <strong>Cảnh báo:</strong> Gọi API repair_scrap thất bại: ${repairScrapResult}
                    </div>
                `;
                    return;
                }
            } else {
                resultDiv.innerHTML = `
                <div class="alert alert-danger">
                    <strong>Lỗi:</strong> ${inputSnResult.message}
                </div>
            `;
                return;
            }
        } catch (error) {
            resultDiv.innerHTML = `
            <div class="alert alert-danger">
                <strong>Lỗi:</strong> Không thể kết nối đến API. Vui lòng kiểm tra lại.
            </div>
        `;
            console.error("Error:", error);
            return;
        }
    });


    // Xử lý sự kiện khi nhấn nút "Download Excel" trong form SN_WAIT_SPE_APPROVE
    document.getElementById("sn-wait-list-btn").addEventListener("click", async function () {
        try {
            // Gọi API để lấy toàn bộ dữ liệu
            const response = await fetch("http://10.220.130.119:9090/api/Scrap/get-scrap-status-two-and-four", {
                method: "GET",
                headers: {
                    "Content-Type": "application/json"
                }
            });

            const result = await response.json();

            if (response.ok && result && result.length > 0) {
                // Tải file Excel
                downloadExcel(result);
            } else {
                const resultDiv = document.getElementById("sn-wait-approve-result");
                resultDiv.innerHTML = `
                    <div class="alert alert-warning">
                        <strong>Cảnh báo:</strong> Không có dữ liệu để tải xuống.
                    </div>
                `;
            }
        } catch (error) {
            const resultDiv = document.getElementById("sn-wait-approve-result");
            resultDiv.innerHTML = `
                <div class="alert alert-danger">
                    <strong>Lỗi:</strong> Không thể tải dữ liệu để tạo file Excel.
                </div>
            `;
            console.error("Error:", error);
        }
    });
});