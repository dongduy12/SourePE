// Hiển thị spinner
function showSpinner() {
    document.getElementById("spinner-overlay").style.display = "flex";
}
// Ẩn spinner
function hideSpinner() {
    document.getElementById("spinner-overlay").style.display = "none";
    removeBackdrop()
}
const API_BASE_URL = 'http://10.220.130.119:9090';
const PAGE_SIZE = 2000; // Kích thước trang, khớp với API mặc định
const COLUMN_MAPPING = {
    serialNumber: 'SERIAL_NUMBER',
    moNumber: 'MO_NUMBER',
    modelName: 'MODEL_NAME',
    testGroup: 'TEST_GROUP',
    testCode: 'TEST_CODE',
    data1: 'ERROR_DESC',
    data11: 'STATUS_FA',
    data12: 'PRE_STATUS_FA',
    date3: 'TIME',
    data18: 'LOCATION',
    data19: 'NOTE_FA',
    reasonCode: 'REASON_CODE',
    wipGroup: 'WIP_GROUP',
    workFlag: 'WORK_FLAG',
    kanbanWip: 'KANBAN_WIP',
    productLine: 'PRODUCT_LINE',
    shelfCode: 'KỆ',
    columnNumber: 'CỘT',
    levelNumber: 'TẦNG',
    trayNumber: 'KHAY',
    positionInTray: 'Ô',
    entryDate: 'NGÀY_NHẬP',
    entryPerson: 'NGƯỜI_NHẬP',
    borrowDate: 'NGÀY_MƯỢN',
    borrowPerson: 'NGƯỜI_MƯỢN',
    borrowStatus: 'TRẠNG_THÁI_MƯỢN',
    note: 'NOTE',
    actionNe: 'ACTION',
    scrapStatus: 'SCRAP_STATUS'
};

document.addEventListener("DOMContentLoaded", function () {
    fetch("http://10.220.130.119:9090/api/product/total")
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                const formatted = data.totalCount.toLocaleString('vi-VN');
                const totalStockEl = document.getElementById("total-stock");
                if (totalStockEl) totalStockEl.textContent = formatted;
            } else {
                console.error("API Error:", data.message);
                const totalStockEl = document.getElementById("total-stock");
                if (totalStockEl) totalStockEl.textContent = "Error";
            }
        })
        .catch(error => {
            console.error("Fetch Error:", error);
            const totalStockEl = document.getElementById("total-stock");
            if (totalStockEl) totalStockEl.textContent = "Error";
        });

    fetch("http://10.220.130.119:9090/api/KhoScrap/totalKhoOk")
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                const el = document.getElementById("kho-ok-count");
                if (el) el.textContent = data.totalCount.toLocaleString('vi-VN');
            }
        })
        .catch(err => console.error('KhoOk count error', err));

    fetch("http://10.220.130.119:9090/api/KhoScrap/totalKhoScrap")
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                const el = document.getElementById("kho-scrap-count");
                if (el) el.textContent = data.totalCount.toLocaleString('vi-VN');
            }
        })
        .catch(err => console.error('KhoScrap count error', err));

    fetch("http://10.220.130.119:9090/api/product/borrowed/count")
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                const el = document.getElementById("borrowed-count");
                if (el) el.textContent = data.borrowedCount.toLocaleString('vi-VN');
            }
        })
        .catch(err => console.error('Borrowed count error', err));
});

function removeBackdrop() {
    document.querySelectorAll('.modal-backdrop').forEach(el => el.remove());
    document.body.classList.remove('modal-open');
}

document.addEventListener("DOMContentLoaded", () => {
    const exportButton = document.getElementById("export-all-btn");
    if (exportButton) {
        exportButton.addEventListener("click", async () => {
            try {
                showSpinner();
                exportButton.disabled = true; // Vô hiệu hóa nút trong khi xử lý
                exportButton.textContent = "Đang xuất...";

                const response = await fetch('http://10.220.130.119:9090/api/Search/ExportAllDataToExcel', {
                    method: 'GET',
                    headers: {
                        'Accept': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
                    }
                });

                if (!response.ok) {
                    throw new Error("Không thể xuất dữ liệu!");
                }

                const blob = await response.blob();
                const url = window.URL.createObjectURL(blob);

                // Tạo link download
                const link = document.createElement('a');
                link.href = url;
                link.download = `AllData-${new Date().toISOString()}.xlsx`;
                document.body.appendChild(link);
                link.click();
                link.remove();
                hideSpinner(); //Hide Spinner
                showSuccess("Success!")
            } catch (error) {
                hideSpinner();
                console.error("Lỗi khi xuất Excel:", error.message);
                showError("Error!");
            } finally {
                hideSpinner();
                exportButton.disabled = false; // Kích hoạt lại nút
                exportButton.textContent = "Xuất All";
            }
        });
    } else {
        console.error("Không tìm thấy nút xuất dữ liệu với ID 'export-all-btn'");
    }
});


