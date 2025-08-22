document.addEventListener("DOMContentLoaded", function () {

    const table = $('#sn-table').DataTable({
        dom: 't',
        paging: false,
        info: false,
        ordering: false // Tắt sorting
    });

    let existingSNs = new Set();

    // Gắn sự kiện keypress cho ô nhập SN
    $('#serialNumber').on('keypress', function (e) {
        if (e.key === 'Enter') { // Khi nhấn Enter
            e.preventDefault(); // Ngăn reload trang
            $('#sn-form').trigger('submit'); // Gọi submit
        }
    });

    // Gọi API lấy danh sách ProductLine khi trang được tải
    fetch('http://10.220.130.119:9090/api/FixGuide/GetProductLines', {
        method: 'GET',
        headers: {
            'Content-Type': 'application/json'
        }
    })
        .then(response => {
            if (!response.ok) {
                throw new Error('Lỗi khi gọi API lấy danh sách ProductLine');
            }
            return response.json();
        })
        .then(data => {
            const dropdown = $('#productLineDropdown');
            data.productLines.forEach(productLine => {
                dropdown.append(`<option value="${productLine}">${productLine}</option>`);
            });
        })
        .catch(error => {
            console.error('Lỗi khi lấy ProductLine:', error);
        });

    $('#sn-form').on('submit', function (e) {
        e.preventDefault(); // Ngăn chặn reload trang

        const serialNumber = $('#serialNumber').val().trim();

        if (!serialNumber) {
            showWarning("Vui lòng nhập SN!")
            return;
        }

        if (existingSNs.has(serialNumber)) {
            showWarning(`"${serialNumber}" đã tồn tại!`);
            return;
        }

        fetch(`http://10.220.130.119:9090/api/Product/SearchCheckIn?serialNumber=${serialNumber}`, {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json'
            }
        })
            .then(response => {
                if (!response.ok) {
                    throw new Error('Lỗi khi gọi API SearchCheckIn');
                }
                return response.json();
            })
            .then(data => {
                if (!data.success) {
                    showError(`No data "${serialNumber}"!`);
                    return;
                }

                const modelName = data.modelName || '';
                const productLine = data.productLine || '';
                const testGroup = data.testGroup || '';
                const errorCode = data.testCode || '';
                const data1 = data.data1 || '';

                if (existingSNs.has(serialNumber)) {
                    showWarning(`"${serialNumber}" đã tồn tại!`);
                    return;
                }

                existingSNs.add(serialNumber);

                // Gọi API GetCheckIn
                fetch('http://10.220.130.119:9090/api/FixGuide/GetCheckIn', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({
                        productLine: productLine,
                        errorCode: errorCode
                    })
                })
                    .then(res => res.json())
                    .then(checkInData => {
                        // Kiểm tra phản hồi API và xử lý Guides
                        const checkInArray = (checkInData.success && Array.isArray(checkInData.guides))
                            ? checkInData.guides
                            : ["Không tìm thấy Guide"];

                        // Thêm hàng vào DataTable
                        table.row.add([
                            serialNumber,
                            productLine,
                            modelName,
                            errorCode,
                            testGroup,
                            `<span title="${data1}">${data1}</span>`,
                            `<span title="${checkInArray.join(', ')}">${checkInArray.join(', ')}</span>`
                        ]).draw(false);

                        $('#serialNumber').val(''); // Reset input
                    })
                    .catch(err => {
                        console.error('Lỗi khi gọi API GetCheckIn:', err);

                        // Thêm dòng mặc định nếu lỗi
                        table.row.add([
                            serialNumber,
                            productLine,
                            modelName,
                            errorCode,
                            testGroup,
                            `<span title="${data1}">${data1}</span>`,
                            `<span title="Không tìm thấy Guide">Không tìm thấy Guide</span>`
                        ]).draw(false);

                        $('#serialNumber').val('');
                    });
            })
            .catch(error => {
                console.error('Lỗi khi gọi API SearchCheckIn:', error);
            });
    });

    // Xử lý sự kiện khi nhấn nút Add Guide
    $('#addGuideButton').on('click', function () {
        const fileInput = document.getElementById('guideFile');
        const file = fileInput.files[0];
        const productLine = $('#productLineDropdown').val();

        if (!file || !productLine) {
            showWarning("Vui lòng chọn File và Model!")
            return;
        }

        const formData = new FormData();
        formData.append('file', file);
        formData.append('productLine', productLine);

        fetch('http://10.220.130.119:9090/api/FixGuide/UploadGuideCheckIn', {
            method: 'POST',
            body: formData
        })
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    showSuccess("Success!")

                    // Reset các trường về mặc định
                    $('#guideFile').val(''); // Xóa file đã chọn
                    $('#productLineDropdown').val(''); // Reset dropdown về giá trị mặc định
                } else {
                    showError("Error!")
                }
            })
            .catch(error => {
                console.error('Lỗi tải hướng dẫn:', error);
                showError("Lỗi khi gọi API!")
            });
    });
});
