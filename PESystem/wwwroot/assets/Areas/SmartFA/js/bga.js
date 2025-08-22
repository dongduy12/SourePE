$(document).ready(function () {
    // Khởi tạo DataTable
    const table = $('#keyPartTable').DataTable({
        destroy: true,
        paging: true,
        searching: true,
        ordering: true,
        language: {
            search: "Tìm kiếm:",
            lengthMenu: "Hiển thị _MENU_ dòng",
            info: "Hiển thị _START_ đến _END_ của _TOTAL_ dòng",
            paginate: {
                first: "Đầu",
                last: "Cuối",
                next: "Tiếp",
                previous: "Trước"
            }
        }
    });

    // Hàm gửi form
    window.submitSerialNumberForm = function () {
        const serialNumbers = $('#sn-input').val()
            .split('\n') // Tách các dòng
            .map(sn => sn.trim()) // Loại bỏ khoảng trắng
            .filter(sn => sn); // Loại bỏ dòng rỗng

        if (serialNumbers.length === 0) {
            showWarning('Vui lòng nhập ít nhất một Serial Number.');
            return;
        }

        fetch('http://10.220.130.119:9090/api/Search/GetKeyPartDetails', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(serialNumbers), // Gửi danh sách SNs
        })
            .then(response => {
                if (!response.ok) {
                    throw new Error('Lỗi khi gọi API.');
                }
                return response.json();
            })
            .then(data => {
                if (!data.success) {
                    showError(`Lỗi: ${data.message}`);
                    return;
                }

                // Xóa dữ liệu cũ trong bảng
                table.clear();

                // Thêm dữ liệu mới từ API vào bảng
                data.data.forEach(item => {
                    table.row.add([
                        item.keyPartSN,
                        item.newPN,
                        item.serialNumber,
                        item.data1,
                        item.oldTestCode,
                        item.newTestCode,
                        item.modelName,
                        item.wipGroup,
                        item.status
                    ]);
                });

                // Hiển thị danh sách SN không tìm thấy (nếu có)
                if (data.notFoundSNs && data.notFoundSNs.length > 0) {
                    // Xuất Excel chỉ với danh sách không tìm thấy
                    exportNotFoundSNsToExcel(data.notFoundSNs);
                    showInfo(`SN không tìm thấy: ${data.notFoundSNs.join(', ')}`);
                }

                // Vẽ lại bảng
                table.draw();
            })
            .catch(error => {
                console.error('Lỗi:', error);
                showError('Không thể tải dữ liệu. Vui lòng thử lại sau.');
            });
    };

    // Hàm xuất dữ liệu ra Excel
    window.exportTableToExcel = function () {
        // Lấy dữ liệu từ DataTable
        const data = table.rows().data().toArray();

        // Tạo một mảng dữ liệu cho Excel
        const excelData = [
            ['Key Part SN', 'New PN', 'Serial Number', 'Model Name', 'Data1','OldTestCode', 'NewTestCode', 'WIP Group', 'Status'], // Tiêu đề cột
            ...data // Dữ liệu từ bảng
        ];

        // Chuyển đổi dữ liệu thành Workbook
        const ws = XLSX.utils.aoa_to_sheet(excelData); // Chuyển mảng thành sheet
        const wb = XLSX.utils.book_new(); // Tạo workbook
        XLSX.utils.book_append_sheet(wb, ws, 'KeyPartData'); // Thêm sheet vào workbook

        // Xuất file Excel
        XLSX.writeFile(wb, 'KeyPartData.xlsx');
    };

    // Gán sự kiện cho nút xuất Excel
    $('#export-sn-excel-btn').on('click', exportTableToExcel);

    //Xuất ra những QR không tìm thấy.
    function exportNotFoundSNsToExcel(notFoundSNs) {
        if (!notFoundSNs || notFoundSNs.length === 0) {
            showError('Không có QR Code nào không tìm thấy.');
            return;
        }

        // Chuẩn bị dữ liệu cho Excel
        const excelData = [
            ['Key Part SN'], // Tiêu đề cột
            ...notFoundSNs.map(sn => [sn]) // Chuyển đổi danh sách SNs thành mảng 2D
        ];

        // Tạo Workbook và Worksheet
        const ws = XLSX.utils.aoa_to_sheet(excelData); // Chuyển mảng thành sheet
        const wb = XLSX.utils.book_new(); // Tạo workbook
        XLSX.utils.book_append_sheet(wb, ws, 'NotFoundKeyParts'); // Thêm sheet vào workbook

        // Xuất file Excel
        XLSX.writeFile(wb, 'NotFoundKeyParts.xlsx');
    }

});

