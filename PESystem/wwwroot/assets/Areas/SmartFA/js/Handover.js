const config = {
    apiBaseUrl: "http://10.220.130.119:9090",
    endpoints: {
        getStatusCounts: "/api/SearchFA/get-status-counts",
        search: "/api/SearchFA/search",
        getFullnameBatch: "/api/SearchFA/get-fullname-batch",
        handOverStatus: "/api/RepairStatus/hand-over-status",
        receivingStatus: "/api/RepairStatus/receiving-status",
        repairStatus: "/api/RepairStatus/repair-status",
        getAllowedAreas: "/api/SearchFA/get-allowed-areas",
        getCheckPoints: "/api/FixGuide/GetCheckPoints"
    },
    statuses: {
        thayLieu: "THAY LIỆU",
        fa: "FA",
        retest: "RETEST",
        checkList: "CHECK_LIST",
        vi: "VI-RE"
    }
};

// Hàm gọi API chung
async function fetchApi(endpoint, payload, method = "POST") {
    try {
        const response = await fetch(`${config.apiBaseUrl}${endpoint}`, {
            method,
            headers: { "Content-Type": "application/json" },
            body: method === "POST" ? JSON.stringify(payload) : null
        });
        if (!response.ok) throw new Error(`API call failed: ${response.status}`);
        return await response.json();
    } catch (error) {
        console.error(`Error calling ${endpoint}:`, error);
        throw error;
    }
}

// Hàm hiển thị modal nhập vị trí
function showLocationModal(callback) {
    const modalHtml = `
        <div class="modal fade" id="locationModal" tabindex="-1">
            <div class="modal-dialog">
                <div class="modal-content">
                    <div class="modal-header">
                        <h5 class="modal-title">Nhập vị trí</h5>
                        <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                    </div>
                    <div class="modal-body">
                        <input type="text" id="locationInput" class="form-control" placeholder="Vị trí (Location)">
                    </div>
                    <div class="modal-footer">
                        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Hủy</button>
                        <button type="button" class="btn btn-primary" id="confirmLocation">Xác nhận</button>
                    </div>
                </div>
            </div>
        </div>
    `;

    const existingModal = document.getElementById("locationModal");
    if (existingModal) existingModal.remove();

    document.body.insertAdjacentHTML("beforeend", modalHtml);
    const modal = new bootstrap.Modal(document.getElementById("locationModal"));
    modal.show();

    document.getElementById("confirmLocation").addEventListener("click", () => {
        const location = document.getElementById("locationInput").value.trim();
        modal.hide();
        callback(location);
    });
}

// Hàm hiển thị modal hướng dẫn sửa chữa
function showRepairGuideModal(pdfUrl, downloadFileName) {
    const modalHtml = `
        <div class="modal fade" id="repairGuideModal" tabindex="-1">
            <div class="modal-dialog modal-lg">
                <div class="modal-content">
                    <div class="modal-header">
                        <h5 class="modal-title">Hướng Dẫn Sửa Chữa</h5>
                        <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                    </div>
                    <div class="modal-body">
                        <p><strong>Serial Number:</strong> <span id="modal-sn"></span></p>
                        <p><strong>Model:</strong> <span id="modal-model"></span></p>
                        <p><strong>Product Line:</strong> <span id="modal-product-line"></span></p>
                        <p><strong>CheckPoints:</strong></p>
                        <ul id="modal-checkpoints"></ul>
                        <p><strong>Chi tiết:</strong> <span id="modal-repair-detail"></span></p>
                        <embed id="modal-product-line-pdf" width="100%" height="400px" type="application/pdf">
                    </div>
                    <div class="modal-footer">
                        <a id="download-pdf-btn" href="${pdfUrl}" download="${downloadFileName}" class="btn btn-primary">Tải PDF</a>
                        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Đóng</button>
                    </div>
                </div>
            </div>
        </div>
    `;

    const existingModal = document.getElementById("repairGuideModal");
    if (existingModal) existingModal.remove();

    document.body.insertAdjacentHTML("beforeend", modalHtml);
    return new bootstrap.Modal(document.getElementById("repairGuideModal"));
}

