
//============= xử lý search =========================

document.getElementById('PdStock-search-btn').addEventListener('click', function () {
    const searchOption = document.getElementById('search-options');
    const searchInput = document.getElementById('PD-repo-seach-input');
    const resultsSection = document.getElementById('PDStock-search-results-section');
    const qtyElement = document.getElementById('Data-search-qty'); // Thẻ hiển thị số lượng kết quả

    // Kiểm tra xem tất cả các phần tử có tồn tại trong DOM hay không
    if (!searchOption || !searchInput || !resultsSection || !qtyElement) {
        console.error("Missing required elements in DOM.");
        return;
    }

    // Lấy giá trị từ thẻ select và textarea
    const searchOptionValue = searchOption.value;
    const searchInputValue = searchInput.value.trim();

    // Kiểm tra xem người dùng đã nhập dữ liệu hay chưa
    if (!searchInputValue) {
        resultsSection.innerHTML = '<p class="data-alert">Please enter a valid input.</p>';
        qtyElement.textContent = '0'; // Đặt số lượng kết quả về 0 nếu không có dữ liệu
        return;
    }

    resultsSection.innerHTML = '<p class="data-alert">Loading...</p>'; // Hiển thị trạng thái tải dữ liệu
    qtyElement.textContent = ''; // Reset số lượng trước khi cập nhật

    // Tách dữ liệu đầu vào thành danh sách
    const dataSearch = searchInputValue
        .split('\n') // Tách theo dòng
        .map(item => item.trim()) // Loại bỏ khoảng trắng mỗi dòng
        .filter(item => item !== ""); // Loại bỏ các dòng trống

    if (dataSearch.length === 0) {
        resultsSection.innerHTML = '<p class="data-alert">Please enter valid data.</p>';
        qtyElement.textContent = '0';
        return;
    }

    // Chọn API phù hợp dựa trên searchOptionValue
    let url;
    switch (searchOptionValue) {
        case 'SEARCH_S/N':
            url = 'http://10.220.130.119:9090/DdRepositorys/GetBySerialNumber';
            break;
        case 'SEARCH_MODEL_NAME':
            url = 'http://10.220.130.119:9090/DdRepositorys/GetByModelName';
            break;
        case 'SEARCH_CAR':
            url = 'http://10.220.130.119:9090/DdRepositorys/GetByCartonNo';
            break;
        case 'SEARCH_EXPORT':
            url = 'http://10.220.130.119:9090/DdRepositorys/GetHistoryBySerialNumber';
            break;
        case 'SEARCH_All':
            url = 'http://10.220.130.119:9090/DdRepositorys/GetAll';
            break;
        default:
            resultsSection.innerHTML = '<p class="data-alert">Please select a valid search option.</p>';
            qtyElement.textContent = '0';
            return;
    }

    // Gửi yêu cầu POST (hoặc GET nếu SEARCH_All)
    const fetchOptions = {
        method: searchOptionValue === 'SEARCH_All' ? 'GET' : 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: searchOptionValue === 'SEARCH_All' ? null : JSON.stringify(dataSearch),
    };

    fetch(url, fetchOptions)
        .then(response => {
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            return response.json();
        })
        .then(data => {
            displayResultsAsTable(data, resultsSection, qtyElement); // Hiển thị kết quả
        })
        .catch(error => {
            console.error('Error fetching data:', error);
            resultsSection.innerHTML = '<p class="data-alert">Error fetching data. Please try again later.</p>';
            qtyElement.textContent = '0'; // Đặt số lượng là 0 nếu có lỗi
        });
});

/**
 * Hàm hiển thị kết quả dưới dạng bảng và cập nhật số lượng
 */
function displayResultsAsTable(data, resultsSection, qtyElement) {
    resultsSection.innerHTML = ''; // Xóa trạng thái tải

    // Kiểm tra nếu không có kết quả trả về
    if (!data || !data.data || (Array.isArray(data.data) && data.data.length === 0)) {
        resultsSection.innerHTML = '<p class="data-alert">No results found.</p>';
        qtyElement.textContent = '0'; // Đặt số lượng là 0 nếu không có kết quả
        return;
    }

    const results = Array.isArray(data.data) ? data.data : [data.data]; // Đảm bảo dữ liệu là mảng

    // Cập nhật số lượng kết quả
    qtyElement.textContent = `Tổng số kết quả tìm được: ${results.length}`;

    // Xác định xem có cột "OutDate" và "OutOp" hay không
    const searchOption = document.getElementById('search-options').value;
    const includeOutFields = searchOption === 'SEARCH_EXPORT';

    // Tạo bảng HTML để hiển thị kết quả
    const table = document.createElement('table');
    table.className = 'PdStock-results-table';

    // Tạo tiêu đề bảng
    table.innerHTML = `
        <thead>
            <tr>
                <th>Serial Number</th>
                <th>Model Name</th>
                <th>Carton No</th>
                <th>Location Stock</th>
                <th>Entry Date</th>
                <th>Entry Op</th>
                ${includeOutFields ? '<th>Out Date</th><th>Out Op</th>' : ''}
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
            ${includeOutFields ? `<td>${item.outDate || '-'}</td><td>${item.outOp || '-'}</td>` : ''}
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










