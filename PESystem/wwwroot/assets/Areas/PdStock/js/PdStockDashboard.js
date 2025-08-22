document.getElementById('searchBtn').addEventListener('click', function () {
    // Lấy dữ liệu từ các thẻ input
    const startDate = document.getElementById('startDate').value;
    const endDate = document.getElementById('endDate').value;
    const searchOption = document.getElementById('search-options').value;
    const resultsSection = document.getElementById('PDStock-search-results-section');
    const qtyElement = document.getElementById('dashboard-qty');

    // Kiểm tra xem các giá trị có hợp lệ không
    if (!startDate || !endDate || searchOption === "TYPE") {
        alert("Vui lòng nhập đầy đủ thông tin thời gian và chọn loại tìm kiếm.");
        return;
    }

    // Xác định URL API
    let url;
    if (searchOption === "add-stock") {
        url = "http://10.220.130.119:9090/DdRepositorys/GetProductsByDateRange";
    } else if (searchOption === "out-stock") {
        url = "http://10.220.130.119:9090/DdRepositorys/GetExportedProductsByDateRange";
    } else {
        alert("Tùy chọn tìm kiếm không hợp lệ.");
        return;
    }

    // Tạo dữ liệu JSON gửi tới API
    const requestData = {
        startDate: startDate,
        endDate: endDate
    };

    // Gửi request tới API
    fetch(url, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(requestData)
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
            // Hiển thị kết quả dưới dạng bảng
            displayResultsAsTable(data.data, resultsSection, qtyElement);
        })
        .catch(error => {
            console.error('Error fetching data:', error);
            resultsSection.innerHTML = `<p class="data-alert">Error fetching data: ${error.message}</p>`;
            qtyElement.textContent = "0";
        });
});

/**
 * Hiển thị kết quả dưới dạng bảng và đếm số lượng
 */
function displayResultsAsTable(data, resultsSection, qtyElement) {
    resultsSection.innerHTML = ''; // Xóa nội dung cũ

    if (!data || data.length === 0) {
        resultsSection.innerHTML = '<p class="data-alert">Không tìm thấy dữ liệu phù hợp.</p>';
        qtyElement.textContent = "0"; // Đặt số lượng về 0 nếu không có kết quả
        return;
    }

    // Cập nhật số lượng kết quả
    qtyElement.textContent = data.length;

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
                ${data[0].outDate ? '<th>Out Date</th><th>Out Op</th>' : ''}
            </tr>
        </thead>
        <tbody></tbody>
    `;

    // Thêm các hàng dữ liệu vào bảng
    const tbody = table.querySelector('tbody');
    data.forEach(item => {
        const row = document.createElement('tr');
        row.innerHTML = `
            <td>${item.serialNumber || '-'}</td>
            <td>${item.modelName || '-'}</td>
            <td>${item.cartonNo || '-'}</td>
            <td>${item.locationStock || '-'}</td>
            <td>${item.entryDate || '-'}</td>
            <td>${item.entryOp || '-'}</td>
            ${item.outDate ? `<td>${item.outDate || '-'}</td><td>${item.outOp || '-'}</td>` : ''}
        `;
        tbody.appendChild(row);
    });

    // Thêm bảng vào resultsSection
    resultsSection.appendChild(table);
}


// xử lý nút xuất Excel

document.getElementById('PdStock-ExportExcel-btn').addEventListener('click', function () {
    // Lấy dữ liệu từ bảng PdStock-results-table
    const table = document.querySelector('.PdStock-results-table');
    if (!table) {
        alert("Không có dữ liệu để xuất!");
        return;
    }

    // Tạo dữ liệu Excel từ bảng
    let tableData = '';
    const rows = table.querySelectorAll('tr');
    rows.forEach(row => {
        const cols = row.querySelectorAll('td, th');
        const rowData = Array.from(cols)
            .map(col => col.textContent.replace(/,/g, '')) // Loại bỏ dấu phẩy
            .join(','); // Ngăn cách bằng dấu phẩy
        tableData += rowData + '\n';
    });

    // Tạo Blob chứa dữ liệu CSV
    const blob = new Blob([tableData], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);

    // Tạo thẻ <a> để tải file
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', 'PdStockData.csv'); // Đặt tên file tải về
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);

    // Thông báo thành công
    alert("Xuất dữ liệu ra file Excel thành công!");
});

