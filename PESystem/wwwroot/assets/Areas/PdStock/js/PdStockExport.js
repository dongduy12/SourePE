// ================ Hàm xử lý dữ liệu input carton No hoặc Serial Number ======================

document.getElementById('PD-repo-exprot-input').addEventListener('keypress', function (event) {
    if (event.key === 'Enter') {
        const textarea = event.target;
        const resultsSection = document.getElementById('PDStock-search-results-section');
        const qtyElement = document.getElementById('Data-search-qty');
        const searchOption = document.getElementById('search-options');

        // Lấy giá trị từ textarea và xử lý
        const searchOptionValue = searchOption.value;
        const inputValue = textarea.value.trim();
        if (!inputValue) {
            resultsSection.innerHTML = '<p class="data-alert">No input provided.</p>';
            qtyElement.textContent = 'Tổng số kết quả: 0';
            return;
        }

        // Xử lý dữ liệu nhập
        const dataList = inputValue
            .split('\n')
            .map(item => item.trim())
            .filter(item => item !== "");

        if (dataList.length === 0) {
            resultsSection.innerHTML = '<p class="data-alert">No valid data found.</p>';
            qtyElement.textContent = 'Tổng số kết quả: 0';
            return;
        }

        // Chọn API dựa trên tùy chọn tìm kiếm
        let url;
        switch (searchOptionValue) {
            case 'exportByCartonNo':
                url = 'http://10.220.130.119:9090/DdRepositorys/GetByCartonNo';
                break;
            case 'exportBySn':
                url = 'http://10.220.130.119:9090/DdRepositorys/GetBySerialNumber';
                break;
            default:
                resultsSection.innerHTML = '<p class="data-alert">Please select a valid search option.</p>';
                qtyElement.textContent = 'Tổng số kết quả: 0';
                return;
        }

        // Gửi request tới API
        fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(dataList)
        })
            .then(response => {
                if (!response.ok) {
                    return response.text().then(err => {
                        throw new Error(`HTTP error! status: ${response.status}, message: ${err}`);
                    });
                }
                return response.json();
            })
            .then(data => {
                console.log('Response data:', data);
                displayResultsAsTable(data, resultsSection, qtyElement);
            })
            .catch(error => {
                console.error('Error fetching data:', error);
                resultsSection.innerHTML = `<p class="data-alert">Error fetching data: ${error.message}</p>`;
                qtyElement.textContent = 'Tổng số kết quả: 0';
            });
    }
});

/**
 * Hàm hiển thị kết quả dưới dạng bảng và cập nhật số lượng
 */
function displayResultsAsTable(data, resultsSection, qtyElement) {
    resultsSection.innerHTML = ''; // Xóa trạng thái tải

    // Kiểm tra nếu không có kết quả trả về
    if (!data || !data.data || (Array.isArray(data.data) && data.data.length === 0)) {
        resultsSection.innerHTML = '<p class="data-alert">No results found.</p>';
        qtyElement.textContent = 'Tổng số kết quả: 0'; // Đặt số lượng là 0 nếu không có kết quả
        return;
    }

    const results = Array.isArray(data.data) ? data.data : [data.data]; // Đảm bảo dữ liệu là mảng

    // Cập nhật số lượng kết quả
    qtyElement.textContent = `Tổng số kết quả tìm được: ${results.length}`;

    // Tạo bảng HTML để hiển thị kết quả
    const table = document.createElement('table');
    table.className = 'PdStock-results-table';
    table.innerHTML = `
        <thead>
            <tr>
                <th>Serial Number</th>
                <th>Model Name</th>
                <th>Carton No</th>
                <th>Location Stock</th>
                <th>Entry Date</th>
                <th>Entry Op</th>
            </tr>
        </thead>
        <tbody>
        </tbody>
    `;

    // Thêm các hàng dữ liệu vào bảng
    const tbody = table.querySelector('tbody');
    results.forEach(item => {
        const row = document.createElement('tr');
        row.innerHTML = `
            <td>${item.serialNumber || '-'}</td>
            <td>${item.modelName || '-'}</td>
            <td>${item.cartonNo || '-'}</td>
            <td>${item.locationStock || '-'}</td>
            <td>${item.entryDate || '-'}</td>
            <td>${item.entryOp || '-'}</td>
        `;
        tbody.appendChild(row);
    });

    // Thêm bảng vào resultsSection
    resultsSection.appendChild(table);
}

// ================ xử lý nút xuất kho =========================

document.getElementById('PdStock-exprort-btn').addEventListener('click', function () {
    // Lấy tất cả các dòng trong bảng PdStock-results-table
    const tableRows = document.querySelectorAll('.PdStock-results-table tbody tr');
    if (!tableRows || tableRows.length === 0) {
        alert("Không có sản phẩm nào trong bảng để xóa!");
        return;
    }

    // Lấy tài khoản người dùng hiện tại
    const currentUser = document.querySelector('.d-none.d-md-block.ps-2')?.textContent || 'CurrentUser'; // Thay thế bằng logic thực tế để lấy tài khoản đăng nhập hiện tại

    // Thu thập danh sách SN từ bảng
    const deleteData = Array.from(tableRows).map(row => {
        const serialNumber = row.children[0]?.textContent.trim(); // Cột 1: Serial Number
        return {
            serialNumber: serialNumber || "",
            outOp: currentUser // Gắn tài khoản hiện tại
        };
    });

    console.log("Data to send:", deleteData); // Kiểm tra dữ liệu JSON trước khi gửi

    // Gửi dữ liệu tới API
    fetch('http://10.220.130.119:9090/DdRepositorys/DeleteBySerialNumbers', {
        method: 'DELETE',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(deleteData) // Dữ liệu xóa
    })
        .then(response => {
            if (!response.ok) {
                return response.text().then(err => {
                    throw new Error(`HTTP error! status: ${response.status}, message: ${err}`);
                });
            }
            return response.json();
        })
        .then(data => {
            console.log('Response:', data);
            alert(data.message || "Xóa sản phẩm thành công!");

            // Reset bảng và các thông tin liên quan sau khi xóa
            document.querySelector('.PdStock-results-table tbody').innerHTML = ''; // Xóa dữ liệu bảng
            document.getElementById('Data-search-qty').textContent = 'Tổng số kết quả: 0'; // Reset số lượng kết quả
        })
        .catch(error => {
            console.error('Error:', error);
            alert(`Đã xảy ra lỗi: ${error.message}`);
        });
});