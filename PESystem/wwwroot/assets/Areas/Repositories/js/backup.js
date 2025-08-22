//====================Search SN=======================
let searchResultsSN = []; // Biến toàn cục để lưu kết quả
function submitSerialNumberForm() {

    // Lấy danh sách SN từ textarea
    const serialNumbers = document.getElementById("sn-input").value
        .split('\n')
        .map(sn => sn.trim())
        .filter(sn => sn); // Loại bỏ các dòng trống

    if (serialNumbers.length === 0) {
        alert("Vui lòng nhập ít nhất một Serial Number!");
        return;
    }

    // Gọi API Search SN
    fetch('http://localhost:5125/api/Search/SearchProductsBySN', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(serialNumbers)
    })
        .then(response => {
            if (!response.ok) {
                throw new Error('Failed to fetch search results');
            }
            return response.json();
        })
        .then(data => {
            if (data.success) {
                console.log("Dữ liệu trả về từ API:", data);
                // Lưu kết quả vào biến toàn cục
                searchResultsSN = data.results;

                // Xóa nội dung cũ
                const resultsBody = document.getElementById("results-body");
                resultsBody.innerHTML = "";

                // Hiển thị kết quả
                data.results.forEach(result => {
                    const row = `
                    <tr>
                        <td>${result.serialNumber || "N/A"}</td>
                        <td>${result.productLine || "N/A"}</td>
                        <td>${result.modelName || "N/A"}</td>
                        <td>${result.moNumber || "N/A"}</td>
                        <td>${result.wipGroup || "N/A"}</td>
                        <td>${result.testGroup || "N/A"}</td>
                        <td>${result.reasonCode || "N/A"}</td>
                        <td>${result.data1 || "N/A"}</td>
                        <td>${result.testCode || "N/A"}</td>
                        <td>${result.shelfCode || "N/A"}</td>
                        <td>${result.columnNumber || "N/A"}</td>
                        <td>${result.levelNumber || "N/A"}</td>
                        <td>${result.trayNumber || "N/A"}</td>
                        <td>${result.positionInTray || "N/A"}</td>
                        <td>${result.entryDate || "N/A"}</td>
                        <td>${result.entryPerson || "N/A"}</td>
                        <td>${result.borrowStatus || "N/A"}</td>
                        <td>${result.borrowDate || "N/A"}</td>
                        <td>${result.borrowPerson || "N/A"}</td>
                    </tr>
                `;
                    resultsBody.innerHTML += row;
                });

                // Hiển thị số lượng kết quả
                alert(`Tìm thấy ${data.totalFound} SN. Không tìm thấy: ${data.totalNotFound}`);
            } else {
                alert(data.message || "Không thể tìm thấy kết quả!");
            }
        })
        .catch(error => {
            console.error('Error fetching search results:', error);
            alert("Đã xảy ra lỗi khi tìm kiếm!");
        });
}


