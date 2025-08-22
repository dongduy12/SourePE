document.getElementById("searchBtn").addEventListener("click", async () => {
    const serials = document.getElementById("sn-input").value
        .split(/\n|,|\s+/)
        .map(s => s.trim())
        .filter(s => s);
    if (serials.length === 0) {
        showWarning("Vui lòng nhập SerialNumber!");
        return;
    }

    try {
        const response = await fetch("http://10.220.130.119:9090/api/SearchFA/get-repair-owner-data-by-sn", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ serialNumbers: serials })
        });
        const result = await response.json();
        if (result.success) {
            renderTable(Array.isArray(result.data) ? result.data : []);
        } else {
            showError(result.message || "Không lấy được dữ liệu!");
        }
    } catch (err) {
        console.error(err);
        showError("Không lấy được dữ liệu!");
    }
});

function renderTable(data = []) {
    const tbody = document.getElementById("results-body");
    tbody.innerHTML = "";
    data.forEach(item => {
        const tr = document.createElement("tr");
        tr.innerHTML = `
            <td>${item.serialNumber || ""}</td>
            <td>${item.modelName || ""}</td>
            <td>${item.reasonCode || ""}</td>
            <td>${item.testCode || ""}</td>
            <td>${item.errorDesc || ""}</td>
            <td>${(item.repairer || "").trim()}</td>
            <td>${item.testGroup || ""}</td>
            <td>${(item.errorItemCode || "").trim()}</td>
            <td>${(item.repairTime || "").trim()}</td>`;
        tbody.appendChild(tr);
    });
}

// Export to Excel
_documentExport();
function _documentExport() {
    document.getElementById("exportExcelBtn").addEventListener("click", () => {
        const rows = document.querySelectorAll("#results-body tr");
        if (rows.length === 0) {
            showWarning("Không có dữ liệu để xuất!");
            return;
        }
        const excelData = [[
            "SerialNumber", "ModelName", "ReasonCode", "TestCode", "ErrorDesc", "Repairer", "TestGroup", "ErrorItemCode", "RepairTime"
        ]];
        rows.forEach(r => {
            excelData.push(Array.from(r.children).map(c => c.textContent.trim()));
        });
        const ws = XLSX.utils.aoa_to_sheet(excelData);
        const wb = XLSX.utils.book_new();
        XLSX.utils.book_append_sheet(wb, ws, "Result");
        XLSX.writeFile(wb, "RepairOwner.xlsx");
    });
}

function showWarning(message) {
    Swal.fire({
        title: message,
        icon: "warning",
        draggable: true
    });
}

function showError(message) {
    Swal.fire({
        title: message,
        icon: "error",
        draggable: true
    });
}