document.addEventListener("DOMContentLoaded", () => {
    // Lấy totalCount và hiển thị
    fetch(`${API_BASE_URL}/api/SAP?page=1&pageSize=1`)
        .then(response => {
            if (!response.ok) {
                throw new Error(`Lỗi HTTP! Trạng thái: ${response.status}`);
            }
            return response.json();


        })
        .then(data => {
            if (data && typeof data.totalCount === 'number') {
                document.getElementById("total-stock-re").textContent = data.totalCount.toLocaleString('vi-VN');
            } else {
                throw new Error("Định dạng phản hồi API không hợp lệ");
            }
        })
        .catch(error => {
            console.error("Lỗi khi gọi API:", error.message);
            document.getElementById("total-stock-re").textContent = "Lỗi";
            showError("Không thể tải số lượng");
        });

    // Xuất toàn bộ dữ liệu ra Excel
    const exportButton = document.getElementById("export-all-re-btn");
    if (exportButton) {
        exportButton.addEventListener("click", async () => {
            try {
                showSpinner();
                exportButton.disabled = true;
                exportButton.textContent = "Đang xuất...";

                // Lấy totalCount để tính số trang
                const initialResponse = await fetch(`${API_BASE_URL}/api/SAP?page=1&pageSize=1`);
                if (!initialResponse.ok) {
                    throw new Error(`Lỗi HTTP! Trạng thái: ${initialResponse.status}`);
                }
                const initialData = await initialResponse.json();
                if (!initialData || typeof initialData.totalCount !== 'number') {
                    throw new Error("Định dạng phản hồi API không hợp lệ");
                }

                const totalCount = initialData.totalCount;
                const totalPages = Math.ceil(totalCount / PAGE_SIZE);
                const allData = [];

                // Gọi API cho từng trang
                for (let page = 1; page <= totalPages; page++) {
                    const response = await fetch(`${API_BASE_URL}/api/SAP?page=${page}&pageSize=${PAGE_SIZE}`);
                    if (!response.ok) {
                        throw new Error(`Lỗi HTTP khi lấy trang ${page}! Trạng thái: ${response.status}`);
                    }
                    const data = await response.json();
                    if (data && Array.isArray(data.data)) {
                        allData.push(...data.data);
                    } else {
                        throw new Error(`Dữ liệu trang ${page} không hợp lệ`);
                    }
                }

                // Chuyển dữ liệu thành Excel
                if (typeof XLSX === 'undefined') {
                    throw new Error("Thư viện XLSX chưa được tải");
                }

                // Ánh xạ tên cột
                const mappedData = allData.map(item => {
                    const mappedItem = {};
                    for (const key in item) {
                        if (COLUMN_MAPPING[key]) {
                            mappedItem[COLUMN_MAPPING[key]] = item[key];
                        } else {
                            mappedItem[key] = item[key]; // Giữ nguyên nếu không có ánh xạ
                        }
                    }
                    return mappedItem;
                });


                const worksheet = XLSX.utils.json_to_sheet(mappedData);
                const workbook = XLSX.utils.book_new();
                XLSX.utils.book_append_sheet(workbook, worksheet, "Data");


                // Xuất file Excel
                const excelBuffer = XLSX.write(workbook, { bookType: 'xlsx', type: 'array' });
                const blob = new Blob([excelBuffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
                const url = window.URL.createObjectURL(blob);
                const link = document.createElement('a');
                link.href = url;
                link.download = `AllData_Check_In-${new Date().toISOString().slice(0, 10)}.xlsx`;
                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
                window.URL.revokeObjectURL(url);

                showSuccess("Xuất Excel thành công!");
            } catch (error) {
                console.error("Lỗi khi xuất Excel:", error.message);
                showError(`Lỗi khi xuất Excel: ${error.message}`);
            } finally {
                hideSpinner();
                exportButton.disabled = false;
                exportButton.textContent = "Xuất All";
            }
        });
    } else {
        console.error("Không tìm thấy nút xuất dữ liệu với ID 'export-all-re-btn'");
    }
});

document.addEventListener('DOMContentLoaded', () => {
    const exportKhoOkBtn = document.getElementById('export-kho-ok-btn');
    if (exportKhoOkBtn) {
        exportKhoOkBtn.addEventListener('click', async () => {
            try {
                showSpinner();
                const res = await fetch(`${API_BASE_URL}/api/Search/ExportExcelKhoOk`, {
                    method: 'GET',
                    headers: {
                        'Accept': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
                    }
                });
                if (!res.ok) throw new Error('Export KhoOk failed');
                const blob = await res.blob();
                const url = window.URL.createObjectURL(blob);
                const link = document.createElement('a');
                link.href = url;
                link.download = `KhoOk-${new Date().toISOString()}.xlsx`;
                document.body.appendChild(link);
                link.click();
                link.remove();
                showSuccess('Success!');
            } catch (err) {
                console.error('Export KhoOk error', err);
                showError('Error!');
            } finally {
                hideSpinner();
            }
        });
    }

    const exportKhoScrapBtn = document.getElementById('export-kho-scrap-btn');
    if (exportKhoScrapBtn) {
        exportKhoScrapBtn.addEventListener('click', async () => {
            try {
                showSpinner();
                const res = await fetch(`${API_BASE_URL}/api/Search/ExportExcelScrap`, {
                    method: 'GET',
                    headers: {
                        'Accept': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
                    }
                });
                if (!res.ok) throw new Error('Export KhoScrap failed');
                const blob = await res.blob();
                const url = window.URL.createObjectURL(blob);
                const link = document.createElement('a');
                link.href = url;
                link.download = `KhoScrap-${new Date().toISOString()}.xlsx`;
                document.body.appendChild(link);
                link.click();
                link.remove();
                showSuccess('Success!');
            } catch (err) {
                console.error('Export KhoScrap error', err);
                showError('Error!');
            } finally {
                hideSpinner();
            }
        });
    }
});



async function fetchTopModelsChart() {
    const response = await fetch("http://10.220.130.119:9090/api/product/top_models");
    const data = await response.json();

    if (!data.success || !Array.isArray(data.topModels)) {
        console.error("Lỗi dữ liệu API:", data);
        document.getElementById("top-model-chart").innerHTML = "<p>Không có dữ liệu.</p>";
        return;
    }

    const categories = data.topModels.map(item => item.modelName);
    const values = data.topModels.map(item => item.count);

    const option = {
        grid: {
            left: "5%",   // ✅ Tăng khoảng cách lề trái
            right: "5%",  // ✅ Tránh cột bị tràn ra ngoài
            bottom: "10%"
        },
        legend: { bottom: 0 },
        tooltip: { trigger: "axis" },
        xAxis: {
            type: "category",
            data: categories,
            axisLabel: {
                rotate: 0,        // ✅ Xoay chữ nhẹ cho dễ đọc
                fontSize: 10,
                color: "#000000",
                formatter: function (value) {
                    return value.length > 10 ? value.substring(0, 10) + "..." : value;
                }
            }
        },
        yAxis: { name: "Số lượng" },
        series: [
            {
                type: "bar",
                name: "Số lượng",
                color: "#2eca6a",
                barWidth: "45%",  // ✅ Điều chỉnh độ rộng cột để có khoảng cách
                barCategoryGap: "30%", // ✅ Giãn khoảng cách giữa các cột
                data: values,
                itemStyle: { borderRadius: [5, 5, 5, 5] }, // ✅ Bo tròn góc trên
                label: { show: true, position: "top", color: "#000", fontWeight: "bold" }
            }
        ]
    };

    // ✅ Vẽ biểu đồ & tự động thu nhỏ theo màn hình
    const chartContainer = document.getElementById("top-model-chart");
    const chart = echarts.init(chartContainer, null, { responsive: true });
    chart.setOption(option);

    // ✅ Đảm bảo biểu đồ tự động resize khi thu nhỏ màn hình
    window.addEventListener("resize", function () {
        chart.resize();
    });
}

document.addEventListener("DOMContentLoaded", fetchTopModelsChart);

document.addEventListener("DOMContentLoaded", function () {
    const container = document.getElementById("top-models-container");
    if (!container) {
        console.error("Lỗi: Không tìm thấy phần tử 'top-models-container'!");
        return;
    }

    // Hiển thị loader trước khi fetch
    const loader = document.createElement("div");
    loader.className = "loader";
    container.appendChild(loader);

    fetch("http://10.220.130.119:9090/api/product/top_models")
        .then(response => response.json())
        .then(data => {
            loader.style.display = "none"; // Ẩn loader khi có dữ liệu

            if (data.success && Array.isArray(data.topModels)) {
                container.innerHTML = ""; // Xóa nội dung cũ
                data.topModels.forEach((model, index) => {
                    const activityItem = `
                        <div class="activity-item d-flex align-items-center p-3 shadow-sm mb-3">
                            <div class="activite-label display-5 fw-bold">${index + 1}</div>
                            <i class="bi bi-star-fill activity-badge text-warning align-self-center mx-3"></i>
                            <div class="activity-content d-flex justify-content-between align-items-center w-100">
                                <span class="model-name fs-4 fw-bold">${model.modelName}</span>
                                <span class="badge bg-primary fs-5 ms-auto">${model.count}</span>
                            </div>
                        </div>
                    `;
                    container.innerHTML += activityItem;
                });
            } else {
                container.innerHTML = "<p>Không có dữ liệu.</p>";
            }
        })
        .catch(error => {
            console.error("Fetch Error:", error);
            loader.style.display = "none";
            container.innerHTML = "<p>Đã xảy ra lỗi khi tải dữ liệu.</p>";
        });
});

//CHECK B36R
document.addEventListener("DOMContentLoaded", function () {
    // Khởi tạo DataTable
    // Khởi tạo DataTable
    let table = $('#dataTable').DataTable({
        destroy: true,
        data: [],
        columns: [
            { data: 'sn' },
            { data: 'productLine' },
            { data: 'modelName' },
            {
                data: 'exportDate',
                type: 'date' // Đảm bảo cột này được coi là kiểu ngày giờ
            },
            { data: 'status' }
        ],
        dom: 'lBfrtip', // Bao gồm 'B' để hiển thị buttons
        lengthChange: true,
        lengthMenu: [15, 25, 50, 100],
        pageLength: 15,
        buttons: [
            {
                extend: 'excelHtml5',
                text: '<img src = "/assets/img/excel.png" alt = "Excel Icon" style = "width:28px; height:28px;" >', // Sử dụng thẻ img
                title: 'CHECK_B36R', // Tên file Excel
                exportOptions: {
                    columns: [0, 1, 2, 3, 4] // Xuất tất cả các cột
                },
                customize: function (xlsx) {
                    var sheet = xlsx.xl.worksheets['sheet1.xml'];
                    var rowCount = table.rows().count();
                    var start = startInput.value ? startInput.value.replace('T', ' ') : '';
                    var end = endInput.value ? endInput.value.replace('T', ' ') : '';
                    var summary = `Số lượng serial_number (${start} - ${end}): ${rowCount}`;
                    var sheetData = $('sheetData', sheet);
                    var lastRow = $('row', sheet).length;
                    var summaryRow = `<row r="${lastRow + 1}"><c t="inlineStr" r="A${lastRow + 1}"><is><t>${summary}</t></is></c></row>`;
                    sheetData.append(summaryRow);
                }
            }
        ]
    });

    const startInput = document.getElementById("b36r-start-date");
    const endInput = document.getElementById("b36r-end-date");
    const filterBtn = document.getElementById("b36r-filter-btn");

    function buildUrl() {
        const params = [];
        if (startInput.value) params.push(`startDate=${encodeURIComponent(startInput.value)}`);
        if (endInput.value) params.push(`endDate=${encodeURIComponent(endInput.value)}`);
        return `http://10.220.130.119:9090/checking-b36r${params.length ? "?" + params.join("&") : ""}`;
    }

    function toRows(arr) {
        return (arr || []).map(item => ({
            sn: item.sn || "N/A",
            productLine: item.productLine || "N/A",
            modelName: item.modelName || "N/A",
            exportDate: item.exportDate || "N/A",
            linkTime: item.linkTime || "N/A",
            status: item.status || "N/A"
        }));
    }

    function loadCounts() {
        fetch(buildUrl())
            .then(r => r.json())
            .then(data => {
                if (data.success) {
                    document.getElementById("totaL-linked-mo").textContent = data.linkCount ?? 0;
                    document.getElementById("total-waiting-link-mo").textContent = data.awaitingLinkCount ?? 0;
                } else {
                    console.error("API Error:", data.message);
                    document.getElementById("totaL-linked-mo").textContent = "Error";
                    document.getElementById("total-waiting-link-mo").textContent = "Error";
                }
            })
            .catch(err => {
                console.error("Fetch Error:", err);
                document.getElementById("totaL-linked-mo").textContent = "Error";
                document.getElementById("total-waiting-link-mo").textContent = "Error";
            });
    }

    filterBtn.addEventListener("click", loadCounts);
    loadCounts();

    // === Click "Đã link" ===
    document.getElementById("totaL-linked-mo").addEventListener("click", function () {
        fetch(buildUrl())
            .then(r => r.json())
            .then(data => {
                if (data.success) {
                    const linkedData = toRows(data.linked);
                    if (linkedData.length === 0) {
                        alert("Không có dữ liệu MO đã link để hiển thị.");
                        return;
                    }
                    table.clear();
                    table.rows.add(linkedData).draw();
                    $('#dataModal').modal('show');
                    document.getElementById("dataModalLabel").textContent = "Danh sách MO đã link";
                } else {
                    console.error("API Error:", data.message);
                }
            })
            .catch(err => console.error("Fetch Error:", err));
    });

    // === Click "Chờ link" ===
    document.getElementById("total-waiting-link-mo").addEventListener("click", function () {
        fetch(buildUrl())
            .then(r => r.json())
            .then(data => {
                if (data.success) {
                    const awaitingData = toRows(data.awaiting);
                    if (awaitingData.length === 0) {
                        alert("Không có dữ liệu MO chờ link để hiển thị.");
                        return;
                    }
                    table.clear();
                    table.rows.add(awaitingData).draw();
                    $('#dataModal').modal('show');
                    document.getElementById("dataModalLabel").textContent = "Danh sách MO chờ link";
                } else {
                    console.error("API Error:", data.message);
                }
            })
            .catch(err => console.error("Fetch Error:", err));
    });

});

// Quản lý Export B36R
document.addEventListener("DOMContentLoaded", function () {
    const settingsBtn = document.getElementById('b36r-settings');
    if (!settingsBtn) return;

    const searchInput = document.getElementById('export-search-input');
    const searchBtn = document.getElementById('export-search-btn');
    const addBtn = document.getElementById('export-add-btn');

    const table = $('#exportTable').DataTable({
        destroy: true,
        data: [],
        columns: [
            { data: 'serialNumber' },
            { data: 'productLine' },
            { data: 'modelName' },
            {
                data: 'exportDate',
                render: function (data) {
                    return data ? new Date(data).toLocaleString() : '';
                }
            },
            {
                data: null,
                orderable: false,
                render: function () {
                    return `<button class="btn btn-sm btn-primary btn-edit">Edit</button>
                            <button class="btn btn-sm btn-danger btn-delete">Delete</button>`;
                }
            }
        ]
    });

    async function loadData(query) {
        const url = query ? `${API_BASE_URL}/api/Export?serialNumber=${encodeURIComponent(query)}` : `${API_BASE_URL}/api/Export`;
        const res = await fetch(url);
        const json = await res.json();
        if (json.success) {
            table.clear();
            table.rows.add(json.data);
            table.draw();
        }
    }

    settingsBtn.addEventListener('click', function () {
        searchInput.value = '';
        loadData('');
        $('#exportModal').modal('show');
    });

    searchBtn.addEventListener('click', function () {
        loadData(searchInput.value.trim());
    });

    addBtn.addEventListener('click', async function () {
        const sn = prompt('Nhập Serial Number mới');
        if (!sn) return;
        await fetch(`${API_BASE_URL}/api/Export`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ serialNumber: sn })
        });
        loadData(searchInput.value.trim());
    });

    $('#exportTable').on('click', '.btn-edit', async function () {
        const data = table.row($(this).parents('tr')).data();
        const newSn = prompt('Chỉnh sửa Serial Number', data.serialNumber);
        if (!newSn) return;
        await fetch(`${API_BASE_URL}/api/Export/${data.id}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ serialNumber: newSn })
        });
        loadData(searchInput.value.trim());
    });

    $('#exportTable').on('click', '.btn-delete', async function () {
        const data = table.row($(this).parents('tr')).data();
        if (!confirm('Xóa Serial Number này?')) return;
        await fetch(`${API_BASE_URL}/api/Export/${data.id}`, { method: 'DELETE' });
        loadData(searchInput.value.trim());
    });
});

document.addEventListener('DOMContentLoaded', () => {
    const btn = document.getElementById('warehouse-search-btn');
    const input = document.getElementById('warehouse-sn-input');
    const createListBtn = document.getElementById('create-list-btn');
    let warehouseNotFound = [];
    let warehouseListItems = [];
    let warehouseTable = $('#warehouseTable').DataTable({
        destroy: true,
        data: [],
        columns: [
            { title: 'Serial Number' },
            { title: 'Kho' },
            { title: 'Kệ' },
            { title: 'Cột' },
            { title: 'Tầng' },
            { title: 'Khay' },
            { title: 'Ô' },
            { title: 'Product Line' },
            { title: 'Model' }
        ],
        dom: 'Bfrtip',
        buttons: [
            {
                extend: 'excel',
                text: '<img src="/assets/img/excel.png" class="excel-icon"/>',
                className: 'excel-button',
                title: 'WarehouseSearchResults'
            }
        ]
    });

    if (btn && input) {
        btn.addEventListener('click', async () => {
            const sns = input.value
                .split(/\s+/)
                .map(s => s.trim().toUpperCase())
                .filter(s => s);
            if (!sns.length) return;
            try {
                const res = await fetch(`${API_BASE_URL}/api/Search/FindLocations`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(sns)
                });
                const data = await res.json();
                warehouseTable.clear();
                if (data.success) {
                    const borrowedSerials = [];
                    const noPositionSerials = [];
                    data.data.forEach(d => {
                        warehouseTable.row.add([
                            d.serialNumber,
                            d.warehouse,
                            d.shelfCode || '',
                            d.columnNumber ?? '',
                            d.levelNumber ?? '',
                            d.trayNumber ?? '',
                            d.positionInTray ?? '',
                            d.productLine || '',
                            d.modelName || ''
                        ]);
                        if (d.borrowStatus === 'Borrowed') {
                            borrowedSerials.push(d.serialNumber);
                        }
                        const noPosition = !d.shelfCode && d.columnNumber == null;
                        if (noPosition){
                        noPositionSerials.push(d.serialNumber);
                    }
                    });
                    warehouseNotFound = data.notFoundSerialNumbers || [];
                    warehouseListItems = [...warehouseNotFound, ...borrowedSerials, ...noPositionSerials];
                    if (warehouseNotFound.length) {
                        warehouseNotFound.forEach(sn => {
                            warehouseTable.row.add([sn, 'Not found', '', '', '', '', '', '', '']);
                        });
                    }
                    if (createListBtn) {
                        createListBtn.style.display = warehouseListItems.length ? 'inline-block' : 'none';
                    }
                } else {
                    warehouseNotFound = [];
                    warehouseListItems = [];
                    if (createListBtn) createListBtn.style.display = 'none';
                }
                warehouseTable.draw();
                $('#warehouseModal').modal('show');
            } catch (err) {
                console.error('FindLocations error', err);
            }
        });
    }

    if (createListBtn) {
        createListBtn.addEventListener('click', async () => {
            const currentUser = document.getElementById('entryPerson').value;
            if (!warehouseListItems.length) {
                showError('Không có Serial Number không tìm thấy!');
                return;
            }
            $('#warehouseModal').modal('hide');
            Swal.fire({
                title: 'Tạo danh sách tìm kiếm',
                input: 'text',
                inputLabel: 'Tên danh sách',
                inputPlaceholder: 'Nhập tên danh sách',
                showCancelButton: true,
                confirmButtonText: 'Lưu',
                cancelButtonText: 'Hủy',
                showLoaderOnConfirm: true,
                preConfirm: async (listName) => {
                    if (!listName || listName.trim() === '') {
                        Swal.showValidationMessage('Vui lòng nhập tên danh sách!');
                        return false;
                    }
                    try {
                        showSpinner();
                        const response = await fetch('http://10.220.130.119:9090/api/Search/SaveSearchList', {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify({
                                listName: listName.trim(),
                                createdBy: currentUser,
                                foundItems: [],
                                notFoundItems: warehouseListItems.map(sn => ({
                                    serialNumber: sn || '',
                                    modelName: '',
                                    shelfCode: '',
                                    columnNumber: 0,
                                    levelNumber: 0,
                                    trayNumber: 0,
                                    positionInTray: 0
                                }))
                            })
                        });
                        if (!response.ok) {
                            const errorData = await response.json();
                            throw new Error(errorData.message || 'Không thể lưu danh sách!');
                        }
                        return { success: true };
                    } catch (error) {
                        Swal.showValidationMessage(`Lỗi: ${error.message}`);
                        return false;
                    } finally {
                        hideSpinner();
                    }
                },
                allowOutsideClick: () => !Swal.isLoading()
            }).then(result => {
                if (result.isConfirmed && result.value.success) {
                    showSuccess('Success!!');
                }
                $('#warehouseModal').modal('show');
            });
        });
    }
});