// Hàm vẽ biểu đồ
async function drawChart(canvasId, apiPayload, chartTitle, barColor, chartType) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) {
        console.error(`Không tìm thấy canvas với ID '${canvasId}'`);
        return;
    }

    const ctx = canvas.getContext("2d");
    const result = await fetchApi(config.endpoints.getStatusCounts, apiPayload);
    if (result.success) {
        const data = result.data;
        const labels = data.map(item => item.status);
        const counts = data.map(item => item.count);

        new Chart(ctx, {
            type: "bar",
            data: {
                labels,
                datasets: [{
                    label: "Số lượng trạng thái",
                    data: counts,
                    backgroundColor: barColor,
                    borderColor: barColor.replace("0.8", "1"),
                    borderWidth: 1
                }]
            },
            options: {
                responsive: true,
                plugins: {
                    legend: { display: true, position: "top" },
                    title: { display: true, text: chartTitle },
                    datalabels: {
                        anchor: "end",
                        align: "end",
                        color: "black",
                        font: { weight: "bold", size: 12 },
                        formatter: value => value
                    }
                },
                scales: { y: { beginAtZero: true } },
                onClick: async (event, elements) => {
                    if (elements.length > 0) {
                        const index = elements[0].index;
                        const selectedStatus = labels[index];
                        await handleChartClick(selectedStatus, "", chartType);
                    }
                }
            },
            plugins: [ChartDataLabels]
        });
    }
}

// Hàm xử lý click biểu đồ
async function handleChartClick(selectedStatus, handoverStatus, chartType) {
    const payload = {
        serialNumbers: [],
        modelName: "",
        testCode: "",
        status: selectedStatus,
        data1: "",
        handoverStatus
    };

    try {
        const result = await fetchApi(config.endpoints.search, payload);
        if (result.success) {
            const filteredData = chartType === "online"
                ? result.data.filter(item => item.datA18 !== null && item.datA18 !== "TRONG_KHO" && item.datA13 !== "WAITING_HAND_OVER")
                : result.data;
            updateModalSNTable(filteredData);
            new bootstrap.Modal(document.getElementById("statusModal")).show();
        } else {
            showError(`Không thể tìm thấy dữ liệu cho trạng thái: ${selectedStatus}`);
        }
    } catch (error) {
        showError("Lỗi khi gọi API!");
    }
}

// Hàm cập nhật bảng modal
function updateModalSNTable(data) {
    const table = document.querySelector("#modal-sn-table");
    if (!table) {
        console.error("Không tìm thấy bảng với ID 'modal-sn-table'");
        return;
    }

    if ($.fn.DataTable.isDataTable("#modal-sn-table")) {
        $("#modal-sn-table").DataTable().destroy();
    }

    const tableBody = table.querySelector("tbody") || table.appendChild(document.createElement("tbody"));
    tableBody.innerHTML = "";

    data.forEach(item => {
        const row = document.createElement("tr");
        row.innerHTML = `
            <td title="${item.seriaL_NUMBER || ''}">${truncateText(item.seriaL_NUMBER, 20)}</td>
            <td title="${item.modeL_NAME || ''}">${truncateText(item.modeL_NAME, 20)}</td>
            <td title="${item.tesT_GROUP || ''}">${truncateText(item.tesT_GROUP, 20)}</td>
            <td title="${item.tesT_CODE || ''}">${truncateText(item.tesT_CODE, 20)}</td>
            <td title="${item.datA1 || ''}">${truncateText(item.datA1, 20)}</td>
            <td>${item.datA12 || ""}</td>
            <td title="${item.datA11 || ''}">${truncateText(item.datA11, 20)}</td>
            <td>${item.tester || ""}</td>
            <td title="${item.datE3 || ''}">${truncateText(item.datE3, 20)}</td>
            <td>${item.datA13 || ""}</td>
            <td>${item.datA18 || ""}</td>
        `;
        tableBody.appendChild(row);
    });

    $("#modal-sn-table").DataTable({
        destroy: true,
        paging: true,
        searching: true,
        ordering: true,
        fixedColumns: {
            leftColumns: 1 // Ghim cột đầu tiên
        },
        language: {
            search: "Tìm kiếm:",
            lengthMenu: "Hiển thị _MENU_ dòng",
            info: "Hiển thị _START_ đến _END_ của _TOTAL_ dòng",
            paginate: { first: "Đầu", last: "Cuối", next: "Tiếp", previous: "Trước" }
        }
    });
}