$(document).ready(function () {
    // Khởi tạo Select2 với multiple
    $('#wipGroup-select').select2({
        placeholder: "WIP Group...",
        allowClear: true,
        width: '100%' // Đảm bảo kích thước phù hợp
    });

    // Gọi API để lấy danh sách WIP Groups
    fetch('http://localhost:5125/api/Search/GetWipGroups')
        .then(response => {
            if (!response.ok) {
                throw new Error('Failed to fetch WIP Groups');
            }
            return response.json();
        })
        .then(wipGroups => {
            // Thêm các tùy chọn vào Select2
            wipGroups.forEach(wipGroup => {
                const option = new Option(wipGroup, wipGroup, false, false);
                $('#wipGroup-select').append(option);
            });
            $('#wipGroup-select').trigger('change'); // Cập nhật giao diện
        })
        .catch(error => {
            console.error('Error fetching WIP Groups:', error);
        });
});
$(document).ready(function () {
    // Khởi tạo Select2
    $('#modelName-select').select2({
        placeholder: "Model Name",
        allowClear: true,
        width: '100%' // Đảm bảo kích thước phù hợp
    });

    // Gọi API để lấy danh sách Model Name
    fetch('http://localhost:5125/api/Search/ModelName')
        .then(response => {
            if (!response.ok) {
                throw new Error('Failed to fetch ModelName');
            }
            return response.json();
        })
        .then(modelNames => {
            // Thêm các tùy chọn vào Select2
            modelNames.forEach(modelName => {
                const option = new Option(modelName, modelName, false, false);
                $('#modelName-select').append(option);
            });
            $('#modelName-select').trigger('change'); // Cập nhật giao diện
        })
        .catch(error => {
            console.error('Error fetching Model Name:', error);
        });
});
//====================Xuất Excel=======================
document.getElementById("export-sn-excel-btn").addEventListener("click", () => {
    if (searchResultsSN.length === 0) {
        alert("Không có dữ liệu để xuất!");
        return;
    }

    // Chuyển dữ liệu SN sang định dạng Excel
    const worksheetData = searchResultsSN.map(result => ({
        "Serial Number": result.serialNumber || "",
        "Product Line": result.productLine || "",
        "Model Name": result.modelName || "",
        "Mo Number": result.moNumber || "",
        "WIP Group": result.wipGroup || "",
        "Test Group": result.testGroup || "",
        "Reason Code": result.reasonCode || "",
        "Data1": result.data1 || "",
        "Test Code": result.testCode || "",
        "Shelf Code": result.shelfCode || "",
        "Column Number": result.columnNumber || "",
        "Level Number": result.levelNumber || "",
        "Tray Number": result.trayNumber || "",
        "Position In Tray": result.positionInTray || "",
        "Entry Date": result.entryDate || "",
        "Entry Person": result.entryPerson || "",
        "Borrow Status": result.borrowStatus || "",
        "Borrow Date": result.borrowDate || "",
        "Borrow Person": result.borrowPerson || "",
    }));

    // Tạo workbook và worksheet
    const workbook = XLSX.utils.book_new();
    const worksheet = XLSX.utils.json_to_sheet(worksheetData);
    XLSX.utils.book_append_sheet(workbook, worksheet, "Search Results");

    // Xuất file Excel
    XLSX.writeFile(workbook, `SearchResults-${new Date().toISOString()}.xlsx`);
});
//async function fetchAllPages(requestData) {
//    let allResults = [];
//    let currentPage = 1;
//    let totalPages = 1;

//    try {
//        do {
//            const response = await fetch(`${baseUrl}?page=${currentPage}&pageSize=50`, {
//                method: "POST",
//                headers: { "Content-Type": "application/json" },
//                body: JSON.stringify(requestData),
//            });

//            if (!response.ok) throw new Error("Lỗi API!");

//            const data = await response.json();
//            if (data.success && data.results) {
//                allResults = allResults.concat(data.results); // Gộp kết quả
//                currentPage = data.currentPage + 1;
//                totalPages = data.totalPages;
//            } else {
//                break; // Nếu không có thêm kết quả
//            }
//        } while (currentPage <= totalPages);

//        return allResults;
//    } catch (error) {
//        console.error("Lỗi khi lấy dữ liệu:", error.message);
//        alert("Không thể tải toàn bộ dữ liệu!");
//        return [];
//    }
//}

//document.getElementById("export-excel-btn").addEventListener("click", () => {
//    if (searchResults.length === 0) {
//        alert("Không có dữ liệu để xuất!");
//        return;
//    }

//    // Chuyển dữ liệu sang định dạng Excel
//    const worksheetData = searchResults.map(result => ({
//        "Serial Number": result.serialNumber || "N/A",
//        "Product Line": result.productLine || "N/A",
//        "Model Name": result.modelName || "N/A",
//        "Mo Number": result.moNumber || "N/A",
//        "WIP Group": result.wipGroup || "N/A",
//        "Test Group": result.testGroup || "N/A",
//        "Reason Code": result.reasonCode || "N/A",
//        "Data1": result.data1 || "N/A",
//        "Test Code": result.testCode || "N/A",
//        "Shelf Code": result.shelfCode || "N/A",
//        "Column Number": result.columnNumber || "N/A",
//        "Level Number": result.levelNumber || "N/A",
//        "Tray Number": result.trayNumber || "N/A",
//        "Position In Tray": result.positionInTray || "N/A",
//        "Entry Date": result.entryDate || "N/A",
//        "Entry Person": result.entryPerson || "N/A",
//        "Borrow Status": result.borrowStatus || "N/A",
//        "Borrow Date": result.borrowDate || "N/A",
//        "Borrow Person": result.borrowPerson || "N/A",
//    }));

//    // Tạo workbook và worksheet
//    const workbook = XLSX.utils.book_new();
//    const worksheet = XLSX.utils.json_to_sheet(worksheetData);
//    XLSX.utils.book_append_sheet(workbook, worksheet, "Search Results");

