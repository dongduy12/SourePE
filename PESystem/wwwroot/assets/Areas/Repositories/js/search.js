let searchResultsSN = []; // Lưu kết quả tìm thấy
let selectedSerialNumbers = [];

async function searchSerialNumbers() {
    const snInput = document.getElementById("sn-input")?.value || "";
    const serialNumbers = snInput.split('\n')
        .map(sn => sn.trim().toUpperCase())
        .filter(sn => sn && /^[A-Za-z0-9-]+$/.test(sn));

    if (serialNumbers.length === 0) {
        showError("Vui lòng nhập ít nhất một Serial Number hợp lệ!");
        return;
    }

    try {
        const response = await fetch('http://10.220.130.119:9090/api/Search/SearchProductsBySN', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(serialNumbers)
        });

        if (!response.ok) throw new Error('Không thể tải dữ liệu từ Server');

        const data = await response.json();
        console.log(data.success, "hihihihi");
        if (data.success) {
            searchResultsSN = data.results || [];
            renderTable(searchResultsSN, 'results-body');
            displayTotalResults(data.totalFound || 0);
            document.getElementById("search-results").style.display = "block";
            document.getElementById("export-sn-excel-btn").style.display = "inline-block";
            document.getElementById("export-excel-btn").style.display = "none";
            document.getElementById("button-action").classList.remove("hidden");
            showSuccess(`Tìm thấy ${data.totalFound || 0}/${data.totalNotFound + data.totalFound || 0} SN`);
        } else {
            showError(data.message || "Không tìm thấy kết quả!");
            searchResultsSN = [];
            renderTable([], 'results-body');
        }
    } catch (error) {
        showError("Lỗi khi tìm kiếm: " + error.message);
    } finally {
        hideSpinner();
    }
}

function renderTable(results, targetElementId) {
    const resultsBody = document.getElementById(targetElementId);
    if (!resultsBody) {
        console.error(`Phần tử #${targetElementId} không tồn tại!`);
        return;
    }

    resultsBody.innerHTML = "";
    if (!results || results.length === 0) {
        resultsBody.innerHTML = "<tr><td colspan='22'>Không tìm thấy kết quả!</td></tr>";
        return;
    }

    results.forEach(result => {
        const row = `
            <tr>
                <td><input type="checkbox" class="sn-checkbox" data-serial-number="${result.serialNumber || ""}"/></td>
                <td>${result.serialNumber || ""}</td>
                <td title="${result.productLine || ""}">${result.productLine || ""}</td>
                <td>${result.modelName || ""}</td>
                <td>${result.moNumber || ""}</td>
                <td>${result.wipGroup || ""}</td>
                <td>${result.workFlag || ""}</td>
                <td title="${result.testGroup || ""}">${result.testGroup || ""}</td>
                <td title="${result.reasonCode || ""}">${result.reasonCode || ""}</td>
                <td title="${result.testCode || ""}">${result.testCode || ""}</td>
                <td title="${result.data1 || ""}">${result.data1 || ""}</td>
                <td title="${result.kanBanWIP || ""}">${result.kanBanWIP || ""}</td>
                <td title="${result.holdReason || ""}">${result.holdReason || ""}</td>
                <td title="${result.blockReason || ""}">${result.blockReason || ""}</td>
                <td>${result.shelfCode || ""}</td>
                <td>${result.columnNumber || ""}</td>
                <td>${result.levelNumber || ""}</td>
                <td>${result.trayNumber || ""}</td>
                <td>${result.positionInTray || ""}</td>
                <td>${result.entryDate || ""}</td>
                <td title="${result.entryPerson || ""}">${result.entryPerson || ""}</td>
                <td>${result.borrowStatus || ""}</td>
                <td>${result.borrowDate || ""}</td>
                <td title="${result.borrowPerson || ""}">${result.borrowPerson || ""}</td>
                <td title="${result.note || ""}">${result.note || ""}</td>
                <td title="${result.actionNe || ""}">${result.actionNe || ""}</td>
                <td title="${result.scrap || ""}">${result.scrap || ""}</td>
            </tr>`;
        resultsBody.innerHTML += row;
    });
}

document.getElementById("submit-sn-btn").addEventListener("click", searchSerialNumbers);

// Advanced Search
const baseUrl = "http://10.220.130.119:9090/api/Search/AdvancedSearch";
let currentPage = 1;
let totalPages = 1;
let searchResults = [];

