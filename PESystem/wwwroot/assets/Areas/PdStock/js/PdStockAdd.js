// ================ Hàm xử lý dữ liệu input carton No hoặc Serial Number ======================

document.getElementById('PD-repo-add-input').addEventListener('keypress', function (event) {
    if (event.key === 'Enter') {
        const textarea = event.target; // Lấy textarea
        const resultsSection = document.getElementById('PDStock-search-results-section');
        const qtyElement = document.getElementById('Data-search-qty');
        const searchOption = document.getElementById('search-options').value; // Lấy giá trị từ dropdown

        // Lấy dữ liệu nhập vào và xử lý thành danh sách
        const inputValue = textarea.value.trim();
        if (!inputValue) {
            resultsSection.innerHTML = '<p class="data-alert">No input provided.</p>';
            qtyElement.textContent = 'Tổng số kết quả: 0';
            return;
        }

        // Tách danh sách dựa trên dòng
        const inputList = inputValue
            .split('\n') // Tách theo dòng
            .map(item => item.trim()) // Loại bỏ khoảng trắng
            .filter(item => item !== ""); // Loại bỏ dòng trống

        if (inputList.length === 0) {
            resultsSection.innerHTML = '<p class="data-alert">No valid data found.</p>';
            qtyElement.textContent = 'Tổng số kết quả: 0';
            return;
        }

        // Ghi log dữ liệu gửi
        console.log("Sending data to API:", inputList);

        // Xác định URL API dựa trên giá trị của dropdown
        let apiUrl = '';
        if (searchOption === 'add-with-SN') {
            apiUrl = 'http://10.220.130.119:9090/DdRepositorys/GetR107bySN'; // API cho Serial Number
        } else if (searchOption === 'add-with-cartonNO') {
            apiUrl = 'http://10.220.130.119:9090/DdRepositorys/GetR107byCartonNos'; // API cho Carton No
        } else {
            resultsSection.innerHTML = '<p class="data-alert">Please select a valid search option.</p>';
            qtyElement.textContent = 'Tổng số kết quả: 0';
            return;
        }

        // Gửi request tới API
        fetch(apiUrl, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(inputList) // Gửi danh sách dưới dạng mảng chuỗi
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
 * Hàm hiển thị dữ liệu dưới dạng bảng và đếm kết quả
 */
function displayResultsAsTable(data, resultsSection, qtyElement) {
    resultsSection.innerHTML = ''; // Xóa nội dung cũ

    if (!data || !data.data || data.data.length === 0) {
        resultsSection.innerHTML = '<p class="data-alert">No results found.</p>';
        qtyElement.textContent = 'Tổng số kết quả: 0';
        return;
    }

    const results = data.data;

    // Hiển thị số lượng kết quả
    qtyElement.textContent = `Tổng số kết quả: ${results.length}`;

    // Tạo bảng HTML
    const table = document.createElement('table');
    table.className = 'PdStock-results-table';
    table.innerHTML = `
        <thead>
            <tr>
                <th>CARTON_NO</th>
                <th>SERIAL_NUMBER</th>
                <th>MODEL_NAME</th>
            </tr>
        </thead>
        <tbody></tbody>
    `;

    const tbody = table.querySelector('tbody');
    results.forEach(item => {
        const row = document.createElement('tr');
        row.innerHTML = `
            <td>${item.cartoN_NO || '-'}</td>
            <td>${item.seriaL_NUMBER || '-'}</td>
            <td>${item.modeL_NAME || '-'}</td>
        `;
        tbody.appendChild(row);
    });

    resultsSection.appendChild(table);
}

//=========== Hàm xử lý nút nhập kho =================

document.getElementById('PdStock-add-btn').addEventListener('click', function () {
    // Lấy tất cả các dòng trong bảng (nếu bảng đã được tạo và có dữ liệu)
    const tableRows = document.querySelectorAll('.PdStock-results-table tbody tr');
    const locationStock = document.getElementById('PdStock-location').value.trim(); // Lấy giá trị từ ô nhập vị trí
    const entryOp = document.querySelector('.d-none.d-md-block.ps-2')?.textContent || 'CurrentUser'; // Lấy tài khoản, mặc định là "CurrentUser" nếu không tìm thấy

    // Kiểm tra dữ liệu trong bảng
    if (!tableRows || tableRows.length === 0) {
        alert("Không có dữ liệu trong bảng để nhập kho!");
        return;
    }

    // Kiểm tra giá trị nhập của vị trí
    if (!locationStock) {
        alert("Vui lòng nhập vị trí (locationStock)!");
        return;
    }

    // Thu thập dữ liệu từ bảng
    const dataToPost = Array.from(tableRows).map(row => {
        const cartonNo = row.children[0]?.textContent.trim();
        const serialNumber = row.children[1]?.textContent.trim();
        const modelName = row.children[2]?.textContent.trim();

        return {
            serialNumber: serialNumber || "",
            modelName: modelName || "",
            cartonNo: cartonNo || "",
            locationStock: locationStock,
            entryOp: entryOp // Tài khoản hiện tại
        };
    });

    console.log("Data to send:", dataToPost); // Log dữ liệu gửi để kiểm tra

    // Gửi dữ liệu tới API
    fetch('http://10.220.130.119:9090/DdRepositorys/PostToTable', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(dataToPost) // Dữ liệu gửi
    })
        .then(response => {
            if (!response.ok) {
                return response.text().then(err => {
                    throw new Error(`HTTP error! status: ${response.status}, message: ${err}`);
                });
            }
            return response.json();
        })
        .then(result => {
            console.log('Success:', result);
            alert(JSON.stringify(result));
            // Reset dữ liệu của các thẻ
            document.getElementById('PdStock-location').value = ''; // Reset ô nhập vị trí
            document.getElementById('PDStock-search-results-section').innerHTML = ''; // Xóa nội dung bảng
            document.getElementById('Data-search-qty').textContent = 'Tổng số kết quả: 0'; // Đặt lại số lượng kết quả
            document.getElementById('PD-repo-add-input').value = ''; // Reset nội dung ô nhập danh sách
        })
        .catch(error => {
            console.error('Error:', error);
            alert("Đã xảy ra lỗi khi nhập kho.");
        });
});