//    // Xuất file Excel
//    XLSX.writeFile(workbook, `SearchResults-${new Date().toISOString()}.xlsx`);
//});
//document.getElementById("export-excel-btn").addEventListener("click", async () => {
//    const requestData = {
//        BorrowStatus: document.getElementById("borrowStatus").value,
//        WIPGroup: Array.from(document.getElementById("wipGroup-select").selectedOptions).map(opt => opt.value),
//        TestCode: document.getElementById("testCode").value,
//        Data1: document.getElementById("data1").value,
//        Sfg: Array.from(document.getElementById("modelName-select").selectedOptions).map(opt => opt.value),
//    };

//    // Hiển thị trạng thái đang tải
//    const exportButton = document.getElementById("export-excel-btn");
//    exportButton.disabled = true;
//    exportButton.textContent = "Đang xuất...";

//    try {
//        // Lấy toàn bộ dữ liệu từ tất cả các trang
//        const allResults = await fetchAllPages(requestData);

//        if (allResults.length === 0) {
//            alert("Không có dữ liệu để xuất!");
//            return;
//        }

//        // Chuyển dữ liệu sang định dạng Excel
//        const worksheetData = allResults.map(result => ({
//            "Serial Number": result.serialNumber || "N/A",
//            "Product Line": result.productLine || "N/A",
//            "Model Name": result.modelName || "N/A",
//            "Mo Number": result.moNumber || "N/A",
//            "WIP Group": result.wipGroup || "N/A",
//            "Test Group": result.testGroup || "N/A",
//            "Reason Code": result.reasonCode || "N/A",
//            "Data1": result.data1 || "N/A",
//            "Test Code": result.testCode || "N/A",
//            "Shelf Code": result.shelfCode || "N/A",
//            "Column Number": result.columnNumber || "N/A",
//            "Level Number": result.levelNumber || "N/A",
//            "Tray Number": result.trayNumber || "N/A",
//            "Position In Tray": result.positionInTray || "N/A",
//            "Entry Date": result.entryDate || "N/A",
//            "Entry Person": result.entryPerson || "N/A",
//            "Borrow Status": result.borrowStatus || "N/A",
//            "Borrow Date": result.borrowDate || "N/A",
//            "Borrow Person": result.borrowPerson || "N/A",
//        }));

//        // Tạo workbook và worksheet
//        const workbook = XLSX.utils.book_new();
//        const worksheet = XLSX.utils.json_to_sheet(worksheetData);
//        XLSX.utils.book_append_sheet(workbook, worksheet, "Search Results");

//        // Xuất file Excel
//        XLSX.writeFile(workbook, `SearchResults-${new Date().toISOString()}.xlsx`);
//    } catch (error) {
//        console.error("Lỗi khi xuất Excel:", error.message);
//        alert("Không thể xuất dữ liệu!");
//    } finally {
//        exportButton.disabled = false;
//        exportButton.textContent = "Xuất Excel";
//    }
//});





//=========================Search Advance==================================

const baseUrl = "http://localhost:5125/api/Search/AdvancedSearch"; // URL API
let currentPage = 1;
let totalPages = 1;
let searchResults = []; // Biến toàn cục để lưu kết quả
document.addEventListener("DOMContentLoaded", () => {
    // Gắn sự kiện cho nút tìm kiếm và phân trang
    const searchBtn = document.getElementById("advanced-search-btn");
    const prevBtn = document.getElementById("prev-page-btn");
    const nextBtn = document.getElementById("next-page-btn");

    if (searchBtn) {
        searchBtn.addEventListener("click", (event) => {
            event.preventDefault();
            performAdvancedSearch();
        });
    }

    if (prevBtn) {
        prevBtn.addEventListener("click", (event) => {
            event.preventDefault();
            changePage(-1);
        });
    }

    if (nextBtn) {
        nextBtn.addEventListener("click", (event) => {
            event.preventDefault();
            changePage(1);
        });
    }
});


