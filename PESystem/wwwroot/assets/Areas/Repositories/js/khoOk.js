/**
 * Quản lý kho bản ok
 * Chức năng: Tìm kiếm, Nhập kho, Xuất kho, Xuất Excel
 */
const KhoScrapManager = (function () {
    // Biến toàn cục
    let searchResultsSN = []; // Lưu kết quả tìm kiếm
    let notFoundSerialNumbers = []; // Lưu Serial Number không tìm thấy
    let selectedSerialsSNs = []; // Không dùng (bỏ checkbox)

    const API_BASE_URL = 'http://10.220.130.119:9090/api/KhoScrap';
    const API_TOTAL_SCRAP = `${API_BASE_URL}/totalKhoOk`;
    const API_CLASSIFY_SCRAP = `${API_BASE_URL}/ClassifyScrap`;

    // Nhóm hàm hỗ trợ
    const Utils = {
        getSerialNumbersFromInput: () => {
            const snInput = document.getElementById("sn-input")?.value || "";
            const snImport = document.getElementById("sn-import")?.value || "";
            if (snInput != "") return snInput.split('\n')
                .map(sn => sn.trim().toUpperCase())
                .filter(sn => sn && /^[A-Za-z0-9-]+$/.test(sn));
            return snImport.split('\n')
                .map(sn => sn.trim().toUpperCase())
                .filter(sn => sn && /^[A-Za-z0-9-]+$/.test(sn));
        },

        hasDuplicateSerials: (serialNumbers) => {
            const uniqueSerials = new Set(serialNumbers);
            return uniqueSerials.size !== serialNumbers.length;
        },

        showError: (message) => {
            Swal.fire({
                icon: 'error',
                title: 'Lỗi',
                text: message
            });
        },

        showSuccess: (message) => {
            Swal.fire({
                icon: 'success',
                title: 'Thành công',
                text: message
            });
        },

        showWarning: (message) => {
            Swal.fire({
                icon: 'warning',
                title: 'Cảnh báo',
                text: message
            });
        },

        showSpinner: () => {
            // Thêm logic hiển thị spinner nếu cần
        },

        hideSpinner: () => {
            // Thêm logic ẩn spinner nếu cần
        },

        displayTotalResults: (count) => {
            const totalResults = document.getElementById("total-results");
            if (totalResults) {
                totalResults.textContent = `Kết quả: ${count}`;
            }
        },

        resetUI: () => {
            document.getElementById("sn-input").value = "";
            document.getElementById("sn-import").value = "";
            searchResultsSN = [];
            Render.renderTable([], 'results-body');
            Utils.displayTotalResults(0);
            document.getElementById("search-results").style.display = "none";
            document.getElementById("export-sn-excel-btn").style.display = "none";
            const borrowBtn = document.getElementById("borrow-btn");
            if (borrowBtn) borrowBtn.style.display = "none";
            const buttonAction = document.getElementById("button-action");
            if (buttonAction) buttonAction.classList.add("hidden");
        }
    };

    // Nhóm hàm tìm kiếm
    const Search = {
        searchSerialNumbers: async () => {
            const serialNumbers = Utils.getSerialNumbersFromInput();

            if (serialNumbers.length === 0) {
                Utils.showError("Vui lòng nhập ít nhất một Serial Number hợp lệ!");
                return;
            }

            try {
                Utils.showSpinner();
                const response = await fetch(`http://10.220.130.119:9090/api/Search/SearchSnOK`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(serialNumbers)
                });

                if (!response.ok) throw new Error('Không thể tải dữ liệu từ Server');

                const data = await response.json();
                if (data.success) {
                    searchResultsSN = data.results || [];
                    notFoundSerialNumbers = data.notFoundSerialNumbers || [];
                    Render.renderTable(searchResultsSN, 'results-body');
                    Utils.displayTotalResults(data.totalFound || 0);
                    document.getElementById("search-results").style.display = "block";
                    const exportExcelBtn = document.getElementById("export-sn-excel-btn");
                    if (exportExcelBtn) {
                        exportExcelBtn.style.display = "block";
                        exportExcelBtn.classList.remove("hidden");
                    }
                    document.getElementById("export-sn-btn").style.display = "block";
                    const buttonAction = document.getElementById("button-action");
                    if (buttonAction) buttonAction.classList.remove("hidden");
                    Utils.showSuccess(`Tìm thấy ${data.totalFound || 0}/${(data.totalFound || 0) + (data.totalNotFound || 0)} SN`);
                } else {
                    Utils.showError(data.message || "Không tìm thấy kết quả!");
                    searchResultsSN = [];
                    notFoundSerialNumbers = [];
                    Render.renderTable([], 'results-body');
                }
            } catch (error) {
                Utils.showError("Lỗi khi tìm kiếm: " + error.message);
            } finally {
                Utils.hideSpinner();
            }
        }
    };

    // Nhóm hàm xuất kho
    const Export = {
        exportSerialNumbers: async () => {
            const serialNumbers = Utils.getSerialNumbersFromInput();
            const exportPerson = document.getElementById("entryPerson")?.value?.trim() || "Unknown";

            if (serialNumbers.length === 0) {
                Utils.showError("Vui lòng nhập ít nhất một Serial Number để xuất kho!");
                return;
            }

            const { value: note } = await Swal.fire({
                title: 'Xác nhận xuất kho',
                html: `Bạn có chắc muốn xuất kho ${serialNumbers.length} Serial Number?<br>
                       <input id="swal-input-note" class="swal2-input" placeholder="Nhập ghi chú (tùy chọn)">`,
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: 'Xác nhận',
                cancelButtonText: 'Hủy',
                preConfirm: () => document.getElementById('swal-input-note').value
            });

            if (!note && note !== '') return;

            try {
                Utils.showSpinner();
                const response = await fetch(`${API_BASE_URL}/ExportKhoOk`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        serialNumbers: serialNumbers,
                        exportPerson: exportPerson,
                        note: note
                    })
                });

                if (!response.ok) throw new Error('Không thể xuất kho Serial Number!');

                const data = await response.json();
                if (data.success) {
                    Utils.showSuccess(`Xuất kho ${data.results.filter(r => r.success).length} SN thành công!`);
                    searchResultsSN = searchResultsSN.filter(sn => !serialNumbers.includes(sn.serialNumber));
                    Render.renderTable(searchResultsSN, 'results-body');
                    Utils.displayTotalResults(searchResultsSN.length);
                    if (searchResultsSN.length === 0) {
                        document.getElementById("search-results").style.display = "none";
                        document.getElementById("export-sn-excel-btn").style.display = "none";
                        //document.getElementById("export-sn-btn").style.display = "none";
                    }
                    Utils.resetUI();
                    await Total.updateTotalScrapAndCategories();
                } else {
                    Utils.showError(data.message || "Lỗi khi xuất kho Serial Number!");
                }
            } catch (error) {
                Utils.showError("Lỗi khi xuất kho: " + error.message);
            } finally {
                Utils.hideSpinner();
            }
        },

        exportToExcel: (data, fileNamePrefix) => {
            if (!data || data.length === 0) {
                Utils.showError("Không có dữ liệu để xuất!");
                return;
            }

            const worksheetData = data.map(result => ({
                "SERIAL_NUMBER": result.serialNumber || "",
                "MODEL_NAME": result.modelName || "",
                "MO_NUMBER": result.moNumber || "",
                "WIP_GROUP": result.wipGroup || "",
                "TEST_GROUP": result.testGroup || "",
                "REASON_CODE": result.reasonCode || "",
                "TEST_CODE": result.testCode || "",
                "ERROR_DESC": result.data1 || "",
                "SHELF": result.shelfCode || "",
                "COLUMN": result.columnNumber || "",
                "LEVEL": result.levelNumber || "",
                "ENTRY_DATE": result.entryDate || "",
                "ENTRY_PERSON": result.entryPerson || "",
                "BORROW_STATUS": result.borrowStatus || "",
                "BORROW_DATE": result.borrowDate || "",
                "BORROW_PERSON": result.borrowPerson || "",
                "NOTE": result.note || ""
            }));

            const workbook = XLSX.utils.book_new();
            const worksheet = XLSX.utils.json_to_sheet(worksheetData);

            XLSX.utils.book_append_sheet(workbook, worksheet, "SearchResults");

            const formattedDate = new Date().toLocaleString('vi-VN', {
                year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit'
            }).replace(/[,:/]/g, '-');
            XLSX.writeFile(workbook, `${fileNamePrefix}-${formattedDate}.xlsx`);
            Utils.showSuccess("Xuất Excel thành công!");
        },

        exportAllToExcel: async () => {
            try {
                Utils.showSpinner();
                const exportButton = document.getElementById("export-scrap-btn");
                exportButton.disabled = true;
                exportButton.textContent = "Đang xuất...";

                const response = await fetch(`http://10.220.130.119:9090/api/Search/ExportExcelKhoOk`, {
                    method: 'GET',
                    headers: { 'Accept': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' }
                });

                if (!response.ok) throw new Error("Không thể xuất dữ liệu!");

                const blob = await response.blob();
                const url = window.URL.createObjectURL(blob);
                const link = document.createElement('a');
                link.href = url;
                link.download = `KhoPhe-${new Date().toISOString()}.xlsx`;
                document.body.appendChild(link);
                link.click();i
                link.remove();
                Utils.showSuccess("Xuất Excel thành công!");
            } catch (error) {
                Utils.showError("Lỗi khi xuất Excel: " + error.message);
            } finally {
                Utils.hideSpinner();
                exportButton.disabled = false;
                exportButton.textContent = "xuất all...";
            }
        }
    };

    // Nhóm hàm cho mượn
    const Borrow = {
        selectedSNs: [],

        updateSelected: () => {
            Borrow.selectedSNs = Array.from(document.querySelectorAll('.sn-checkbox:checked'))
                .map(cb => cb.getAttribute('data-serial-number'));
            const borrowBtn = document.getElementById('borrow-btn');
            if (borrowBtn) {
                borrowBtn.style.display = Borrow.selectedSNs.length > 0 ? 'inline-block' : 'none';
            }
        },

        borrowSelected: async () => {
            if (Borrow.selectedSNs.length === 0) return;

            const { value: borrower } = await Swal.fire({
                title: 'Tên người mượn',
                input: 'text',
                inputPlaceholder: 'Nhập tên người mượn',
                showCancelButton: true,
                confirmButtonText: 'Xác nhận',
                cancelButtonText: 'Hủy'
            });

            if (!borrower) return;

            try {
                Utils.showSpinner();
                const response = await fetch(`${API_BASE_URL}/BorrowKhoOk`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ serialNumbers: Borrow.selectedSNs, borrower})
                });

                if (!response.ok) throw new Error('Không thể cho mượn kho phế!');

                const data = await response.json();
                if (data.success) {
                    Utils.showSuccess(`Đã cho mượn ${data.totalBorrowed} SN`);
                    searchResultsSN = searchResultsSN.filter(sn => !Borrow.selectedSNs.includes(sn.serialNumber));
                    Render.renderTable(searchResultsSN, 'results-body');
                    Borrow.updateSelected();
                    Utils.displayTotalResults(searchResultsSN.length);
                } else {
                    Utils.showError(data.message || 'Lỗi khi cho mượn!');
                }
            } catch (error) {
                Utils.showError('Lỗi khi cho mượn: ' + error.message);
            } finally {
                Utils.hideSpinner();
            }
        }
    };

    // Nhóm hàm nhập kho
    const Import = {
        handleTrayInput: () => {
            const trayInput = document.querySelector('input[name="location"]');
            let trayData = [];
            let maxSlots = 160;

            trayInput.addEventListener("input", function () {
                let location = trayInput.value.trim().toUpperCase();
                const locationPattern = /^([A-Z]+)(\d+)-(\d+)/;
                const match = locationPattern.exec(location);

                if (match) {
                    const shelf = match[1];
                    const column = match[2];
                    const level = match[3];
                    document.querySelector('input[name="shelf"]').value = shelf.toUpperCase();
                    document.querySelector('input[name="column"]').value = column;
                    document.querySelector('input[name="level"]').value = level;
                } else {
                    document.querySelector('input[name="shelf"]').value = "";
                    document.querySelector('input[name="column"]').value = "";
                    document.querySelector('input[name="level"]').value = "";
                    document.getElementById("serial-count").textContent = "Tổng số serial đã nhập: 0/...";
                    trayData = [];
                    document.getElementById("serial-limit-warning").style.display = "none";
                }
            });

            trayInput.addEventListener("keydown", function (event) {
                if (event.key === "Enter") {
                    event.preventDefault();
                    document.getElementById("sn-import").focus();
                }
            });
        },

        updateSerialDetails: async (serialNumbers) => {
            const modelNames = [];
            const productLines = [];

            for (const serial of serialNumbers) {
                try {
                    const response = await fetch(`http://10.220.130.119:9090/api/Product/GetSNInfo?serialNumber=${serial}`);
                    const data = await response.json();
                    if (data.success) {
                        modelNames.push(data.modelName || "");
                        productLines.push(data.productLine || "");
                    } else {
                        modelNames.push("");
                        productLines.push("");
                    }
                } catch (error) {
                    console.error(`Lỗi khi lấy thông tin SN ${serial}:`, error);
                    modelNames.push("");
                    productLines.push("");
                }
            }

            document.querySelector(".model-name-field").value = modelNames.join("\n");
            document.querySelector(".product-line-field").value = productLines.join("\n");
        },

        handleEntry: async () => {
            const shelf = document.querySelector('input[name="shelf"]').value;
            const column = document.querySelector('input[name="column"]').value;
            const level = document.querySelector('input[name="level"]').value;
            const currentUser = document.getElementById("entryPerson").value;
            const serialNumbers = Utils.getSerialNumbersFromInput();

            if (!shelf || !column || !level || serialNumbers.length === 0) {
                Utils.showWarning("Vui lòng điền đầy đủ thông tin vị trí và danh sách serial!");
                return;
            }

            try {
                const saveResponse = await fetch(`${API_BASE_URL}/AddOk`, {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({
                        shelf: shelf,
                        column: parseInt(column),
                        level: parseInt(level),
                        serialNumbers: serialNumbers,
                        entryPerson: currentUser
                    })
                });

                const saveData = await saveResponse.json();

                if (saveData.success) {
                    const failedSerials = saveData.results.filter(result => !result.success);
                    if (failedSerials.length > 0) {
                        const failedMessages = failedSerials.map(result => `Serial: ${result.serialNumber}, Lỗi: ${result.message}`).join("\n");
                        Utils.showError(`Có lỗi xảy ra với các serial:\n${failedMessages}`);
                        return;
                    }
                    Utils.showSuccess("Nhập kho thành công!");
                    document.querySelector('input[name="location"]').dispatchEvent(new Event("input"));
                    document.getElementById("sn-import").value = "";
                    document.querySelector(".model-name-field").value = "";
                    document.querySelector(".product-line-field").value = "";
                    Utils.resetUI();
                    await Total.updateTotalScrapAndCategories();
                } else {
                    Utils.showError(saveData.message || "Lỗi khi nhập kho!");
                }
            } catch (error) {
                console.error("Lỗi khi nhập kho:", error);
                Utils.showError("Lỗi khi nhập kho: " + error.message);
            }
        },

        handleSerialInput: () => {
            const serialInput = document.getElementById("sn-import");
            serialInput.addEventListener("input", async function () {
                const serialNumbers = Utils.getSerialNumbersFromInput();
                await Import.updateSerialDetails(serialNumbers);
            });
        }
    };

    // Nhóm hàm render
    const Render = {
        renderTable: (results, targetElementId) => {
            const resultsBody = document.getElementById(targetElementId);
            if (!resultsBody) {
                console.error(`Phần tử #${targetElementId} không tồn tại!`);
                return;
            }

            resultsBody.innerHTML = "";
            if (!results || results.length === 0) {
                resultsBody.innerHTML = "<tr><td colspan='18'>Không tìm thấy kết quả!</td></tr>";
                return;
            }

            results.forEach(result => {
                const row = `
                    <tr>
                        <td><input type="checkbox" class="sn-checkbox" data-serial-number="${result.serialNumber || ""}"/></td>
                        <td>${result.serialNumber || ""}</td>
                        <td>${result.modelName || ""}</td>
                        <td>${result.moNumber || ""}</td>
                        <td>${result.wipGroup || ""}</td>
                        <td title="${result.testGroup || ""}">${result.testGroup || ""}</td>
                        <td title="${result.reasonCode || ""}">${result.reasonCode || ""}</td>
                        <td title="${result.testCode || ""}">${result.testCode || ""}</td>
                        <td title="${result.data1 || ""}">${result.data1 || ""}</td>
                        <td>${result.shelfCode || ""}</td>
                        <td>${result.columnNumber || ""}</td>
                        <td>${result.levelNumber || 0}</td>
                        <td>${result.entryDate || ""}</td>
                        <td title="${result.entryPerson || ""}">${result.entryPerson || ""}</td>
                        <td title="${result.borrowStatus || ""}">${result.borrowStatus || ""}</td>
                        <td title="${result.borrowDate || ""}">${result.borrowDate || ""}</td>
                        <td title="${result.borrowPerson || ""}">${result.borrowPerson || ""}</td>
                        <td title="${result.note || ""}">${result.note || ""}</td>
                    </tr>`;
                resultsBody.innerHTML += row;
            });
            Borrow.updateSelected();
        }
    };

    // Nhóm hàm tổng kho ok
    const Total = {
        updateTotalScrapAndCategories: async () => {
            try {
                // Gọi API tổng kho ok
                const totalResponse = await fetch(API_TOTAL_SCRAP, {
                    method: 'GET',
                    headers: { 'Content-Type': 'application/json' }
                });

                if (!totalResponse.ok) throw new Error('Không thể tải tổng kho phế');

                const totalData = await totalResponse.json();
                if (totalData.success) {
                    document.getElementById("total-stock").textContent = totalData.totalCount || 0;
                } else {
                    throw new Error(totalData.message || 'Lỗi khi tải tổng kho phế');
                }
            } catch (error) {
                console.error('Lỗi khi tải số lượng:', error);
                Utils.showError(`Không thể tải số lượng: ${error.message}`);
            }
        }
    };

    // Khởi tạo sự kiện
    const init = () => {
        document.addEventListener("DOMContentLoaded", () => {
            // Gọi hàm cập nhật tổng số và phân loại
            Total.updateTotalScrapAndCategories();

            // Tìm kiếm SN
            const submitSnBtn = document.getElementById("submit-sn-btn");
            if (submitSnBtn) {
                submitSnBtn.addEventListener("click", Search.searchSerialNumbers);
            } else {
                console.error("Phần tử #submit-sn-btn không tồn tại!");
            }

            // Xuất Excel tìm kiếm
            const exportSnExcelBtn = document.getElementById("export-sn-excel-btn");
            if (exportSnExcelBtn) {
                exportSnExcelBtn.addEventListener("click", () => Export.exportToExcel(searchResultsSN, "SearchResultsSN"));
            } else {
                console.error("Phần tử #export-sn-excel-btn không tồn tại!");
            }

            // Xuất kho
            const exportSnBtn = document.getElementById("export-sn-btn");
            if (exportSnBtn) {
                exportSnBtn.addEventListener("click", Export.exportSerialNumbers);
            } else {
                console.error("Phần tử #export-sn-btn không tồn tại!");
            }

            // Xuất toàn bộ Excel
            const exportButtons = document.querySelectorAll("#export-scrap-btn");
            exportButtons.forEach(button => {
                button.addEventListener("click", Export.exportAllToExcel);
            });

            // Cho mượn
            const borrowBtn = document.getElementById("borrow-btn");
            if (borrowBtn) {
                borrowBtn.addEventListener("click", Borrow.borrowSelected);
            }

            document.addEventListener("change", (e) => {
                if (e.target.id === "select-all-checkbox") {
                    const checked = e.target.checked;
                    document.querySelectorAll('.sn-checkbox').forEach(cb => cb.checked = checked);
                }
                if (e.target.classList.contains('sn-checkbox') || e.target.id === "select-all-checkbox") {
                    Borrow.updateSelected();
                }
            });

            // Nhập kho
            const entryButton = document.getElementById("entry-btn");
            if (entryButton) {
                entryButton.addEventListener("click", Import.handleEntry);
            } else {
                console.error("Phần tử #entry-btn không tồn tại!");
            }

            // Xử lý nhập vị trí khay
            if (document.querySelector('input[name="location"]')) {
                Import.handleTrayInput();
            }

            // Xử lý nhập SN
            if (document.getElementById("sn-import")) {
                Import.handleSerialInput();
            }
        });
    };

    return {
        init
    };
})();

// Khởi động ứng dụng
KhoScrapManager.init();