async function performAdvancedSearch(page = 1) {
    showSpinner();
    const requestData = {
        BorrowStatus: document.getElementById("borrowStatus")?.value || "",
        WIPGroup: Array.from(document.getElementById("wipGroup-select")?.selectedOptions || []).map(opt => opt.value),
        TestCode: document.getElementById("testCode")?.value || "",
        Data1: document.getElementById("data1")?.value || "",
        Sfg: Array.from(document.getElementById("modelName-select")?.selectedOptions || []).map(opt => opt.value),
    };

    try {
        const response = await fetch(`${baseUrl}?page=${page}&pageSize=50`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(requestData),
        });

        if (!response.ok) throw new Error("Lỗi khi gọi API!");

        const data = await response.json();
        if (data.success && data.results?.length > 0) {
            searchResults = data.results;
            renderTable(searchResults, 'results-body');
            currentPage = data.currentPage || 1;
            totalPages = data.totalPages || 1;
            updatePagination();
            displayTotalResults(data.totalItems || 0);
            document.getElementById("search-results").style.display = "block";
            document.getElementById("export-excel-btn").style.display = "inline-block";
            document.getElementById("export-sn-excel-btn").style.display = "none";
            document.getElementById("create-list-sn-btn").style.display = "none";
            document.getElementById("button-action").classList.remove("hidden");
        } else {
            searchResults = [];
            renderTable([], 'results-body');
            displayTotalResults(0);
            document.getElementById("export-excel-btn").style.display = "none";
            showError(data.message || "Không tìm thấy kết quả!");
        }
    } catch (error) {
        showError("Lỗi khi tìm kiếm nâng cao: " + error.message);
    } finally {
        hideSpinner();
    }
}

function updatePagination() {
    const paginationInfo = document.getElementById("pagination-info");
    if (!paginationInfo) {
        console.error("Phần tử #pagination-info không tồn tại!");
        return;
    }
    paginationInfo.textContent = `Trang ${currentPage}/${totalPages}`;

    const prevBtn = document.getElementById("prev-page-btn");
    const nextBtn = document.getElementById("next-page-btn");
    if (prevBtn && nextBtn) {
        prevBtn.style.display = currentPage > 1 ? "inline-block" : "none";
        nextBtn.style.display = currentPage < totalPages ? "inline-block" : "none";
    }
}

function displayTotalResults(total) {
    const totalResults = document.getElementById("total-results");
    if (!totalResults) {
        console.error("Phần tử #total-results không tồn tại!");
        return;
    }
    totalResults.textContent = `Kết quả: ${total}`;
}

function changePage(direction) {
    const newPage = currentPage + direction;
    if (newPage >= 1 && newPage <= totalPages) {
        performAdvancedSearch(newPage);
    }
}

document.getElementById("advanced-search-btn").addEventListener("click", () => performAdvancedSearch());
document.getElementById("prev-page-btn").addEventListener("click", () => changePage(-1));
document.getElementById("next-page-btn").addEventListener("click", () => changePage(1));

// Export Excel
function exportToExcel(data, fileNamePrefix) {
    if (!data || data.length === 0) {
        showError("Không có dữ liệu để xuất!");
        return;
    }

    const worksheetData = data.map(result => ({
        "SERIAL_NUMBER": result.serialNumber || "",
        "PRODUCT_LINE": result.productLine || "",
        "MODEL_NAME": result.modelName || "",
        "MO_NUMBER": result.moNumber || "",
        "WIP_GROUP": result.wipGroup || "",
        "WORK_FLAG": result.workFlag || "",
        "TEST_GROUP": result.testGroup || "",
        "REASON_CODE    ": result.reasonCode || "",
        "TEST_CODE": result.testCode || "",
        "ERROR_DESC": result.data1 || "",
        "SHELF": result.shelfCode || "",
        "COLUMN": result.columnNumber || "",
        "LEVEL": result.levelNumber || "",
        "TRAY": result.trayNumber || "",
        "POSITION_IN_TRAY": result.positionInTray || "",
        "ENTRY_DATE": result.entryDate || "",
        "ENTRY_PERSON": result.entryPerson || "",
        "STATUS": result.borrowStatus || "",
        "BORROW_DATE": result.borrowDate || "",
        "BORROW_PERSON": result.borrowPerson || "",
        "NOTE": result.note || "",
        "ACTION": result.actionNe || "",
        "SCRAP": result.scrap || "",
    }));

    const workbook = XLSX.utils.book_new();
    const worksheet = XLSX.utils.json_to_sheet(worksheetData);
    XLSX.utils.book_append_sheet(workbook, worksheet, "Search Results");

    const formattedDate = new Date().toLocaleString('vi-VN', {
        year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit'
    }).replace(/[,:/]/g, '-');
    XLSX.writeFile(workbook, `${fileNamePrefix}-${formattedDate}.xlsx`);
}

