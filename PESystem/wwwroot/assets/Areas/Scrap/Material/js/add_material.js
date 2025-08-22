document.addEventListener('DOMContentLoaded', function () {
    const temAllpartsInput = document.getElementById('tem-allparts');
    const partNumberInput = document.getElementById('part-number');
    const vendorCodeInput = document.getElementById('vendor-code');
    const vendorNameInput = document.getElementById('vendor-name');
    const dateCodeInput = document.getElementById('date-code');
    const lotCodeInput = document.getElementById('lot-code');
    const qtyInput = document.getElementById('quantity');
    const esdInput = document.getElementById('esd');
    const locationInput = document.getElementById('location');
    const remarkInput = document.getElementById('remark');
    const entryButton = document.getElementById('entry-btn');
    const currentUser = document.getElementById('entryPerson').value;
    const borrowBtn = document.getElementById('borrow-btn');
    const exportBtn = document.getElementById('export-btn');
    const exportExcelBtn = document.getElementById('export-excel-btn');
    // Kiểm tra null cho các phần tử
    const elements = {
        temAllpartsInput,
        partNumberInput,
        vendorCodeInput,
        vendorNameInput,
        dateCodeInput,
        lotCodeInput,
        qtyInput,
        esdInput,
        locationInput,
        remarkInput,
        entryButton,
        borrowBtn,
        exportBtn
    };
    for (const [key, element] of Object.entries(elements)) {
        if (!element) {
            console.warn(`Element ${key} not found. This may be due to permission restrictions. Skipping related functionality.`);
            // Không return để tiếp tục chạy các phần khác
        }
    }
    let selectedRows = []; // Lưu các hàng được chọn
    let table = null; // Biến toàn cục để lưu instance của DataTable

    // Hàm tạo nội dung cho ô với tooltip
    function createTooltipCell(data) {
        return `<span class="tooltip-trigger" data-tooltip="${data || ''}">${data || ''}</span>`;
    }


    // Xóa các trường readonly
    function clearReadonlyFields() {
        const fields = [
            partNumberInput,
            vendorCodeInput,
            vendorNameInput,
            dateCodeInput,
            lotCodeInput,
            qtyInput
        ];
        fields.forEach(field => {
            if (field) field.value = '';
        });
    }

    // Sự kiện khi nhập TEM ALLPARTS
    if (temAllpartsInput) {
        temAllpartsInput.addEventListener('change', async function () {
            const temAllparts = temAllpartsInput.value.trim();
            if (!temAllparts) {
                clearReadonlyFields();
                return;
            }

            try {
                const response = await fetch('http://10.220.130.119:9090/api/RepairStatus/info-allpart', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({
                        type: 'GET-TRSN-INFOR',
                        item: '',
                        var_1: temAllparts
                    })
                });

                if (!response.ok) {
                    throw new Error(`Lỗi khi gọi API: ${response.status} ${response.statusText}`);
                }

                const data = await response.json();
                console.log('API Response:', data); // Ghi log để kiểm tra
                // Ánh xạ dữ liệu từ phản hồi API
                partNumberInput.value = data.part_number || '';
                vendorCodeInput.value = data.vendor_code || '';
                vendorNameInput.value = data.vendor_name || '';
                dateCodeInput.value = data.date_code || '';
                lotCodeInput.value = data.lot_code || '';
                qtyInput.value = data.quantity || '';

                // Kiểm tra dữ liệu hợp lệ
                if (!data.part_number && !data.vendor_code && !data.date_code && !data.lot_code && (parseInt(data.quantity) || 0) < 0) {
                    showError("Dữ liệu không hợp lệ!");
                    clearReadonlyFields();
                }
            } catch (error) {
                console.error('Lỗi khi lấy dữ liệu:', error);
                showError("Không thể lấy dữ liệu từ Tem Allparts!");
                clearReadonlyFields();
            }
        });
        temAllpartsInput.addEventListener('keydown', function (event) {
            if (event.key === 'Enter') {
                event.preventDefault(); // Ngăn chặn hành vi mặc định (như submit form)
                esdInput.focus(); // Chuyển focus sang ô esd
            }
        });
    }

    if (esdInput) {
        esdInput.addEventListener('keydown', function (event) {
            if (event.key === 'Enter') {
                event.preventDefault(); // Ngăn chặn hành vi mặc định
                locationInput.focus(); // Chuyển focus sang ô location
            }
        });
    }

    if (entryButton) {
        // Sự kiện khi nhấn nút NHẬP KHO
        entryButton.addEventListener('click', async function () {
            const temAllparts = temAllpartsInput.value.trim();
            const partNumber = partNumberInput.value.trim();
            const vendorCode = vendorCodeInput.value.trim();
            const vendorName = vendorNameInput.value.trim();
            const dateCode = dateCodeInput.value.trim();
            const lotCode = lotCodeInput.value.trim();
            const qty = parseInt(qtyInput.value.trim()) || 0;
            const esd = esdInput.value.trim();
            const location = locationInput.value.trim();
            const remark = remarkInput.value.trim();

            // Kiểm tra dữ liệu bắt buộc (cho phép qty = 0)
            if (!temAllparts || !partNumber || !vendorCode || !vendorName || !dateCode || !lotCode || qty < 0) {
                showWarning("Thông tin không được để trống!")
                return;
            }

            if (!location) {
                showWarning("Location không được để trống!")
                return;
            }

            try {
                const response = await fetch('http://10.220.130.119:9090/api/MaterialSystem/AddMaterial', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({
                        MaAll: temAllparts,
                        MaLieu: partNumber,
                        NhaCungUng: vendorCode,
                        DateCode: dateCode,
                        LotCode: lotCode,
                        Qty: qty,
                        OP: currentUser,
                        ESD: esd,
                        Location: location,
                        Remark: remark
                    })
                });

                if (!response.ok) {
                    throw new Error(`Lỗi khi gọi API AddMaterial: ${response.status} ${response.statusText}`);
                }

                const result = await response.json();
                showSuccess("Nhập kho thành công");
                temAllpartsInput.value = '';
                clearReadonlyFields();
                esdInput.value = '';
                locationInput.value = '';
                remarkInput.value = '';

                // Tải lại bảng sau khi nhập kho thành công
                loadSumMaterialsTable();
            } catch (error) {
                console.error('Lỗi khi nhập kho:', error);
                showError("Có lỗi xảy ra!");
            }
        });
    }

    // Hàm gắn sự kiện tooltip cho các phần tử
    function attachTooltipEvents() {
        $('.tooltip-trigger').each(function () {
            const $this = $(this);
            // Kiểm tra xem sự kiện đã được gắn chưa để tránh trùng lặp
            if (!$this.data('tooltip-initialized')) {
                $this.on('mouseover', function (e) {
                    const tooltipText = $this.data('tooltip');
                    if (tooltipText) {
                        let tooltip = document.querySelector('.custom-tooltip');
                        if (!tooltip) {
                            tooltip = document.createElement('div');
                            tooltip.className = 'custom-tooltip';
                            document.body.appendChild(tooltip);
                        }
                        tooltip.textContent = tooltipText;
                        tooltip.style.display = 'block';
                        tooltip.style.left = (e.pageX + 10) + 'px';
                        tooltip.style.top = (e.pageY - 20) + 'px';
                    }
                }).on('mousemove', function (e) {
                    const tooltip = document.querySelector('.custom-tooltip');
                    if (tooltip && tooltip.style.display === 'block') {
                        tooltip.style.left = (e.pageX + 10) + 'px';
                        tooltip.style.top = (e.pageY - 20) + 'px';
                    }
                }).on('mouseout', function () {
                    const tooltip = document.querySelector('.custom-tooltip');
                    if (tooltip) {
                        tooltip.style.display = 'none';
                    }
                });
                // Đánh dấu phần tử đã được gắn sự kiện
                $this.data('tooltip-initialized', true);
            }
        });
    }

    // Hàm tải và hiển thị dữ liệu trong DataTable
    function loadSumMaterialsTable() {
        fetch('http://10.220.130.119:9090/api/MaterialSystem/GetAllSumMaterials', {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json'
            }
        })
            .then(response => {
                if (!response.ok) {
                    throw new Error(`Lỗi khi gọi API: ${response.status} ${response.statusText}`);
                }
                return response.json();
            })
            .then(data => {
                console.log('SumMaterials Data:', data); // Ghi log để kiểm tra
                if ($.fn.DataTable.isDataTable('#sumMaterialsTable')) {
                    $('#sumMaterialsTable').DataTable().destroy();
                }
                table = $('#sumMaterialsTable').DataTable({
                    data: data,
                    scrollX: true,
                    columns: [
                        {
                            data: null,
                            orderable: false,
                            render: function (data, type, row, meta) {
                                return `<input type="checkbox" class="row-checkbox" data-row-index="${meta.row}" />`;
                            }
                        },
                        { data: 'mA_LIEU', defaultContent: '', render: createTooltipCell },
                        { data: 'nhA_CUNG_UNG', defaultContent: '', render: createTooltipCell },
                        { data: 'model', defaultContent: '', render: createTooltipCell },
                        { data: 'datE_CODE', defaultContent: '', render: createTooltipCell },
                        { data: 'loT_CODE', defaultContent: '', render: createTooltipCell },
                        { data: 'sO_LUONG_OK', defaultContent: '', render: createTooltipCell },
                        { data: 'sO_LUONG_NG', defaultContent: '', render: createTooltipCell },
                        { data: 'location', defaultContent: '', render: createTooltipCell },
                        { data: 'esd', defaultContent: '', render: createTooltipCell },
                        { data: 'chO_MUON', defaultContent: '', render: createTooltipCell },
                        { data: 'dA_BAO_PHE', defaultContent: '', render: createTooltipCell },
                        { data: 'tonG_LINH', defaultContent: '', render: createTooltipCell },
                        { data: 'vitrI_TRENBAN', defaultContent: '', render: createTooltipCell },
                        { data: 'remark', defaultContent: '', render: createTooltipCell }
                    ],
                    pageLength: 10,
                    responsive: true,
                    language: {
                        url: '/lib/datatable/i18n/vi.json' // Sử dụng file local
                    },
                    order: [[1, 'asc']], // Sắp xếp mặc định theo cột thứ 2 (maAll) thay vì cột đầu tiên
                    drawCallback: function () {
                        attachTooltipEvents();
                    }
                });

                // Xóa danh sách hàng được chọn khi bảng được tải lại
                selectedRows = [];

                // Sự kiện chọn checkbox từng hàng
                $('#sumMaterialsTable tbody').on('change', '.row-checkbox', function () {
                    const rowIndex = $(this).data('row-index');
                    const rowData = table.row(rowIndex).data();

                    if ($(this).is(':checked')) {
                        if (!selectedRows.includes(rowData)) {
                            selectedRows.push(rowData);
                        }
                    } else {
                        selectedRows = selectedRows.filter(row => row !== rowData);
                    }
                    console.log('Hàng được chọn:', selectedRows);
                });
                // Sự kiện chọn tất cả checkbox
                selectAllCheckbox.addEventListener('change', function () {
                    const isChecked = this.checked;
                    $('.row-checkbox').prop('checked', isChecked);
                    if (isChecked) {
                        selectedRows = table.rows().data().toArray();
                    } else {
                        selectedRows = [];
                    }
                    console.log('Hàng được chọn:', selectedRows);
                });
            })
            .catch(error => {
                console.error('Lỗi khi tải dữ liệu bảng:', error);
                showError("Có lỗi xảy ra!");
            });
    }

    // Hàm xuất dữ liệu sang Excel sử dụng xlsx.full.min.js
    function exportToExcel() {
        if (!table || !$.fn.DataTable.isDataTable('#sumMaterialsTable')) {
            showWarning("Bảng dữ liệu chưa được tải!");
            return;
        }

        // Lấy dữ liệu từ DataTable
        const data = table.rows().data().toArray();

        // Tạo workbook và worksheet
        const wb = XLSX.utils.book_new();
        const ws = XLSX.utils.json_to_sheet(data);

        // Thêm worksheet vào workbook
        XLSX.utils.book_append_sheet(wb, ws, "Sum Materials");

        // Xuất file Excel
        const fileName = `sum_materials_${new Date().toISOString().slice(0, 10)}.xlsx`;
        XLSX.writeFile(wb, fileName);
    }

    // Sự kiện cho nút xuất Excel
    exportExcelBtn.addEventListener('click', function () {
        exportToExcel();
    });

    if (borrowBtn) {
        borrowBtn.addEventListener('click', async function () {
            if (selectedRows.length === 0) {
                showWarning("Vui lòng chọn ít nhất 1 hàng!")
                return;
            }
            try {
                const { value: formValues } = await Swal.fire({
                    title: 'Nhập thông tin phát liệu',
                    html:
                       `<div>
                            <label for="qtyOK">Số lượng OK:</label>
                            <input type="number" id="swal-qtyOK" class="swal2-input" placeholder="Nhập số lượng OK" min="0" value="0">
                        </div>
                        <div>
                            <label for="qtyNG">Số lượng NG:</label>
                            <input type="number" id="swal-qtyNG" class="swal2-input" placeholder="Nhập số lượng NG" min="0" value="0">
                        </div>
                        <div>
                            <label for="opBorrow">Người nhận:</label>
                            <input type="text" id="swal-opBorrow" class="swal2-input" placeholder="Nhập mã người nhận">
                        </div>`,
                    focusConfirm: false,
                    preConfirm: () => {
                        const qtyOK = parseInt(document.getElementById('swal-qtyOK').value || 0);
                        const qtyNG = parseInt(document.getElementById('swal-qtyNG').value || 0);
                        const opBorrow = document.getElementById('swal-opBorrow').value.trim();

                        if (!opBorrow) {
                            Swal.showValidationMessage('Vui lòng nhập mã người nhận.');
                            return false;
                        }
                        return { qtyOK, qtyNG, opBorrow };
                    },
                    showCancelButton: true,
                    confirmButtonText: 'Xác nhận',
                    cancelButtonText: 'Hủy',
                    showLoaderOnConfirm: true,
                    allowOutsideClick: () => !Swal.isLoading()
                });

                if (!formValues) return; // Nếu người dùng hủy

                // Gửi từng hàng được chọn đến API BorrowMaterial
                for (const row of selectedRows) {
                    const response = await fetch('http://10.220.130.119:9090/api/MaterialSystem/BorrowMaterial', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json'
                        },
                        body: JSON.stringify({
                            maLieu: row.mA_LIEU || '',
                            nhaCungUng: row.nhA_CUNG_UNG || '',
                            dateCode: row.datE_CODE || '',
                            lotCode: row.loT_CODE || '',
                            qtyOK: formValues.qtyOK,
                            qtyNG: formValues.qtyNG,
                            op: currentUser,
                            oPborrow: formValues.opBorrow
                        })
                    });

                    if (!response.ok) {
                        throw new Error(`Lỗi khi gọi API BorrowMaterial: ${response.status} ${response.statusText}`);
                    }

                    const result = await response.json();
                    console.log('Kết quả phát liệu:', result);
                }
                showSuccess("Success!")
                loadSumMaterialsTable();
            } catch (error) {
                console.error('Lỗi khi phát liệu:', error);
                showError("Lỗi khi phát liệu!")
            }
        })

    }

    //Chức năng xuất liệu
    if (exportBtn) {
        exportBtn.addEventListener('click', async function () {
            if (selectedRows.length === 0) {
                showWarning("Vui lòng chọn ít nhất 1 hàng!");
                return;
            }
            try {
                const { value: formValues } = await Swal.fire({
                    title: 'Nhap thong tin xuat lieu',
                    html: `
                <div>
                    <label for="qty">Số lượng:</label>
                    <input type="number" id="swal-qty" class="swal2-input" placeholder="Nhập số lượng xuất" min="0" value="0">
                </div>
                <div>
                    <label for="remark">Ghi chú (Remark):</label>
                    <input type="text" id="swal-remark" class="swal2-input" placeholder="Nhập ghi chú">
                </div>`,
                    focusConfirm: false,
                    preConfirm: () => {
                        const qty = parseInt(document.getElementById('swal-qty').value) || 0;
                        const remark = document.getElementById('swal-remark').value.trim();

                        if (qty <= 0) {
                            Swal.showValidationMessage('So luong phai > 0');
                            return false;
                        }
                        return { qty, remark };
                    },
                    showCancelButton: true,
                    confirmButtonText: 'XAC NHAN',
                    cancelButtonText: 'HUY',
                    showLoaderOnConfirm: true,
                    allowOutsideClick: () => !Swal.isLoading()
                });

                if (!formValues) return;

                //Gui tung hang duoc chon den API
                for (const row of selectedRows) {
                    const response = await fetch('http://10.220.130.119:9090/api/MaterialSystem/ExportDefectiveMaterial', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json'
                        },
                        body: JSON.stringify({
                            maLieu: row.mA_LIEU || '',
                            nhaCungUng: row.nhA_CUNG_UNG || '',
                            dateCode: row.datE_CODE || '',
                            lotCode: row.loT_CODE || '',
                            qty: formValues.qty,
                            op: currentUser, // Thay bằng thông tin người dùng thực tế nếu cần
                            remark: formValues.remark
                        })
                    });
                    if (!response.ok) {
                        throw new Error(`Lỗi khi gọi API ExportDefectiveMaterial: ${response.status} ${response.statusText}`);
                    }

                    const result = await response.json();
                    console.log('Kết quả xuất liệu:', result);
                }
                showSuccess("Success!")
                loadSumMaterialsTable();
            } catch (error) {
                console.error('Lỗi khi xuất liệu:', error);
                showError("Lỗi Khi xuất liệu!")
            }
        })

    }
    // Tải bảng khi trang tải
    loadSumMaterialsTable();
});