document.addEventListener('DOMContentLoaded', function () {
    const exportExcelBtn = document.getElementById('export-excel-btn');
    const returnBtn = document.getElementById('return-btn');
    const selectAllCheckbox = document.getElementById('selectAllCheckbox');
    const selectTypeBtn = document.getElementById('select-type-btn');
    const currentUser = document.getElementById('entryPerson').value;
    let selectedRows = [];
    let table = null;

    // Hàm tạo nội dung cho ô với tooltip
    function createTooltipCell(data) {
        return `<span class="tooltip-trigger" data-tooltip="${data || ''}">${data || ''}</span>`;
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

    // Hàm tải dữ liệu lịch sử dựa trên type
    async function loadHistoryTable(type) {
        try {
            const response = await fetch('http://10.220.130.119:9090/api/MaterialSystem/GetHistory', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ type: type }) // Gửi type dưới dạng JSON
            });

            if (!response.ok) {
                throw new Error(`Lỗi khi gọi API: ${response.status} ${response.statusText}`);
            }

            const data = await response.json();
            console.log('Dữ liệu lịch sử:', data);

            // Hủy bảng cũ nếu đã tồn tại
            if ($.fn.DataTable.isDataTable('#historyMaterialsTable')) {
                $('#historyMaterialsTable').DataTable().destroy();
            }

            // Tạo bảng DataTable mới
            table = $('#historyMaterialsTable').DataTable({
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
                    { data: 'task', defaultContent: '', render: createTooltipCell },
                    { data: 'mA_LIEU', defaultContent: '', render: createTooltipCell },
                    { data: 'mA_ALL', defaultContent: '', render: createTooltipCell },
                    { data: 'nhA_CUNG_UNG', defaultContent: '', render: createTooltipCell },
                    { data: 'datE_CODE', defaultContent: '', render: createTooltipCell },
                    { data: 'loT_CODE', defaultContent: '', render: createTooltipCell },
                    { data: 'type', defaultContent: '', render: createTooltipCell },
                    { data: 'qtY1', defaultContent: '', render: createTooltipCell },
                    { data: 'qtY2', defaultContent: '', render: createTooltipCell },
                    { data: 'timE1', defaultContent: '', render: createTooltipCell },
                    { data: 'oP1', defaultContent: '', render: createTooltipCell },
                    { data: 'timE2', defaultContent: '', render: createTooltipCell },
                    { data: 'oP2', defaultContent: '', render: createTooltipCell },
                    { data: 'esd', defaultContent: '', render: createTooltipCell },
                    { data: 'location', defaultContent: '', render: createTooltipCell },
                    { data: 'remark', defaultContent: '', render: createTooltipCell },
                    { data: 'qtY3', defaultContent: '', render: createTooltipCell },
                    { data: 'qtY4', defaultContent: '', render: createTooltipCell }
                ],
                pageLength: 10,
                responsive: true,
                language: {
                    url: '/lib/datatable/i18n/vi.json'
                },
                order: [[10, 'desc']], // Sắp xếp mặc định theo cột TIME (giảm dần)
                drawCallback: function () {
                    attachTooltipEvents();
                }
            });

            // Xóa danh sách hàng được chọn khi bảng được tải lại
            selectedRows = [];

            // Sự kiện chọn checkbox từng hàng
            $('#historyMaterialsTable tbody').on('change', '.row-checkbox', function () {
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
            if (selectAllCheckbox) {
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
            }
        } catch (error) {
            console.error('Lỗi khi tải dữ liệu bảng:', error);
            showError("Có lỗi xảy ra khi tải dữ liệu!");
        }
    }

    // Hàm hiển thị dropdown chọn type
    async function showTypeSelection() {
        const { value: type } = await Swal.fire({
            title: "Chọn Type",
            input: "select",
            inputOptions: {
                "":"Tất cả(All)",
                Input: "Nhập kho",
                Borrow: "Phát Liệu",
                DefectiveExport: "Xuất kho"
            },
            inputPlaceholder: "Chọn...",
            showCancelButton: true,
            inputValidator: (value) => {
                return new Promise((resolve) => {
                    if (value !== undefined) {
                        resolve();
                    } else {
                        resolve("Vui lòng chọn Type!");
                    }
                });
            }
        });

        if (type!== undefined) {
            Swal.fire(`Bạn đã chọn: ${type || 'Tất cả'}`);
            loadHistoryTable(type);
        }
    }

    // Sự kiện cho nút "Chọn Loại Giao Dịch"
    if (selectTypeBtn) {
        selectTypeBtn.addEventListener('click', function () {
            showTypeSelection();
        });
    }

    // Hàm xuất dữ liệu sang Excel
    function exportToExcel() {
        if (!table || !$.fn.DataTable.isDataTable('#historyMaterialsTable')) {
            showWarning("Bảng dữ liệu chưa được tải!");
            return;
        }

        const data = table.rows().data().toArray();
        const wb = XLSX.utils.book_new();
        const ws = XLSX.utils.json_to_sheet(data);
        XLSX.utils.book_append_sheet(wb, ws, "History Materials");
        const fileName = `history_materials_${new Date().toISOString().slice(0, 10)}.xlsx`;
        XLSX.writeFile(wb, fileName);
    }

    // Sự kiện cho nút xuất Excel
    if (exportExcelBtn) {
        exportExcelBtn.addEventListener('click', function () {
            exportToExcel();
        });
    }

    // Sự kiện cho nút TRẢ
    if (returnBtn) {
        returnBtn.addEventListener('click', async function () {
            if (selectedRows.length === 0) {
                showWarning("Vui lòng chọn ít nhất 1 hàng!");
                return;
            }

            try {
                const { value: formValues } = await Swal.fire({
                    title: 'Nhập thông tin thu liệu',
                    html: `
                    <div>
                        <label for="qtyOK">Số lượng OK:</label>
                        <input type="number" id="swal-qtyOK" class="swal2-input" placeholder="Nhập số lượng OK" min="0" value="0">
                    </div>
                    <div>
                        <label for="qtyNG">Số lượng NG:</label>
                        <input type="number" id="swal-qtyNG" class="swal2-input" placeholder="Nhập số lượng NG" min="0" value="0">                        
                    </div>
                    <div>
                        <label for="remark">Remark (Note):</label>
                        <input type="text" id="swal-remark" class="swal2-input" placeholder="Nhập ...">
                    </div>
                    <div>
                        <label for="opReturn">Người trả(OP):</label>
                        <input type="text" id="swal-opReturn" class="swal2-input" placeholder="Nhập mã người trả">
                    </div>
                    `,
                    focusConfirm: false,
                    preConfirm: () => {
                        const qtyOK = parseInt(document.getElementById('swal-qtyOK').value || 0);
                        const qtyNG = parseInt(document.getElementById('swal-qtyNG').value || 0);
                        const remark = document.getElementById('swal-remark').value.trim();
                        const opReturn = document.getElementById('swal-opReturn').value.trim();

                        if (!opReturn) {
                            Swal.showValidationMessage('Vui lòng nhập mã người trả.');
                            return false;
                        }
                        return { qtyOK, qtyNG,remark, opReturn };
                    },
                    showCancelButton: true,
                    confirmButtonText: 'Xác nhận',
                    cancelButtonText: 'Hủy',
                    showLoaderOnConfirm: true,
                    allowOutsideClick: () => !Swal.isLoading()
                });

                if (!formValues) return;

                // Gửi từng hàng được chọn đến API ReturnMaterial
                for (const row of selectedRows) {
                    const response = await fetch('http://10.220.130.119:9090/api/MaterialSystem/ReturnMaterial', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json'
                        },
                        body: JSON.stringify({
                            task: row.task || '',
                            maLieu: row.mA_LIEU || '',
                            nhaCungUng: row.nhA_CUNG_UNG || '',
                            dateCode: row.datE_CODE || '',
                            lotCode: row.loT_CODE || '',
                            qtyOK: formValues.qtyOK,
                            qtyNG: formValues.qtyNG,
                            remark: formValues.remark || '',
                            op: formValues.opReturn, // Người trả
                        })
                    });

                    if (!response.ok) {
                        throw new Error(`Lỗi khi gọi API ReturnMaterial:${response.status} ${response.statusText}`);
                    }

                    const result = await response.json();
                    console.log('Kết quả thu liệu:', result);
                }
                showSuccess("Nhận liệu thành công!");
                loadHistoryTable('Borrow'); // Tải lại bảng với type "Return"
            } catch (error) {
                console.error('Lỗi khi trả liệu:', error.message);
                showError("Tổng liệu OK + NG (thu lại) phải bằng số lượng phát!");
            }
        });
    }
    // Tải toàn bộ dữ liệu ngay khi trang được tải
    loadHistoryTable("");
});