document.getElementById("export-sn-excel-btn").addEventListener("click", () => {
    exportToExcel(searchResultsSN, "SearchResultsSN");
});

document.getElementById("export-excel-btn").addEventListener("click", async () => {
    showSpinner();
    try {
        const requestData = {
            BorrowStatus: document.getElementById("borrowStatus").value,
            WIPGroup: Array.from(document.getElementById("wipGroup-select").selectedOptions).map(opt => opt.value),
            TestCode: document.getElementById("testCode").value,
            Data1: document.getElementById("data1").value,
            Sfg: Array.from(document.getElementById("modelName-select").selectedOptions).map(opt => opt.value),
        };

        const allResults = await fetchAllPagesParallel(requestData);
        exportToExcel(allResults, "AdvancedSearchResults");
    } catch (error) {
        showError("Lỗi khi xuất Excel: " + error.message);
        hideSpinner();
    } finally {
        hideSpinner();
    }
});

async function fetchAllPagesParallel(requestData) {
    const firstPageResponse = await fetch(`${baseUrl}?page=1&pageSize=50`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(requestData),
    });
    const firstPageData = await firstPageResponse.json();
    const totalPages = firstPageData.totalPages || 1;

    const pagePromises = Array.from({ length: totalPages }, (_, i) =>
        fetch(`${baseUrl}?page=${i + 1}&pageSize=50`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(requestData),
        }).then(res => res.json())
    );

    const pageResults = await Promise.all(pagePromises);
    return pageResults.flatMap(data => (data.success && data.results ? data.results : []));
}

// Borrow and Note Features
function setupBorrowFeature() {
    const borrowBtn = document.getElementById("borrow-btn");
    const resultsBody = document.getElementById("results-body");

    if (!resultsBody || !borrowBtn) return;

    resultsBody.addEventListener("change", (e) => {
        if (e.target.classList.contains("sn-checkbox")) {
            selectedSerialNumbers = Array.from(resultsBody.querySelectorAll(".sn-checkbox:checked"))
                .map(cb => cb.dataset.serialNumber || "");
            borrowBtn.disabled = selectedSerialNumbers.length === 0;
        }
    });

    borrowBtn.addEventListener("click", () => {
        if (selectedSerialNumbers.length === 0) {
            showError("Vui lòng chọn ít nhất một Serial Number!");
            return;
        }

        Swal.fire({
            title: "Xác nhận cho mượn",
            input: "text",
            inputLabel: "Tên người mượn",
            inputPlaceholder: "Nhập tên người mượn",
            showCancelButton: true,
            confirmButtonText: "Xác nhận",
            cancelButtonText: "Hủy",
            showLoaderOnConfirm: true,
            preConfirm: async (borrower) => {
                if (!borrower || borrower.trim() === "") {
                    Swal.showValidationMessage("Vui lòng nhập tên người mượn!");
                    return false;
                }

                try {
                    showSpinner();
                    const borrowResponse = await fetch("http://10.220.130.119:9090/api/Borrow/Borrow", {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ Borrower: borrower.trim(), serialNumbers: selectedSerialNumbers }),
                    });

                    if (!borrowResponse.ok) throw new Error("Lỗi khi gửi yêu cầu mượn");

                    const handOverResponse = await fetch("http://10.220.130.119:9090/api/RepairStatus/hand-over-status", {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({
                            serialNumbers: selectedSerialNumbers.join(","),
                            handOverStatus: "WAITING_HAND_OVER",
                            tag: "Giao(Cho mượn từ Kho)"
                        }),
                    });

                    const result = await handOverResponse.json();
                    if (result.message.replace(/"/g, '') !== "OK") throw new Error("Lỗi cập nhật trạng thái bàn giao");

                    return { success: true };
                } catch (error) {
                    Swal.showValidationMessage(`Lỗi: ${error.message}`);
                    return false;
                } finally {
                    hideSpinner();
                }
            },
            allowOutsideClick: () => !Swal.isLoading()
        }).then((result) => {
            if (result.isConfirmed && result.value.success) {
                showSuccess("Cho mượn thành công!");
                location.reload(); // Tải lại trang nếu cần
            }
        });
    });
}

