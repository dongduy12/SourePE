document.getElementById("searchBtn").addEventListener("click", async function () {
    const startDate = document.getElementById("startDate").value;
    const endDate = document.getElementById("endDate").value;

    if (!startDate || !endDate) {
        alert("Vui lòng chọn khoảng thời gian!");
        return;
    }

    const payload = {
        StartDate: new Date(startDate).toISOString(),
        EndDate: new Date(endDate).toISOString()
    };

    try {
        // Gọi từng API và hiển thị dữ liệu
        await fetchAndRenderTable("http://10.220.130.119:9090/api/SearchFA/get-retest-summary", "summary_table", ["id", "FullName", "qtY_OK", "qtY_NG", "noT_DEFINE", "total"], payload);
        await fetchAndRenderTable("http://10.220.130.119:9090/api/SearchFA/get-retest-details-normal", "detail_hang_thuong", ["date", "tester", "FullName", "serialNumber", "modelName", "testGroup", "data1", "testResult", "remark"], payload);
        await fetchAndRenderTable("http://10.220.130.119:9090/api/SearchFA/get-retest-mcu-test-group", "detail_MCU", ["date", "tester", "FullName", "serialNumber", "modelName", "testGroup", "data1", "testResult", "remark"], payload);
        await fetchAndRenderTable("http://10.220.130.119:9090/api/SearchFA/get-retest-dpu-mem-test", "detail_DPU", ["date", "tester", "FullName", "serialNumber", "modelName", "testGroup", "data1", "testResult", "remark"], payload);
    } catch (error) {
        console.error("Lỗi khi lấy dữ liệu:", error);
        alert("Lỗi khi lấy dữ liệu. Vui lòng thử lại!");
    }
});

// Hàm fetch API và hiển thị dữ liệu trong bảng
/*async function fetchAndRenderTable(apiUrl, tableId, columns, payload) {
    try {
        const response = await fetch(apiUrl, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        const result = await response.json();

        if (result.success && Array.isArray(result.data)) {
            // Lấy danh sách ID và Tester để gọi API get-fullname-batch
            let ids = new Set();
            let testers = new Set();
            result.data.forEach(row => {
                if (row.id) ids.add(row.id);
                if (row.tester) testers.add(row.tester);
            });

            // Gọi API get-fullname-batch
            const fullNames = await fetchFullNames([...ids, ...testers]);

            // Gắn dữ liệu FullName vào từng dòng
            result.data.forEach(row => {
                row.FullName = fullNames[row.id] || fullNames[row.tester] || "N/A"; // Nếu không có tên, đặt "N/A"
            });

            renderTable(tableId, result.data, columns);
        } else {
            console.warn(`API ${apiUrl} không trả về dữ liệu hợp lệ.`);
        }
    } catch (error) {
        console.error(`Lỗi gọi API ${apiUrl}:`, error);
    }
}
*/

// Hàm tạo bảng HTML từ dữ liệu API
async function fetchAndRenderTable(apiUrl, tableId, columns, payload) {
    try {
        const response = await fetch(apiUrl, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        const result = await response.json();

        if (result.success && Array.isArray(result.data)) {
            // Lấy danh sách ID và Tester để gọi API get-fullname-batch
            let ids = new Set();
            result.data.forEach(row => {
                if (row.id) ids.add(row.id);
                if (row.tester) ids.add(row.tester);
            });

            // Gọi API get-fullname-batch
            const fullNames = await fetchFullNames([...ids]);

            // Gắn dữ liệu FullName vào từng dòng
            result.data.forEach(row => {
                row.FullName = fullNames[row.id] || fullNames[row.tester] || "N/A"; // Nếu không có tên, đặt "N/A"
            });

            renderTable(tableId, result.data, columns);
        } else {
            console.warn(`API ${apiUrl} không trả về dữ liệu hợp lệ.`);
        }
    } catch (error) {
        console.error(`Lỗi gọi API ${apiUrl}:`, error);
    }
}


// Hàm hiển thị dữ liệu lên bảng
function renderTable(tableId, data, columns) {
    const table = document.getElementById(tableId);
    if (!table) {
        console.error(`Không tìm thấy bảng có ID: ${tableId}`);
        return;
    }

    // Xóa dữ liệu cũ nhưng giữ lại <thead>
    table.querySelector("tbody").innerHTML = "";

    // Tạo nội dung bảng
    let tbody = "";
    data.forEach(row => {
        tbody += "<tr>";
        columns.forEach(col => {
            tbody += `<td>${row[col] !== undefined ? row[col] : ""}</td>`;
        });
        tbody += "</tr>";
    });

    // Render bảng vào HTML
    table.querySelector("tbody").innerHTML = tbody;
}


// function fetchFullNames
async function fetchFullNames(ids) {
    if (ids.length === 0) return {};

    try {
        const response = await fetch("http://10.220.130.119:9090/api/SearchFA/get-fullname-batch-list", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(ids)
        });

        const result = await response.json();

        return result.success ? result.data : {};
    } catch (error) {
        console.error("Lỗi khi gọi API get-fullname-batch:", error);
        return {};
    }
}


// Lắng nghe sự kiện click vào nút xuất Excel
document.getElementById("PdStock-ExportExcel-btn").addEventListener("click", function () {
    exportTablesToExcel();
});

async function exportTablesToExcel() {
    const tables = {
        "Summary Report": "summary_table",
        "Detail Hang Thuong": "detail_hang_thuong",
        "Detail MCU": "detail_MCU",
        "Detail DPU": "detail_DPU"
    };

    let workbook = XLSX.utils.book_new();

    for (let sheetName in tables) {
        let tableId = tables[sheetName];
        let table = document.getElementById(tableId);

        if (table) {
            let ws = XLSX.utils.table_to_sheet(table);
            XLSX.utils.book_append_sheet(workbook, ws, sheetName);
        }
    }

    // Xuất file Excel với tên có timestamp
    let timestamp = new Date().toISOString().replace(/[-T:.Z]/g, "");
    let fileName = `ReTest_Report_${timestamp}.xlsx`;

    XLSX.writeFile(workbook, fileName);
}