async function performAdvancedSearch(page = 1) {
    try {
        console.log("Đang gửi request: Trang =", page);

        const requestData = {
            BorrowStatus: document.getElementById("borrowStatus").value,
            WIPGroup: Array.from(document.getElementById("wipGroup-select").selectedOptions).map(opt => opt.value),
            TestCode: document.getElementById("testCode").value,
            Data1: document.getElementById("data1").value,
            Sfg: Array.from(document.getElementById("modelName-select").selectedOptions).map(opt => opt.value),
        };

        const response = await fetch(`${baseUrl}?page=${page}&pageSize=50`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(requestData),
        });

        if (!response.ok) throw new Error("Lỗi API!");

        const data = await response.json();
        console.log("Kết quả nhận được:", data);
        if (data.success && data.results && data.results.length > 0) {
            searchResults = data.results; // Lưu kết quả tìm kiếm vào biến toàn cục
            renderResults(data.results);
            currentPage = data.currentPage;
            totalPages = data.totalPages;
            updatePagination();
        } else {
            searchResults = []; // Nếu không tìm thấy kết quả, làm rỗng danh sách
            console.warn("API không tìm thấy kết quả:", data.message || "Không tìm thấy sản phẩm.");
            renderResults([]);
        }
    } catch (error) {
        console.error("Lỗi:", error.message);
        alert("Không thể thực hiện tìm kiếm!");
    }
}

function renderResults(results) {
    const resultsBody = document.getElementById("results-body");
    resultsBody.innerHTML = "";

    if (!results || results.length === 0) {
        resultsBody.innerHTML = "<tr><td colspan='19'>Không tìm thấy kết quả!</td></tr>";
        return;
    }

    results.forEach(result => {
        const row = document.createElement("tr");
        row.innerHTML = `
            <td>${result.serialNumber || "N/A"}</td>
            <td>${result.productLine || "N/A"}</td>
            <td>${result.modelName || "N/A"}</td>
            <td>${result.moNumber || "N/A"}</td>
            <td>${result.wipGroup || "N/A"}</td>
            <td>${result.testGroup || "N/A"}</td>
            <td>${result.reasonCode || "N/A"}</td>
            <td>${result.data1 || "N/A"}</td>
            <td>${result.testCode || "N/A"}</td>
            <td>${result.shelfCode || "N/A"}</td>
            <td>${result.columnNumber || "N/A"}</td>
            <td>${result.levelNumber || "N/A"}</td>
            <td>${result.trayNumber || "N/A"}</td>
            <td>${result.positionInTray || "N/A"}</td>
            <td>${result.entryDate || "N/A"}</td>
            <td>${result.entryPerson || "N/A"}</td>
            <td>${result.borrowStatus || "N/A"}</td>
            <td>${result.borrowDate || "N/A"}</td>
            <td>${result.borrowPerson || "N/A"}</td>
        `;
        resultsBody.appendChild(row);
    });
}

async function fetchAllPagesParallel(requestData, totalPages) {
    try {
        // Tạo danh sách các promises để gọi API song song
        const pagePromises = Array.from({ length: totalPages }, (_, i) =>
            fetch(`${baseUrl}?page=${i + 1}&pageSize=50`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(requestData),
            }).then(response => {
                if (!response.ok) throw new Error(`Lỗi API trang ${i + 1}`);
                return response.json();
            })
        );

        // Chờ tất cả các promises hoàn thành
        const pageResults = await Promise.all(pagePromises);

        // Kết hợp dữ liệu từ tất cả các trang
        return pageResults.flatMap(data => (data.success && data.results ? data.results : []));
    } catch (error) {
        console.error("Lỗi khi tải dữ liệu song song:", error.message);
        alert("Không thể tải toàn bộ dữ liệu!");
        return [];
    }
}

function updatePagination() {
    document.getElementById("pagination-info").textContent = `Trang ${currentPage}/${totalPages}`;
    document.getElementById("prev-page-btn").style.display = currentPage > 1 ? "inline-block" : "none";
    document.getElementById("next-page-btn").style.display = currentPage < totalPages ? "inline-block" : "none";
}

function changePage(direction) {
    const newPage = currentPage + direction;
    if (newPage >= 1 && newPage <= totalPages) {
        performAdvancedSearch(newPage);
    }
}



//======EXPORT EXCEL===========