// Hàm vẽ bảng Owner
async function drawOwnerTable() {
    try {
        const result = await fetchApi(config.endpoints.search, {
            serialNumbers: [],
            modelName: "",
            testCode: "",
            status: "",
            data1: "",
            handoverStatus: ""
        });

        if (!result.success) {
            showError(`Không thể lấy dữ liệu từ API: ${result.message}`);
            return;
        }

        const filteredData = result.data.filter(item => item.datA18 !== null && item.datA18 !== "TRONG_KHO" && item.datA13 !== "WAITING_HAND_OVER");

        if (!filteredData.length) {
            showError("No data!");
            return;
        }

        const ownerCodes = [...new Set(filteredData.map(item => item.tester).filter(Boolean))];
        const ownerFullNames = (await fetchApi(config.endpoints.getFullnameBatch, ownerCodes)).data || {};

        const ownerSummary = {};
        filteredData.forEach(item => {
            const ownerCode = item.tester;
            if (!ownerCode) return;
            const fullName = ownerFullNames[ownerCode] || ownerCode;
            if (fullName && fullName.trim() !== ownerCode) {
                const status = item.datA11 || "N/A";
                ownerSummary[fullName] = ownerSummary[fullName] || {};
                ownerSummary[fullName][status] = (ownerSummary[fullName][status] || 0) + 1;
            }
        });

        if (Object.keys(ownerSummary).length === 0) {
            showError("Không có dữ liệu Owner có FullName!");
            return;
        }

        const tableHtml = `
            <table class="table table-bordered">
                <thead><tr><th>Owner (FullName)</th><th>Trạng thái (Online)</th><th>Số lượng</th></tr></thead>
                <tbody>
                    ${Object.entries(ownerSummary).map(([fullName, statuses]) =>
            Object.entries(statuses).map(([status, count]) =>
                `<tr><td class="clickable-owner">${fullName}</td><td>${status}</td><td>${count}</td></tr>`
            ).join("")
        ).join("")}
                </tbody>
            </table>
        `;

        document.getElementById("OwnerTableContainer").innerHTML = tableHtml;

        document.querySelectorAll(".clickable-owner").forEach(cell => {
            cell.addEventListener("click", () => {
                const fullName = cell.textContent.trim();
                const ownerCode = Object.keys(ownerFullNames).find(code => ownerFullNames[code] === fullName) || fullName;
                const ownerData = filteredData.filter(item => item.tester === ownerCode);
                if (ownerData.length) {
                    updateModalSNTable(ownerData);
                    new bootstrap.Modal(document.getElementById("statusModal")).show();
                } else {
                    showError("No data!");
                }
            });
        });
    } catch (error) {
        console.error("Lỗi khi vẽ bảng Owner:", error);
    }
}