function setupNoteFeature() {
    const updateNoteBtn = document.getElementById("update-note-btn");
    const resultsBody = document.getElementById("results-body");

    if (!resultsBody || !updateNoteBtn) return;

    resultsBody.addEventListener("change", (e) => {
        if (e.target.classList.contains("sn-checkbox")) {
            selectedSerialNumbers = Array.from(resultsBody.querySelectorAll(".sn-checkbox:checked"))
                .map(cb => cb.dataset.serialNumber || "");
            updateNoteBtn.disabled = selectedSerialNumbers.length === 0;
        }
    });

    updateNoteBtn.addEventListener("click", () => {
        if (selectedSerialNumbers.length === 0) {
            showError("Vui lòng chọn ít nhất một Serial Number!");
            return;
        }

        Swal.fire({
            title: "Cập nhật ghi chú",
            input: "textarea",
            inputLabel: "Nhập ghi chú",
            inputPlaceholder: "Nhập nội dung...",
            showCancelButton: true,
            confirmButtonText: "Cập nhật",
            cancelButtonText: "Hủy",
            showLoaderOnConfirm: true,
            inputAttributes: {
                rows: 3, // Số dòng cho textarea
                //maxlength: 500 // Giới hạn ký tự
            },
            preConfirm: async (note) => {
                if (!note || note.trim() === "") {
                    Swal.showValidationMessage("Vui lòng nhập ghi chú!");
                    return false;
                }

                try {
                    showSpinner();
                    const response = await fetch("http://10.220.130.119:9090/api/Product/UpdateProduct", {
                        method: "PUT",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ serialNumbers: selectedSerialNumbers, note: note.trim() }),
                    });

                    if (!response.ok) {
                        const errorData = await response.json();
                        throw new Error(errorData.message || "Không thể cập nhật ghi chú");
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
        }).then((result) => {
            if (result.isConfirmed && result.value.success) {
                showSuccess("Cập nhật ghi chú thành công!");
                location.reload(); // Tải lại trang nếu cần
            }
        });
    });
}

function setupActionFeature() {
    const updateActionBtn = document.getElementById("update-action-btn");
    const resultsBody = document.getElementById("results-body");

    if (!resultsBody || !updateActionBtn) return;

    resultsBody.addEventListener("change", (e) => {
        if (e.target.classList.contains("sn-checkbox")) {
            selectedSerialNumbers = Array.from(resultsBody.querySelectorAll(".sn-checkbox:checked"))
                .map(cb => cb.dataset.serialNumber || "");
            updateActionBtn.disabled = selectedSerialNumbers.length === 0;
        }
    });

    updateActionBtn.addEventListener("click", () => {
        if (selectedSerialNumbers.length === 0) {
            showError("Vui lòng chọn ít nhất một Serial Number!");
            return;
        }

        Swal.fire({
            title: "Cập nhật Action",
            input: "textarea",
            inputLabel: "Nhập action",
            inputPlaceholder: "Nhập nội dung...",
            showCancelButton: true,
            confirmButtonText: "Cập nhật",
            cancelButtonText: "Hủy",
            showLoaderOnConfirm: true,
            inputAttributes: {
                rows: 3, // Số dòng cho textarea
                maxlength: 500 // Giới hạn ký tự nếu cần
            },
            preConfirm: async (action) => {
                if (!action || action.trim() === "") {
                    Swal.showValidationMessage("Vui lòng nhập action!");
                    return false;
                }

                try {
                    showSpinner();
                    const response = await fetch("http://10.220.130.119:9090/api/Product/UpdateAction", {
                        method: "PUT",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ serialNumbers: selectedSerialNumbers, action: action.trim() }),
                    });

                    if (!response.ok) {
                        const errorData = await response.json();
                        throw new Error(errorData.message || "Không thể cập nhật action");
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
        }).then((result) => {
            if (result.isConfirmed && result.value.success) {
                showSuccess("Cập nhật action thành công!");
                location.reload(); // Tải lại trang nếu cần
            }
        });
    });
}


// Khởi tạo tất cả khi DOM ready
document.addEventListener("DOMContentLoaded", function () {
    const selectAllCheckbox = document.getElementById("select-all-checkbox");
    const resultsBody = document.getElementById("results-body");
    const borrowBtn = document.getElementById("borrow-btn");
    const updateNoteBtn = document.getElementById("update-note-btn");
    const updateActionBtn = document.getElementById("update-action-btn");

    // Xử lý checkbox "Chọn tất cả"
    if (selectAllCheckbox && resultsBody) {
        selectAllCheckbox.addEventListener("change", function () {
            const allCheckboxes = resultsBody.querySelectorAll(".sn-checkbox");
            allCheckboxes.forEach(checkbox => {
                checkbox.checked = selectAllCheckbox.checked;
            });
            updateButtonState();
        });
    }

    // Xử lý các checkbox con
    if (resultsBody) {
        resultsBody.addEventListener("change", function (e) {
            if (e.target.classList.contains("sn-checkbox")) {
                const allCheckboxes = resultsBody.querySelectorAll(".sn-checkbox");
                const allChecked = Array.from(allCheckboxes).every(cb => cb.checked);
                const anyChecked = Array.from(allCheckboxes).some(cb => cb.checked);

                if (selectAllCheckbox) {
                    selectAllCheckbox.checked = allChecked;
                    selectAllCheckbox.indeterminate = !allChecked && anyChecked;
                }
                updateButtonState();
            }
        });
    }

    // Hàm cập nhật trạng thái nút
    function updateButtonState() {
        selectedSerialNumbers = Array.from(resultsBody.querySelectorAll(".sn-checkbox:checked"))
            .map(cb => cb.dataset.serialNumber || "");
        if (borrowBtn) borrowBtn.disabled = selectedSerialNumbers.length === 0;
        if (updateNoteBtn) updateNoteBtn.disabled = selectedSerialNumbers.length === 0;
        if (updateActionBtn) updateActionBtn.disabled = selectedSerialNumbers.length === 0;
    }

    // Khởi tạo các tính năng
    setupBorrowFeature();
    setupNoteFeature();
    setupActionFeature();

    //Select2
    $('#wipGroup-select').select2({
        placeholder: "WIP Group...",
        allowClear: true,
        width: '100%'
    }).on('select2:open', function () {
        console.log("Select2 opened for wipGroup-select");
    });

    $('#modelName-select').select2({
        placeholder: "Model Name",
        allowClear: true,
        width: '100%'
    }).on('select2:open', function () {
    });

    // Gọi API
    fetch('http://10.220.130.119:9090/api/Search/GetWipGroups')
        .then(response => {
            if (!response.ok) throw new Error('API lỗi');
            return response.json();
        })
        .then(data => {
            console.log("API data:", data);
            $('#wipGroup-select').empty();
            data.forEach(item => {
                $('#wipGroup-select').append(new Option(item, item));
            });
            $('#wipGroup-select').trigger('change');
        })
        .catch(error => console.error("Lỗi API:", error));

    fetch('http://10.220.130.119:9090/api/Search/ModelName')
        .then(response => {
            if (!response.ok) throw new Error('API lỗi');
            return response.json();
        })
        .then(data => {
            console.log("API data:", data);
            $('#modelName-select').empty();
            data.forEach(item => {
                $('#modelName-select').append(new Option(item, item));
            });
            $('#modelName-select').trigger('change');
        })
        .catch(error => console.error("Lỗi API:", error));

});
// Hiển thị spinner
function showSpinner() {
    document.getElementById("spinner-overlay").style.display = "flex";
}
// Ẩn spinner
function hideSpinner() {
    const spinner = document.getElementById('spinner-overlay');
    if (spinner) {
        spinner.style.display = 'none';
        console.log("Spinner hidden");
    }

    // Không đóng modal thủ công trong hideSpinner, để modal tự xử lý
    // Chỉ xóa backdrop nếu không có modal nào hiển thị
    const hasVisibleModal = document.querySelector('.modal.show');
    if (!hasVisibleModal) {
        const backdrop = document.querySelector('.modal-backdrop');
        if (backdrop) {
            backdrop.remove();
            console.log("Backdrop removed (no visible modal)");
        }
    } else {
        console.log("Modal still visible, skipping backdrop removal");
    }
}