document.getElementById("export-excel-btn").addEventListener("click", async () => {
    const requestData = {
        BorrowStatus: document.getElementById("borrowStatus").value,
        WIPGroup: Array.from(document.getElementById("wipGroup-select").selectedOptions).map(opt => opt.value),
        TestCode: document.getElementById("testCode").value,
        Data1: document.getElementById("data1").value,
        Sfg: Array.from(document.getElementById("modelName-select").selectedOptions).map(opt => opt.value),
    };

    const exportButton = document.getElementById("export-excel-btn");
    exportButton.disabled = true;
    exportButton.textContent = "Đang xuất...";

    try {
        // Gọi API lần đầu để lấy tổng số trang
        const firstPageResponse = await fetch(`${baseUrl}?page=1&pageSize=50`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(requestData),
        });

        if (!firstPageResponse.ok) throw new Error("Lỗi khi tải trang đầu tiên!");
        const firstPageData = await firstPageResponse.json();
        const totalPages = firstPageData.totalPages;

        // Tải dữ liệu song song từ tất cả các trang
        const allResults = await fetchAllPagesParallel(requestData, totalPages);

        if (allResults.length === 0) {
            alert("Không có dữ liệu để xuất!");
            return;
        }

        // Xuất Excel
        exportToExcel(allResults);
    } catch (error) {
        console.error("Lỗi khi xuất Excel:", error.message);
        alert("Không thể xuất dữ liệu!");
    } finally {
        exportButton.disabled = false;
        exportButton.textContent = "Xuất Excel";
    }
});

function exportToExcel(data) {
    const worksheetData = data.map(result => ({
        "Serial Number": result.serialNumber || "",
        "Product Line": result.productLine || "",
        "Model Name": result.modelName || "",
        "Mo Number": result.moNumber || "",
        "WIP Group": result.wipGroup || "",
        "Test Group": result.testGroup || "",
        "Reason Code": result.reasonCode || "",
        "Data1": result.data1 || "",
        "Test Code": result.testCode || "",
        "Shelf Code": result.shelfCode || "",
        "Column Number": result.columnNumber || "",
        "Level Number": result.levelNumber || "",
        "Tray Number": result.trayNumber || "",
        "Position In Tray": result.positionInTray || "",
        "Entry Date": result.entryDate || "",
        "Entry Person": result.entryPerson || "",
        "Borrow Status": result.borrowStatus || "",
        "Borrow Date": result.borrowDate || "",
        "Borrow Person": result.borrowPerson || "",
    }));

    const workbook = XLSX.utils.book_new();
    const worksheet = XLSX.utils.json_to_sheet(worksheetData);
    XLSX.utils.book_append_sheet(workbook, worksheet, "Search Results");

    XLSX.writeFile(workbook, `SearchResults-${new Date().toISOString()}.xlsx`);
}
//document.getElementById("export-excel-btn").addEventListener("click", async () => {
//    try {
//        const requestData = {
//            BorrowStatus: document.getElementById("borrowStatus").value,
//            WIPGroup: Array.from(document.getElementById("wipGroup-select").selectedOptions).map(opt => opt.value),
//            TestCode: document.getElementById("testCode").value,
//            Data1: document.getElementById("data1").value,
//            Sfg: Array.from(document.getElementById("modelName-select").selectedOptions).map(opt => opt.value),
//        };

//        const response = await fetch("http://localhost:5125/api/Search/ExportSearchResultsToExcel", {
//            method: "POST",
//            headers: {
//                "Content-Type": "application/json",
//            },
//            body: JSON.stringify(requestData),
//        });

//        if (!response.ok) {
//            throw new Error("Không thể xuất Excel! Vui lòng thử lại.");
//        }

//        const blob = await response.blob();
//        const url = window.URL.createObjectURL(blob);
//        const a = document.createElement("a");
//        a.style.display = "none";
//        a.href = url;
//        a.download = "SearchResults.xlsx"; // Tên file tải về
//        document.body.appendChild(a);
//        a.click();
//        window.URL.revokeObjectURL(url);
//    } catch (error) {
//        console.error("Lỗi khi xuất Excel:", error);
//        alert("Không thể xuất Excel! Vui lòng kiểm tra dữ liệu và thử lại.");
//    }
//});
document.getElementById("export-all-btn").addEventListener("click", async () => {
    try {
        const exportButton = document.getElementById("export-all-btn");
        exportButton.disabled = true; // Vô hiệu hóa nút trong khi xử lý
        exportButton.textContent = "Đang xuất...";

        const response = await fetch('http://localhost:5125/api/Search/ExportAllDataToExcel', {
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

        alert("Xuất dữ liệu thành công!");
    } catch (error) {
        console.error("Lỗi khi xuất Excel:", error.message);
        alert("Lỗi: Không thể xuất dữ liệu!");
    } finally {
        exportButton.disabled = false; // Kích hoạt lại nút
        exportButton.textContent = "Xuất All";
    }
});