// Hàm xử lý Giao Bản
async function handleGiaoBan() {
    const table = $("#sn-table").DataTable();
    const allData = table.rows().data().toArray();
    const currentUser = $("#analysisPerson").val();

    const { allowedAreas } = await checkPermissions(currentUser, allData);
    const unauthorizedRecords = allData.filter(row => {
        const status = row[5]?.trim() || "";
        return !allowedAreas.includes(status);
    });
    if (unauthorizedRecords.length > 0) {
        showWarning("Bạn không có quyền giao bản cho một hoặc nhiều trạng thái!");
        return;
    }

    const unauthorizedRecordsByUser = allData.filter(row => row[6]?.trim() !== currentUser);
    if (unauthorizedRecordsByUser.length > 0) {
        showWarning("Bạn không có quyền!");
        return;
    }

    const serialNumbers = allData.map(row => row[0]?.trim()).filter(sn => sn);
    if (!serialNumbers.length) {
        showWarning("Không có dữ liệu hợp lệ để gửi!");
        return;
    }

    const payload = {
        serialnumbers: serialNumbers.join(","),
        handOverStatus: "WAITING_HAND_OVER",
        tag: "Giao"
    };

    try {
        const result = await fetchApi(config.endpoints.handOverStatus, payload);
        showInfo(`Cập nhật trạng thái bàn giao: ${result.message}`);
        if (result.message.replace(/"/g, "").trim() === "OK") {
            const snsToRemove = allData.map(row => row[0]?.trim());
            snsToRemove.forEach(sn => existingSNs.delete(sn));
            table.rows().remove().draw();
        }
    } catch (error) {
        showError("Lỗi khi gọi API!");
    }
}

// Hàm xử lý Nhận Bản
async function handleNhanBan() {
    const table = $("#sn-table").DataTable();
    const allData = table.rows().data().toArray();
    const currentUser = $("#analysisPerson").val();

    const { allowedAreas } = await checkPermissions(currentUser, allData);
    const unauthorizedRecords = allData.filter(row => {
        const status = row[5]?.trim() || "";
        return !allowedAreas.includes(status);
    });
    if (unauthorizedRecords.length > 0) {
        showWarning("Bạn không có quyền nhận bản cho một hoặc nhiều trạng thái!");
        return;
    }

    showLocationModal(async (location) => {
        if (!location) {
            showWarning("Vui lòng nhập vị trí (Location)!");
            return;
        }

        const serialNumbers = allData.map(row => row[0]?.trim()).filter(sn => sn);
        if (!serialNumbers.length) {
            showError("Không có dữ liệu hợp lệ để gửi!");
            return;
        }

        const payload = {
            serialnumbers: serialNumbers.join(","),
            owner: currentUser,
            location,
            tag: "Nhận"
        };

        try {
            const result = await fetchApi(config.endpoints.receivingStatus, payload);
            showInfo(`Trạng thái nhận bàn giao: ${result.message}`);
            if (result.message.replace(/"/g, "").trim() === "OK") {
                const snsToRemove = allData.map(row => row[0]?.trim());
                snsToRemove.forEach(sn => existingSNs.delete(sn));
                table.rows().remove().draw();
            }
        } catch (error) {
            showError("Lỗi khi gọi API!");
        }
    });
}

// Hàm kiểm tra quyền hạn
async function checkPermissions(currentUser, allData) {
    const result = await fetchApi(config.endpoints.getAllowedAreas, { cardCode: currentUser });
    if (!result.success) throw new Error("Không thể lấy quyền truy cập.");

    const allowedAreas = result.allowedAreas.split(",").map(area => area.trim());
    const unauthorizedRecords = allData.filter(row => {
        const status = row[5]?.trim();
        return !allowedAreas.includes(status);
    });

    return { allowedAreas, unauthorizedRecords };
}

// Hàm cắt chuỗi
function truncateText(text, maxLength) {
    return text && text.length > maxLength ? text.substring(0, maxLength) + "..." : text || "";
}

// Hàm chuẩn hóa ProductLine
function standardizeProductLine(productLine) {
    return productLine ? productLine.replace(/\s|-/g, '').toUpperCase() : '';
}

// Hàm xử lý click nút Chi tiết trong cột HƯỚNG_DẪN
function handleViewRepairGuide(event) {
    const button = event.currentTarget;
    const serialNumber = button.dataset.sn;
    const modelName = button.dataset.model;
    const productLine = button.dataset.productLine;
    const checkpoints = JSON.parse(decodeURIComponent(button.dataset.checkpoints));
    const detail = decodeURIComponent(button.dataset.detail);

    const standardizedProductLine = standardizeProductLine(productLine);
    const pdfUrl = `/pdfs/product-lines/${standardizedProductLine}.pdf`;
    const downloadFileName = `${standardizedProductLine}_RepairGuide.pdf`;

    const modal = showRepairGuideModal(pdfUrl, downloadFileName);
    document.getElementById("modal-sn").textContent = serialNumber;
    document.getElementById("modal-model").textContent = modelName;
    document.getElementById("modal-product-line").textContent = productLine;

    const checkpointsList = document.getElementById("modal-checkpoints");
    checkpointsList.innerHTML = "";
    checkpoints.forEach(cp => {
        const li = document.createElement("li");
        li.textContent = cp;
        checkpointsList.appendChild(li);
    });

    document.getElementById("modal-repair-detail").textContent = detail;

    const pdfEmbed = document.getElementById("modal-product-line-pdf");
    pdfEmbed.src = pdfUrl;
    pdfEmbed.onerror = () => {
        pdfEmbed.src = '/pdfs/product-lines/default.pdf';
        document.getElementById("download-pdf-btn").href = '/pdfs/product-lines/default.pdf';
        document.getElementById("download-pdf-btn").download = 'Default_RepairGuide.pdf';
    };

    modal.show();
}

// Khởi tạo khi DOM sẵn sàng
document.addEventListener("DOMContentLoaded", async () => {
    try {
        await drawChart("statusChart", "HANDOVER", "Số Lượng Bản Waiting Handover", "rgba(75, 192, 192, 0.8)", "handover");
        await drawChart("onlineChart", "ONLINE", "Số Lượng Bản Online", "rgba(54, 162, 235, 0.8)", "online");
        await drawOwnerTable();

        document.getElementById("GiaoBan").addEventListener("click", handleGiaoBan);
        document.getElementById("NhanBan").addEventListener("click", handleNhanBan);

        document.getElementById("exportExcelBtn").addEventListener("click", () => {
            showSpinner();
            const table = $("#modal-sn-table").DataTable();
            const allData = table.rows().data().toArray();
            if (!allData.length) {
                showError("No data!");
                hideSpinner();
                return;
            }

            const headers = ["Serial Number", "ModelName", "TestGroup", "TestCode", "Data1", "Status", "Date", "ID_Owner", "TimeConfirm", "HandoverStatus", "Location"];
            const rows = allData.map(row => headers.map((_, i) => row[i]));
            const workbook = XLSX.utils.book_new();
            const worksheet = XLSX.utils.aoa_to_sheet([headers, ...rows]);
            XLSX.utils.book_append_sheet(workbook, worksheet, "DanhSachSN");
            XLSX.writeFile(workbook, "listSN.xlsx");
            hideSpinner();
        });
    } catch (error) {
        console.error("Lỗi khi khởi tạo:", error);
    }
});

// Khai báo existingSNs toàn cục để truy cập từ các hàm khác
let existingSNs;

// Xử lý form SN với jQuery
$(document).ready(function () {
    const table = $("#sn-table").DataTable({
        dom: "t",
        paging: false,
        info: false,
        scrollX: true,
        destroy: true,
        fixedColumns: {
            leftColumns: 1 // Ghim cột đầu tiên
        },
        columnDefs: [
            { width: '140px', targets: 0 }, // SERIAL_NUMBER
            { width: '140px', targets: 1 }, // MODEL_NAME
            { width: '100px', targets: 2 }, // TEST_CODE
            { width: '140px', targets: 3 }, // DATA1
            { width: '100px', targets: 4 }, // WIP_GROUP
            { width: '100px', targets: 5 }, // STATUS
            { width: '80px', targets: 6 }, // HƯỚNG_DẪN
            { width: '100px', targets: 7 }, // ID_OWNER
            { width: '140px', targets: 8 }, // HANDOVER_STATUS
            { width: '140px', targets: 9 }, // LOCATION
            { width: '80px', targets: 10 } // Xóa
        ]
    });
    existingSNs = new Set();

    $("#sn-form").on("submit", async function (e) {
        e.preventDefault();
        const serialNumber = $("#serialNumber").val().trim();
        if (!serialNumber) {
            showWarning("Vui lòng nhập SN!");
            return;
        }
        if (existingSNs.has(serialNumber)) {
            showWarning(`"${serialNumber}" đã tồn tại!`);
            return;
        }

        try {
            const result = await fetchApi(config.endpoints.search, {
                serialNumbers: [serialNumber],
                modelName: "",
                testCode: "",
                status: "",
                data1: "",
                HandoverStatus: ""
            });

            if (result.success && Array.isArray(result.data)) {
                for (const item of result.data) {
                    const sn = item.seriaL_NUMBER?.trim() || "";
                    if (!sn) continue;

                    let checkPointData = { checkPoints: "", detail: "Không có thông tin chi tiết" };
                    try {
                        const standardizedProductLine = standardizeProductLine(item.productLine);
                        const checkPointsPayload = {
                            productLine: standardizedProductLine,
                            testCode: item.tesT_CODE || ''
                        };
                        const apiResponse = await fetchApi(config.endpoints.getCheckPoints, checkPointsPayload);
                        if (apiResponse?.success && apiResponse.checkPoints) {
                            checkPointData = apiResponse;
                        }
                    } catch (error) {
                        console.warn(`Không tìm thấy CheckPoints cho SN ${sn}:`, error);
                    }

                    const checkpointsArray = (checkPointData.success && checkPointData.checkPoints)
                        ? Array.isArray(checkPointData.checkPoints?.$values)
                            ? checkPointData.checkPoints.$values
                            : (typeof checkPointData.checkPoints === 'string' ? [checkPointData.checkPoints] : [checkPointData.checkPoints.toString()])
                        : [""];
                    const encodedCheckpoints = encodeURIComponent(JSON.stringify(checkpointsArray));
                    const encodedDetail = encodeURIComponent(checkPointData.detail || 'Không có thông tin chi tiết');

                    existingSNs.add(sn);
                    table.row.add([
                        sn,
                        item.modeL_NAME || "",
                        `<span title="${item.tesT_CODE || ''}">${truncateText(item.tesT_CODE, 20)}</span>`,
                        `<span title="${item.datA1 || ''}">${truncateText(item.datA1, 20)}</span>`,
                        item.wiP_GROUP || "",
                        item.datA11 || "",
                        `<button class="btn btn-info btn-sm view-repair-guide" 
                                data-sn="${item.seriaL_NUMBER}" 
                                data-model="${item.modeL_NAME}" 
                                data-product-line="${item.productLine}" 
                                data-checkpoints="${encodedCheckpoints}" 
                                data-detail="${encodedDetail}">
                                Chi tiết
                        </button>`,
                        item.tester || "",
                        item.datA13 || "",
                        item.datA18 || "",
                        `<button class="btn btn-success btn-delete">Xóa</button>`
                    ]).draw(false);
                }
                $("#sn-form")[0].reset();
            }
        } catch (error) {
            showError("Lỗi khi gọi API!");
        }
    });

    $("#sn-table tbody").on("click", ".btn-delete", function () {
        const row = table.row($(this).parents("tr"));
        existingSNs.delete(row.data()[0]?.trim());
        row.remove().draw();
    });

    $("#sn-table tbody").on("click", ".view-repair-guide", handleViewRepairGuide);
});