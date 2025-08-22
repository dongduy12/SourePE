// Module: ApiService - Quản lý tất cả các gọi API
const ApiService = (function () {
    const BASE_URL = 'http://10.220.130.119:9090/api';

    async function fetchAPI(endpoint, method, body = null) {
        const options = {
            method,
            headers: {
                'Content-Type': 'application/json'
            }
        };

        if (body) {
            options.body = JSON.stringify(body);
        }

        try {
            const response = await fetch(`${BASE_URL}${endpoint}`, options);
            if (!response.ok) {
                throw new Error(`Lỗi API: ${response.statusText}`);
            }
            return await response.json();
        } catch (error) {
            console.error(`Lỗi khi gọi API ${endpoint}:`, error);
            throw error;
        }
    }

    async function uploadFile(endpoint, formData) {
        try {
            const response = await fetch(`${BASE_URL}${endpoint}`, {
                method: 'POST',
                body: formData
            });
            if (!response.ok) {
                throw new Error(`Lỗi API: ${response.statusText}`);
            }
            return await response.json();
        } catch (error) {
            console.error(`Lỗi khi upload file ${endpoint}:`, error);
            throw error;
        }
    }

    return {
        searchFA: (payload) => fetchAPI('/SearchFA/search', 'POST', payload),
        searchHistory: (payload) => fetchAPI('/SearchFA/search-history-by-list', 'POST', payload),
        getFullName: (username) => fetchAPI(`/SearchFA/get-fullname?username=${username}`, 'GET'),
        getProductLines: () => fetchAPI('/FixGuide/GetProductLines', 'GET'),
        getCheckPoints: (payload) => fetchAPI('/FixGuide/GetCheckPoints', 'POST', payload),
        updateCheckPoint: (payload) => fetchAPI('/FixGuide/update-checkpoint', 'PUT', payload),
        uploadGuide: (formData) => uploadFile('/FixGuide/Upload', formData),
        repairStatus: (payload) => fetchAPI('/RepairStatus/repair-status', 'POST', payload),
        handOverStatus: (payload) => fetchAPI('/RepairStatus/hand-over-status', 'POST', payload),
        receivingStatus: (payload) => fetchAPI('/RepairStatus/receiving-status', 'POST', payload),
        getAllowedAreas: (payload) => fetchAPI('/SearchFA/get-allowed-areas', 'POST', payload),
        getLatestTester: (payload) => fetchAPI('/SearchFA/get-latest-tester', 'POST', payload),
        getStatusCounts: (type) => fetchAPI('/SearchFA/get-status-counts', 'POST', type),
        getLocationCounts: () => fetchAPI('/DataChart/getCountLocation', 'GET'),
        getAgingCounts: () => fetchAPI('/DataChart/get-aging-counts', 'GET'),
        getRepairInfo: (serialNumber) => fetchAPI(`/RepairTaskDetail/data19/${serialNumber}`, 'GET')
    };
})();

// Module: DataTableManager - Quản lý DataTable
const DataTableManager = (function () {
    let snTable, repairHistoryTable, testerInfoTable;
    const handoverStatusMap = new Map();

    //Hiển thị table
    function showTable() {
        $('#sn-table-wrapper').removeClass('d-none');
    }

    //Ẩn table
    function hideTable() {
        $('#sn-table-wrapper').addClass('d-none');
    }

    function initializeSnTable() {
        snTable = $('#sn-table').DataTable({
            dom: 't',
            paging: false,
            ordering: false,
            info: false,
            scrollX: true,
            scrollY: '600px', // Chiều cao tối đa của bảng
            scrollCollapse: true, // Tự động điều chỉnh chiều cao
            fixedColumns: {
                leftColumns: 1 // Ghim cột đầu tiên
            },
            columnDefs: [
                { width: '140px', targets: 0 }, // SERIAL_NUMBER
                { width: '140px', targets: 1 }, // PRODUCT_LINE
                { width: '100px', targets: 2 }, // MODEL_NAME
                { width: '100px', targets: 3 }, // WIP
                { width: '100px', targets: 4 }, // TEST_GROUP
                { width: '100px', targets: 5 }, // TEST_CODE
                { width: '140px', targets: 6 }, // ERROR_DESC
                { width: '100px', targets: 7 }, // PRE_STATUS
                { width: '100px', targets: 8 }, // STATUS
                { width: '140px', targets: 9 }, // DATE
                { width: '100px', targets: 10 }, // ID_NV
                { width: '160px', targets: 11 }, // CHECK_POINT
                { width: '100px', targets: 12 }, // DATA13 -- HANDOVER
                { width: '100px', targets: 13 }, // DATA18 -- VI TRI
                { width: '80px', targets: 14 }, // HƯỚNG_DẪN
                { width: '80px', targets: 15 }, // SỬA_CHỮA
                { width: '80px', targets: 16 }, // LỊCH_SỬ
                { width: '80px', targets: 17 }  // KHÁC
            ]
        });
    }

    function initializeRepairHistoryTable() {
        repairHistoryTable = $('#repair-history-table').DataTable({
            paging: true,
            searching: true,
            ordering: false,
            info: false
        });
    }

    function initializeTesterInfoTable() {
        testerInfoTable = $('#tester-info-table').DataTable({
            paging: true,
            searching: true,
            ordering: false,
            info: false
        });
    }

    function addRowToSnTable(item, checkPointData, fullName) {
        showTable();
        const checkpointsArray = (checkPointData.success && checkPointData.checkPoints)
            ? Array.isArray(checkPointData.checkPoints?.$values)
                ? checkPointData.checkPoints.$values
                : (typeof checkPointData.checkPoints === 'string' ? [checkPointData.checkPoints] : [checkPointData.checkPoints.toString()])
            : [""];

        const encodedCheckpoints = encodeURIComponent(JSON.stringify(checkpointsArray));
        const encodedDetail = encodeURIComponent(checkpointsArray.detail || 'Không có thông tin chi tiết');

        const rowCountBefore = snTable.data().count();

        snTable.row.add([
            item.seriaL_NUMBER.trim(),
            item.productLine || '',
            item.modeL_NAME || '',
            item.wiP_GROUP || '',
            item.tesT_GROUP || '',
            `<span title="${item.tesT_CODE || ''}">${truncateText(item.tesT_CODE, 20)}</span>`,
            `<span title="${item.datA1 || ''}">${truncateText(item.datA1, 20)}</span>`,
            item.datA12 || '',
            item.datA11 || '',
            `<span title="${item.datE3 || ''}">${item.datE3 || ''}</span>`,
            `<span title="${item.tester}">${fullName || item.tester || 'N/A'}</span>`,
            `<span title="${checkpointsArray.join(', ') || ''}">${checkpointsArray.join(', ') || ''}</span>`,
            item.datA13 || '',
            item.datA18 || '',
            `<button class="btn btn-info btn-sm view-detail" 
                    data-sn="${item.seriaL_NUMBER}" 
                    data-model="${item.modeL_NAME}" 
                    data-product-line="${item.productLine}" 
                    data-checkpoints="${encodedCheckpoints}" 
                    data-detail="${encodedDetail}">
                    Chi tiết
            </button>`,
            `<button class="btn btn-warning btn-sm save-repair-history" 
                    data-serial-number="${item.seriaL_NUMBER}">
                    Thêm
            </button>`,
            `<button class="btn btn-info btn-sm btn-repair-detail"
                    data-serial-number="${item.seriaL_NUMBER}">
                    Chi tiết
            </button>`,
            `<button class="btn btn-success btn-delete">Xóa</button>`
        ]).draw(false);

        handoverStatusMap.set(item.seriaL_NUMBER.trim(), item.datA13 || null);

        snTable.columns.adjust().draw(false);

        if (rowCountBefore === 0) {
            $('#sn-table_wrapper').show();
        }
    }

    function removeRow(rowElement, existingSNs) {
        const row = snTable.row(rowElement);
        const rowData = row.data();
        const snToRemove = rowData[0]?.trim();

        if (existingSNs.has(snToRemove)) {
            existingSNs.delete(snToRemove);
        }

        handoverStatusMap.delete(snToRemove);

        row.remove().draw();
        if (snTable.rows().count() === 0) {
            hideTable();
        }
    }

    function updateSnTable(serialNumbers, updatedData) {
        snTable.rows().every(function () {
            const rowData = this.data();
            const updatedRow = updatedData.data.find(item => item.seriaL_NUMBER.trim() === rowData[0]);

            if (updatedRow) {
                rowData[7] = updatedRow.datA12 || '';
                rowData[8] = updatedRow.datA11 || '';
                rowData[9] = updatedRow.datE3 || '';
                rowData[10] = updatedRow.tester || '';
                handoverStatusMap.set(rowData[0].trim(), updatedRow.datA13 || null);
                this.data(rowData).draw(false);
            }
        });
    }

    async function populateRepairHistoryTable(history) {
        repairHistoryTable.clear();

        const filteredHistory = history.filter(item => item.datA19 !== null);
        if (filteredHistory.length === 0) {
            showInfo("Không có lịch sử Repair!");
            return;
        }

        for (const item of filteredHistory) {
            let fullName = item.tester || 'N/A';
            try {
                const fullNameData = await ApiService.getFullName(item.tester);
                fullName = typeof fullNameData === 'object' ? (fullNameData?.fullName || item.tester) : (fullNameData || item.tester);
            } catch (error) {
                console.warn(`Không lấy được FullName cho tester ${item.tester}:`, error);
            }

            repairHistoryTable.row.add([
                item.seriaL_NUMBER || '',
                item.datA12 || '',
                item.datA11 || '',
                item.datE3 ? new Date(item.datE3).toLocaleString() : '',
                `<span title="${item.tester}">${fullName}</span>`,
                `<span title="${item.datA19 || ''}">${item.datA19 || ''}</span>`,
                `<span title="${item.datA17 || ''}">${item.datA17 || ''}</span>`
            ]);
        }

        repairHistoryTable.draw();
    }

    async function populateTesterInfoTable(data) {
        testerInfoTable.clear();

        for (const item of data) {
            let fullName = item.tester || 'N/A';
            try {
                const fullNameData = await ApiService.getFullName(item.tester);
                fullName = typeof fullNameData === 'object' ? (fullNameData?.fullName || item.tester) : (fullNameData || item.tester);
            } catch (error) {
                console.warn(`Không lấy được FullName cho tester ${item.tester}:`, error);
            }

            testerInfoTable.row.add([
                item.seriaL_NUMBER || '',
                item.tester || '',
                fullName
            ]);
        }

        testerInfoTable.draw();
    }

    function getAllSerialNumbers() {
        const serialNumbers = [];
        snTable.rows().every(function () {
            const rowData = this.data();
            serialNumbers.push(rowData[0].trim());
        });
        return serialNumbers;
    }

    function getRowData(serialNumber) {
        let rowData = null;
        snTable.rows().every(function () {
            const data = this.data();
            if (String(data[0]) === String(serialNumber)) {
                rowData = data;
                return false;
            }
        });
        return rowData;
    }

    function removeRowsBySerialNumbers(serialNumbers, existingSet) {
        snTable.rows().every(function () {
            const data = this.data();
            const sn = data[0].trim();
            if (serialNumbers.includes(sn)) {
                if (existingSet) {
                    existingSet.delete(sn);
                }
                this.remove();
            }
        });
        snTable.draw(false);
        if (snTable.rows().count() === 0) {
            hideTable();
        }
    }

    function clearSnTable() {
        snTable.clear().draw(false);
        handoverStatusMap.clear();
        hideTable();
    }

    function truncateText(text, maxLength) {
        if (text && text.length > maxLength) {
            return text.substring(0, maxLength) + "...";
        }
        return text || "";
    }

    return {
        initSnTable: initializeSnTable,
        initRepairHistoryTable: initializeRepairHistoryTable,
        initTesterInfoTable: initializeTesterInfoTable,
        addRowToSnTable,
        removeRow,
        updateSnTable,
        populateRepairHistoryTable,
        populateTesterInfoTable,
        getAllSerialNumbers,
        getRowData,
        removeRowsBySerialNumbers,
        clearSnTable,
        showTable,
        hideTable,
        getHandoverStatus: sn => handoverStatusMap.get(sn)
    };
})();

window.DataTableManager = DataTableManager;

// Module: FormHandler - Xử lý form và sự kiện nhập liệu
const FormHandler = (function () {
    let existingSNs = new Set();

    function setupEventListeners() {
        $('#sn-form').on('submit', handleFormSubmit);
        $('#sn-table tbody').on('click', '.btn-delete', handleRowDelete);
        $('#sn-table tbody').on('click', '.view-detail', ModalManager.handleViewDetail);
        $('#sn-table tbody').on('click', '.btn-repair-detail', RepairHistoryManager.handleRepairDetailClick);
        $('#sn-table tbody').on('click', '.save-repair-history', RepairHistoryManager.handleSaveRepairHistoryClick);
        $('#refreshButton').on('click', handleRefresh);
    }

    async function handleFormSubmit(e) {
        e.preventDefault();
        const serialNumberInput = $('#serialNumberList').val().trim();

        if (!serialNumberInput) {
            showWarning('Vui lòng nhập Serial Number!');
            return;
        }

        const serialNumbers = serialNumberInput.split(/[\n,]+/).map(sn => sn.trim()).filter(sn => sn);
        if (serialNumbers.length === 0) {
            showWarning('Danh sách Serial Number không hợp lệ!');
            return;
        }

        const duplicates = serialNumbers.filter(sn => existingSNs.has(sn));
        if (duplicates.length > 0) {
            showWarning(`Các SN đã tồn tại: ${duplicates.join(', ')}`);
            return;
        }

        try {
            const data = await ApiService.searchFA({
                serialNumbers,
                modelName: "",
                testCode: "",
                status: "",
                data1: "",
                handoverStatus: ""
            });

            await processSerialNumberData(data, serialNumbers);
            $('#serialNumberList').val('');
        } catch (error) {
            showError("Lỗi khi gọi API!");
        }
    }

    async function processSerialNumberData(data, serialNumbers) {
        if (!data.data || data.data.length === 0) {
            showWarning('Không tìm thấy thông tin cho các Serial Number!');
            return;
        }

        for (const item of data.data) {
            const sn = item.seriaL_NUMBER?.trim() || '';
            if (sn === '' || existingSNs.has(sn)) continue;

            existingSNs.add(sn);

            const standardizedProductLine = standardizeProductLine(item.productLine);
            const checkPointsPayload = {
                productLine: standardizedProductLine,
                testCode: item.tesT_CODE || ''
            };

            let checkPointData = { checkPoints: "" };
            try {
                const apiResponse = await ApiService.getCheckPoints(checkPointsPayload);
                if (apiResponse?.success && apiResponse.checkPoints) {
                    checkPointData = apiResponse;
                }
            } catch (error) {
                console.warn(`Không tìm thấy CheckPoints cho SN ${sn}:`, error);
            }

            let fullName = item.tester || 'N/A';
            try {
                const fullNameData = await ApiService.getFullName(item.tester);
                fullName = typeof fullNameData === 'object' ? (fullNameData?.fullName || item.tester) : (fullNameData || item.tester);
            } catch (error) {
                console.warn(`Không lấy được FullName cho tester ${item.tester} của SN ${sn}:`, error);
            }
            DataTableManager.addRowToSnTable(item, checkPointData, fullName);
        }
    }

    function handleRowDelete() {
        DataTableManager.removeRow($(this).parents('tr'), existingSNs);
    }

    function handleRefresh() {
        existingSNs.clear();
        DataTableManager.clearSnTable();
        $('#serialNumberList').val('');
    }

    function standardizeProductLine(productLine) {
        return productLine ? productLine.replace(/\s|-/g, '').toUpperCase() : '';
    }

    return {
        setup: setupEventListeners,
        getExistingSNs: () => existingSNs
    };
})();

// Module: ModalManager - Quản lý hiển thị và tương tác với modal
const ModalManager = (function () {
    function showModal(modalId) {
        $(modalId).modal('show');
    }

    function hideModal(modalId) {
        $(modalId).modal('hide');
    }

    function handleViewDetail() {
        const serialNumber = $(this).data('sn');
        const modelName = $(this).data('model');
        const productLine = $(this).data('product-line');
        const checkpoints = JSON.parse(decodeURIComponent($(this).data('checkpoints')));
        const detail = decodeURIComponent($(this).data('detail'));

        const standardizedProductLine = productLine ? productLine.replace(/\s|-/g, '').toUpperCase() : '';

        $('#modal-sn').text(serialNumber);
        $('#modal-model').text(modelName);
        $('#modal-product-line').text(productLine);

        $('#modal-checkpoints').empty();
        checkpoints.forEach(cp => $('#modal-checkpoints').append(`<li>${cp}</li>`));
        $('#modal-repair-detail').text(detail);

        const pdfUrl = `/pdfs/product-lines/${standardizedProductLine}.pdf`;
        $('#modal-product-line-pdf').attr('src', pdfUrl).on('error', function () {
            $(this).attr('src', '/pdfs/product-lines/default.pdf');
        });

        showModal('#repairDetailModal');
    }
    return {
        showModal,
        hideModal,
        handleViewDetail
    };
})();

// Module: GuideManager - Quản lý chức năng thêm/sửa hướng dẫn sửa chữa
const GuideManager = (function () {
    function setupEventListeners() {
        $('#addGuideButton').on('click', handleAddGuide);
        $('#testCodeInput').on('blur', handleTestCodeBlur);
        $('#saveCheckPointButton').on('click', handleSaveCheckPoint);
        $('#templateButton').on('click', handleTemplateDownload);
        $('#uploadExcelButton').on('click', handleUploadExcel);
    }

    function handleAddGuide() {
        $('#productLineDropdown').empty();
        ApiService.getProductLines()
            .then(data => {
                data.productLines.forEach(productLine => {
                    $('#productLineDropdown').append(`<option value="${productLine}">${productLine}</option>`);
                });
            })
            .catch(() => showError('Lỗi khi lấy ProductLine'));
        ModalManager.showModal('#guideModal');
    }

    async function handleTestCodeBlur() {
        const productLine = $('#productLineDropdown').val();
        const testCode = $('#testCodeInput').val().trim();

        if (productLine && testCode) {
            try {
                const data = await ApiService.getCheckPoints({ productLine, testCode });
                $('#checkPointInput').val(data.checkPoints || 'Không tìm thấy CheckPoint trong database!, Vui lòng nhập CheckPoint mới.');
            } catch (error) {
                showError("Chưa có hướng dẫn");
            }
        }
    }

    async function handleSaveCheckPoint() {
        const productLine = $('#productLineDropdown').val();
        const testCode = $('#testCodeInput').val().trim();
        const checkPoint = $('#checkPointInput').val().trim();
        const employeeId = $('#analysisPerson').val();

        if (productLine && testCode && checkPoint) {
            const updateCheckPoint = `${checkPoint}.[${employeeId}]\n`;
            try {
                const data = await ApiService.updateCheckPoint({ productLine, testCode, check_Point: updateCheckPoint });
                if (data.success) {
                    showInfo(data.message);
                    $('#testCodeInput').val('');
                    $('#checkPointInput').val('');
                    ModalManager.hideModal('#guideModal');
                } else {
                    showError("Lỗi khi cập nhật CheckPoint!");
                }
            } catch (error) {
                showError('Lỗi khi gọi API');
            }
        } else {
            showWarning("Vui lòng điền đầy đủ thông tin!");
        }
    }

    async function handleTemplateDownload() {
        const templateButton = document.getElementById('templateButton');
        templateButton.disabled = true;
        templateButton.textContent = 'Đang tải...';

        try {
            const templateUrl = '/pdfs/file-excel/templateGuide.xlsx';
            const response = await fetch(templateUrl);
            if (!response.ok) {
                throw new Error('File không tồn tại trên server');
            }

            const link = document.createElement('a');
            link.href = templateUrl;
            link.download = 'templateGuide.xlsx';
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
        } catch (error) {
            showError('Không thể tải file template. Vui lòng liên hệ quản trị viên.');
        } finally {
            templateButton.disabled = false;
            templateButton.textContent = 'TEMPLATE';
        }
    }

    async function handleUploadExcel() {
        const file = $('#guideFileInput')[0].files[0];
        const productLine = $('#productLineDropdown').val();

        if (file && productLine) {
            const formData = new FormData();
            formData.append('file', file);
            formData.append('productLine', productLine);

            try {
                const data = await ApiService.uploadGuide(formData);
                if (data.success) {
                    showSuccess('Upload file thành công!');
                    ModalManager.hideModal('#guideModal');
                } else {
                    showError("Lỗi khi upload file!");
                }
            } catch (error) {
                showError('Lỗi kết nối API');
            }
        } else {
            showWarning('Vui lòng chọn ProductLine và file Excel');
        }
    }

    return {
        setup: setupEventListeners
    };
})();

// Module: StatusManager - Xử lý cập nhật trạng thái
const StatusManager = (function () {
    function canUpdateStatus(serialNumbers) {
        for (const sn of serialNumbers) {
            const hs = DataTableManager.getHandoverStatus(sn);
            if (hs === 'WAITING_HAND_OVER') {
                showWarning('PHẢI NHẬN BẢN MỚI CHUYỂN ĐƯỢC TRẠNG THÁI');
                return false;
            }
            if (hs !== null && hs !== undefined && hs !== '') {
                return false;
            }
        }
        return true;
    }

    function setupEventListeners() {
        $('.btn-fa, .btn-retest, .btn-vi, .btn-thay-lieu, .btn-check-list').on('click', handleBulkStatusUpdate);
        $('#btn-retest-status-update').on('click', handleRetestStatusUpdate);
        $('#btn-vi-status-update').on('click', handleVIStatusUpdate);
        $('#btn-thaylieu-status-update').on('click', handleThayLieuStatusUpdate);
        $('#btn-check-list-status-update').on('click', handleCheckListStatusUpdate);
    }

    async function handleBulkStatusUpdate() {
        const serialNumbers = DataTableManager.getAllSerialNumbers();
        if (serialNumbers.length === 0) {
            showWarning('Không có SN để cập nhật trạng thái!');
            return;
        }
        if ($(this).hasClass('btn-vi')) {
            //Kiểm tra nút .btn-vi tồn tại trong DOM
            if (!$('.btn-vi').length) {
                showError('Không có quyền cập nhật trạng thái VI-RE!');
                return;
            }
            //const statusNe = 'VI-RE';
            let firstStatus = null;
            let allSameStatus = true;
            const validStatuses = ['VI-RE'];//Các trạng thái hợp lệ cho VI-RE

            const filteredSerialNumbers = [];
            DataTableManager.getAllSerialNumbers().forEach((sn) => {
                const data = DataTableManager.getRowData(sn);
                if (!firstStatus) {
                    firstStatus = data[8]; //Cot STATUS
                } else if (data[8] !== firstStatus) {
                    allSameStatus = false;
                }
                if (validStatuses.includes(data[8])) {
                    filteredSerialNumbers.push(data[0]);
                }
            });
            if (!allSameStatus) {
                showWarning('SN phải cùng STATUS!');
                return;
            }
            if (filteredSerialNumbers.length === 0) {
                showWarning('Không có SN VI-RE!');
                return;
            }
            ModalManager.showModal('#modal-vi');
            $('select-vi-status').val('');
            $('notes-vi').val('');
        }
        else if ($(this).hasClass('btn-retest')) {
            if (!$('.btn-retest')) {
                showError('Khong co quyen update Status RETEST!');
                return;
            }
            let firstStatus = null;
            let allSameStatus = true;
            const validStatuses = ['RETEST'];//Mang Status hop le cho RETEST
            const filteredSerialNumbers = [];

            DataTableManager.getAllSerialNumbers().forEach((sn) => {
                const data = DataTableManager.getRowData(sn);
                if (!firstStatus) {
                    firstStatus = data[8];//Cot Status
                } else if (data[8] !== firstStatus) {
                    allSameStatus = false;
                }
                if (validStatuses.includes(data[8])) {
                    filteredSerialNumbers.push(data[0]);
                }
            });
            if (!allSameStatus) {
                showWarning('SN phải cùng STATUS!');
                return;
            }
            if (filteredSerialNumbers.length === 0) {
                showWarning('Không có SN RETEST!');
                return;
            }
            ModalManager.showModal('#modal-retest');
            $('#select-retest-status').val('');
            $('#notes-retest').val('');
            $('#select-retest-result').val('');
        }
        else if ($(this).hasClass('btn-thay-lieu')) {
            let firstStatus = null;
            let allSameStatus = true;
            const validStatuses = ['THAY LIỆU'];
            const filteredSerialNumbers = [];
            DataTableManager.getAllSerialNumbers().forEach((sn) => {
                const data = DataTableManager.getRowData(sn);
                if (!firstStatus) {
                    firstStatus = data[8];
                } else if (data[8] != firstStatus) {
                    allSameStatus = false;
                }
                if (validStatuses.includes(data[8])) {
                    filteredSerialNumbers.push(data[0]);
                }
            });
            if (!allSameStatus) {
                showWarning('SN phải cùng STATUS!');
                return;
            }
            if (filteredSerialNumbers.length === 0) {
                showWarning('Không có SN THAY LIỆU!');
                return;
            }

            //$('#modal-sn-thay-lieu').text(serialNumbers.join(', '));

            ModalManager.showModal('#modal-thaylieu');
            $('select-thaylieu-status').val('');
            $('#notes-thaylieu').val('');
        }
        else if ($(this).hasClass('btn-check-list')) {
            let firstStatus = null;
            let allSameStatus = true;
            const validStatuses = ['CHECK_LIST'];
            const filteredSerialNumbers = [];
            DataTableManager.getAllSerialNumbers().forEach((sn) => {
                const data = DataTableManager.getRowData(sn);
                if (!firstStatus) {
                    firstStatus = data[8];
                } else if (data[8] !== firstStatus) {
                    allSameStatus = false;
                }
                if (validStatuses.includes(data[8])) {
                    filteredSerialNumbers.push(data[0]);
                }
            });
            if (!allSameStatus) {
                showWarning('');
                return;
            }
            if (filteredSerialNumbers.length === 0) {
                showWarning('Không có SN CHECK LIST!');
                return;
            }

            ModalManager.showModal('#modal-check-list');
            $('#status-select-check-list').val('');
            $('#notes-check-list').val('');
        }
        else if ($(this).hasClass('btn-fa')) {
            const statusNe = 'FA';
            let firstStatus = null;
            let allSameStatus = true;

            const filteredSerialNumbers = [];
            DataTableManager.getAllSerialNumbers().forEach((sn) => {
                const data = DataTableManager.getRowData(sn);
                if (!firstStatus) {
                    firstStatus = data[8];
                } else if (data[8] !== firstStatus) {
                    allSameStatus = false;
                }
                if (data[8] === 'FA' || data[8] === 'PROCESS') {
                    filteredSerialNumbers.push(data[0]);
                }
            });

            if (!allSameStatus) {
                showError("Các SN không cùng trạng thái!");
                return;
            }

            if (filteredSerialNumbers.length === 0) {
                showWarning(`Không có SN cập nhật trạng thái ${statusNe}!`);
                return;
            }

            prepareTypeModal(statusNe, $('#type-modal'), filteredSerialNumbers);
        }
    }

    async function handleRetestStatusUpdate() {
        const selectedStatus = $('#select-retest-status').val();
        const selectedTestResult = $('#select-retest-result').val();
        const remark = $('#notes-retest').val().trim();
        const empId = $('#analysisPerson').val();
        const serialNumbers = DataTableManager.getAllSerialNumbers();
        const notes = `KQ:${selectedTestResult}: ${remark}`;

        if (!canUpdateStatus(serialNumbers)) {
            return;
        }

        if (!selectedStatus || !selectedTestResult) {
            showWarning('Vui lòng chọn trạng thái và kết quả retest!');
            return;
        }

        const payload = {
            serialNumbers: serialNumbers.join(','),
            type: 'RETEST',
            status: selectedStatus,
            employeeId: empId || 'default_emp_id',
            tag: 'Confirm',
            notes: notes,
            handOverStatus: 'WAITING_HAND_OVER'
        };

        //const handoverPayload = {
        //    serialNumbers: serialNumbers.join(','),
        //    handOverStatus: 'WAITING_HAND_OVER',
        //    tag: 'Giao'
        //};

        try {
            const result = await ApiService.repairStatus(payload);
            const cleanMessage = result.message.replace(/"/g, '').trim();
            if (result.success && cleanMessage === "OK") {
                //await ApiService.handOverStatus(handoverPayload);
                showSuccess("Cập nhật trạng thái thành công!");
                ModalManager.hideModal('#modal-retest');
                const updatedData = await ApiService.searchFA({ serialNumbers });
                DataTableManager.updateSnTable(serialNumbers, updatedData);
            } else {
                showError("Lỗi khi cập nhật trạng thái!");
            }
        } catch (error) {
            showError("Lỗi khi gọi API!");
        }
    }

    async function handleVIStatusUpdate() {
        const selectedStatus = $('#select-vi-status').val();
        const notes = $('#notes-vi').val().trim();
        const empId = $('#analysisPerson').val();
        const serialNumbers = DataTableManager.getAllSerialNumbers();

        if (!canUpdateStatus(serialNumbers)) {
            return;
        }

        if (!selectedStatus) {
            showWarning('Vui lòng chọn trạng thái!');
            return;
        }

        const payload = {
            serialNumbers: serialNumbers.join(','),
            type: 'VI-RE',
            status: selectedStatus,
            employeeId: empId || 'default_emp_id',
            tag: 'Confirm',
            notes: notes,
            handOverStatus: 'WAITING_HAND_OVER'
        };

        //const handoverPayload = {
        //    serialNumbers: serialNumbers.join(','),
        //    handOverStatus: 'WAITING_HAND_OVER',
        //    tag: 'Giao'
        //};

        try {
            const result = await ApiService.repairStatus(payload);
            const cleanMessage = result.message.replace(/"/g, '').trim();
            if (result.success && cleanMessage === "OK") {
                //await ApiService.handOverStatus(handoverPayload);
                showSuccess("Cập nhật trạng thái thành công!");
                ModalManager.hideModal('#modal-vi');
                const updatedData = await ApiService.searchFA({ serialNumbers });
                DataTableManager.updateSnTable(serialNumbers, updatedData);
            } else {
                showError("Lỗi khi cập nhật trạng thái!");
            }
        } catch (error) {
            showError("Lỗi khi gọi API!");
        }
    }

    async function handleThayLieuStatusUpdate() {
        const selectedStatus = $('#select-thaylieu-status').val();
        const notes = $('#notes-thaylieu').val().trim();
        const empId = $('#analysisPerson').val();
        const serialNumbers = DataTableManager.getAllSerialNumbers();

        if (!canUpdateStatus(serialNumbers)) {
            return;
        }

        if (!selectedStatus) {
            showWarning('Vui lòng chọn trạng thái!');
            return;
        }
        const payload = {
            serialNumbers: serialNumbers.join(','),
            type: 'THAY LIỆU',
            status: selectedStatus,
            employeeId: empId,
            tag: 'Confirm',
            notes: notes,
            handOverStatus: 'WAITING_HAND_OVER'
        };

        //const handoverPayload = {
        //    serialNumbers: serialNumbers.join(','),
        //    handOverStatus: 'WAITING_HAND_OVER',
        //    tag: 'Giao'
        //};

        try {
            const result = await ApiService.repairStatus(payload);
            const cleanMessage = result.message.replace(/"/g, '').trim();
            if (result.success && cleanMessage === "OK") {
                //await ApiService.handOverStatus(handoverPayload);
                showSuccess("Cập nhật trạng thái thành công!");
                ModalManager.hideModal('#modal-thaylieu');
                const updatedData = await ApiService.searchFA({ serialNumbers });
                DataTableManager.updateSnTable(serialNumbers, updatedData);
            } else {
                showError("Bản cần được nhận trước khi phân tích!");
            }
        } catch (error) {
            showError("Lỗi khi gọi API!", error);
        }
    }
    //async function handleThayLieuStatusUpdate() {
    //    const notes = $('#notes-thaylieu').val().trim();
    //    const serialNumbers = [];
    //    const statuses = [];

    //    DataTableManager.getAllSerialNumbers().forEach((sn) => {
    //        const rowData = DataTableManager.getRowData(sn);
    //        serialNumbers.push(rowData[0]);
    //        statuses.push(rowData[8]); // Sử dụng STATUS thay vì PRE_STATUS
    //    });

    //    if (!notes) {
    //        showWarning("Vui lòng nhập ghi chú!");
    //        return;
    //    }

    //    if (serialNumbers.length === 0) {
    //        showWarning("Không có SN trong database!");
    //        return;
    //    }

    //    const uniqueStatuses = [...new Set(statuses)];
    //    if (uniqueStatuses.length > 1) {
    //        showWarning("SN không cùng trạng thái!");
    //        return;
    //    }

    //    const status = uniqueStatuses[0];
    //    const payloadHistory = {
    //        serialNumbers: serialNumbers.join(','),
    //        type: 'THAY LIỆU',
    //        status: status,
    //        employeeId: $('#analysisPerson').val(),
    //        notes: notes,
    //        tag: 'confirm'
    //    };

    //    const payloadHandOver = {
    //        serialNumbers: serialNumbers.join(','),
    //        handOverStatus: 'WAITING_HAND_OVER',
    //        tag: 'giao'
    //    };

    //    try {
    //        const result = await ApiService.repairStatus(payloadHistory);
    //        const cleanMessage = result.message.replace(/"/g, '').trim();
    //        if (result.success && cleanMessage === "OK") {
    //            await ApiService.handOverStatus(payloadHandOver);
    //            showSuccess("Cập nhật trạng thái thành công!");
    //            ModalManager.hideModal('#modal-thaylieu');
    //            const updatedData = await ApiService.searchFA({ serialNumbers });
    //            DataTableManager.updateSnTable(serialNumbers, updatedData);
    //        } else {
    //            showError("Lỗi khi cập nhật trạng thái!");
    //        }
    //    } catch (error) {
    //        showError("Lỗi khi gọi API!");
    //    }
    //}

    async function handleCheckListStatusUpdate() {
        const selectedStatus = $('#status-select-check-list').val();
        const notes = $('#notes-check-list').val().trim();
        const empId = $('#analysisPerson').val();
        const serialNumbers = DataTableManager.getAllSerialNumbers();

        if (!canUpdateStatus(serialNumbers)) {
            return;
        }

        if (!selectedStatus) {
            showWarning('Vui lòng chọn trạng thái!');
            return;
        }

        const payload = {
            serialNumbers: serialNumbers.join(','),
            type: 'CHECK_LIST',
            status: selectedStatus,
            employeeId: empId || 'default_emp_id',
            tag: 'Confirm',
            notes: notes,
            handOverStatus: 'WAITING_HAND_OVER'
        };

        //const handoverPayload = {
        //    serialNumbers: serialNumbers.join(','),
        //    handOverStatus: 'WAITING_HAND_OVER',
        //    tag: 'Giao'
        //};

        try {
            const result = await ApiService.repairStatus(payload);
            const cleanMessage = result.message.replace(/"/g, '').trim();
            if (result.success && cleanMessage === "OK") {
                await ApiService.handOverStatus(handoverPayload);
                showSuccess("Cập nhật trạng thái thành công!");
                ModalManager.hideModal('#modal-check-list');
                const updatedData = await ApiService.searchFA({ serialNumbers });
                DataTableManager.updateSnTable(serialNumbers, updatedData);
            } else {
                showError("Lỗi khi cập nhật trạng thái!");
            }
        } catch (error) {
            showError("Lỗi khi gọi API!");
        }
    }

    function prepareTypeModal(statusNe, typeModal, serialNumbers) {
        $('#type-select, #retest-select').prop('required', false).hide();
        $('#additional-notes-container').hide();

        $('#type-select').off('change').on('change', function () {
            const selectedType = $(this).val();
            if (selectedType === 'RETEST' || selectedType === 'THAY LIỆU') {
                $('#additional-notes-container').show();
            } else {
                $('#additional-notes-container').hide();
            }
        });

        $('#type-select').show().prop('required', true);
        $('#type-select').val('');
        ModalManager.showModal('#type-modal');

        $('#type-form').off('submit').on('submit', async function (e) {
            e.preventDefault();

            if (!canUpdateStatus(serialNumbers)) {
                return;
            }

            const selectedType = $('#type-select').val();
            const additionalNotes = $('#additional-notes-input').val()?.trim();

            if (!selectedType) {
                showWarning("Vui lòng chọn giá trị Type!");
                return;
            }

            if ((selectedType === 'RETEST' || selectedType === 'THAY LIỆU') && !additionalNotes) {
                showWarning("Vui lòng nhập dữ liệu thay đổi!");
                return;
            }

            const payload = {
                serialNumbers: serialNumbers.join(','),
                type: statusNe,
                status: selectedType,
                employeeId: $('#analysisPerson').val(),
                notes: additionalNotes || "",
                tag: "Confirm",
                handOverStatus: 'WAITING_HAND_OVER'
            };

            //const handoverPayload = {
            //    serialNumbers: serialNumbers.join(','),
            //    handOverStatus: 'WAITING_HAND_OVER',
            //    tag: 'Giao'
            //};

            try {
                const result = await ApiService.repairStatus(payload);
                const cleanMessage = result.message.replace(/"/g, '').trim();
                if (result.success && cleanMessage === "OK") {
                    //await ApiService.handOverStatus(handoverPayload);
                    showSuccess("Cập nhật trạng thái thành công!");
                    ModalManager.hideModal('#type-modal');
                    const updatedData = await ApiService.searchFA({ serialNumbers });
                    DataTableManager.updateSnTable(serialNumbers, updatedData);
                } else {
                    showError("Lỗi khi cập nhật trạng thái!");
                }
            } catch (error) {
                showError("Lỗi khi gọi API!");
            }
        });
    }

    return {
        setup: setupEventListeners
    };
})();

// Module: RepairHistoryManager - Quản lý lịch sử sửa chữa
const RepairHistoryManager = (function () {
    function handleRepairDetailClick() {
        const serialNumber = $(this).data('serial-number');
        fetchRepairHistory([serialNumber]);
    }

    async function fetchRepairHistory(serialNumbers) {
        try {
            const ensuredStringSerialNumbers = serialNumbers.map(sn => String(sn));
            const data = await ApiService.searchHistory(ensuredStringSerialNumbers);
            DataTableManager.populateRepairHistoryTable(data.data);
            ModalManager.showModal('#repairHistoryModal');
        } catch (error) {
            showError("Lỗi khi lấy lịch sử sửa chữa!");
        }
    }

    function handleSaveRepairHistoryClick() {
        const serialNumber = String($(this).data('serial-number'));
        const currentRow = DataTableManager.getRowData(serialNumber);

        if (!currentRow) return;

        $('#save-serial-number').text(serialNumber);
        $('#save-notes').val('');
        ModalManager.showModal('#save-repair-modal');

        $('#confirm-save-repair').off('click').on('click', async () => {
            const notes = $('#save-notes').val().trim();
            if (!notes) {
                showWarning("Vui lòng nhập ghi chú!");
                return;
            }

            const payload = {
                serialNumbers: serialNumber,
                type: currentRow[8],
                status: currentRow[8],
                employeeId: $('#analysisPerson').val(),
                notes: notes,
                tag: "save"
            };

            try {
                const result = await ApiService.repairStatus(payload);
                const cleanMessage = result.message.replace(/"/g, '').trim();
                if (result.success && cleanMessage === "OK") {
                    showSuccess("Lưu lịch sử sửa chữa thành công!");
                    ModalManager.hideModal('#save-repair-modal');
                } else {
                    showError("Lỗi khi lưu lịch sử sửa chữa!");
                }
            } catch (error) {
                showError("Lỗi khi gọi API!");
            }
        });
    }

    return {
        handleRepairDetailClick,
        handleSaveRepairHistoryClick
    };
})();

// Module: TesterInfoManager - Quản lý thông tin Tester
const TesterInfoManager = (function () {
    function setupEventListeners() {
        $('#btn-fetch-tester').on('click', async () => {
            const serialNumbers = DataTableManager.getAllSerialNumbers();
            if (!serialNumbers.length) {
                showWarning("Không có dữ liệu trong database!");
                return;
            }

            try {
                const testerData = await ApiService.getLatestTester(serialNumbers);
                DataTableManager.populateTesterInfoTable(testerData);
                ModalManager.showModal('#modal-tester-info');
            } catch (error) {
                showError("Lỗi khi lấy thông tin tester!");
            }
        });
    }

    return {
        setup: setupEventListeners
    };
})();

// Module: HandoverManager - Quản lý giao nhận bản
const HandoverManager = (function () {
    function setupEventListeners() {
        $('#btn-handover').on('click', handleHandover);
        $('#btn-receive').on('click', handleReceive);
        $('#updateReceivingButton').on('click', handleManualReceive);
    }

    async function handleHandover() {
        const serialNumbers = DataTableManager.getAllSerialNumbers();
        if (!serialNumbers.length) {
            showWarning('Không có dữ liệu để giao!');
            return;
        }

        const payload = {
            serialNumbers: serialNumbers.join(','),
            handOverStatus: 'WAITING_HAND_OVER',
            tag: 'Giao'
        };

        try {
            const result = await ApiService.handOverStatus(payload);
            const clean = result.message.replace(/"/g, '').trim();
            if (result.success && clean === 'OK') {
                const updatedData = await ApiService.searchFA({ serialNumbers });
                DataTableManager.updateSnTable(serialNumbers, updatedData);
                showSuccess('Giao bản thành công!');
            } else {
                showError('Không thể giao bản!');
            }
        } catch (error) {
            showError('Lỗi khi gọi API!');
        }
    }

    function showLocationModal(callback) {
        $('#locationInput').val('');
        const modal = new bootstrap.Modal(document.getElementById('locationModal'));
        $('#confirmLocation').off('click').on('click', function () {
            const location = $('#locationInput').val().trim().toUpperCase();
            modal.hide();
            callback(location);
        });
        modal.show();
    }

    async function handleReceive() {
        const serialNumbers = DataTableManager.getAllSerialNumbers();
        if (!serialNumbers.length) {
            showWarning('Không có dữ liệu để nhận!');
            return;
        }

        showLocationModal(async (location) => {
            if (!location) {
                showWarning('Vui lòng nhập vị trí!');
                return;
            }

            const payload = {
                serialNumbers: serialNumbers.join(','),
                owner: $('#analysisPerson').val(),
                location,
                tag: 'Nhận'
            };

            try {
                const result = await ApiService.receivingStatus(payload);
                const clean = result.message.replace(/"/g, '').trim();
                if (result.success && clean === 'OK') {
                    const updatedData = await ApiService.searchFA({ serialNumbers });
                    DataTableManager.updateSnTable(serialNumbers, updatedData);
                    showSuccess('Nhận bản thành công!');
                } else {
                    showError('Không thể nhận bản!');
                }
            } catch (error) {
            showError('Lỗi khi gọi API!');
        }
    });
    }

    function handleManualReceive() {
        $('#updateReceivingSerials').val('');
        $('#updateReceivingLocation').val('');
        const modal = new bootstrap.Modal(document.getElementById('updateReceivingModal'));
        $('#confirmUpdateReceiving').off('click').on('click', async function () {
            const serialInput = $('#updateReceivingSerials').val().trim();
            const location = $('#updateReceivingLocation').val().trim().toUpperCase();
            if (!serialInput) {
                showWarning('Vui lòng nhập Serial Number!');
                return;
            }
            if (!location) {
                showWarning('Vui lòng nhập vị trí!');
                return;
            }
            const serialNumbers = serialInput.split(/[\n,]+/).map(sn => sn.trim()).filter(sn => sn);
            if (serialNumbers.length === 0) {
                showWarning('Danh sách Serial Number không hợp lệ!');
                return;
            }
            const payload = {
                serialNumbers: serialNumbers.join(','),
                owner: $('#analysisPerson').val(),
                location,
                tag: 'Nhận'
            };
            try {
                const result = await ApiService.receivingStatus(payload);
                const clean = result.message.replace(/"/g, '').trim();
                if (result.success && clean === 'OK') {
                    const updatedData = await ApiService.searchFA({ serialNumbers });
                    DataTableManager.updateSnTable(serialNumbers, updatedData);
                    showSuccess('Cập nhật vị trí thành công!');
                } else {
                    showError('Không thể cập nhật vị trí!');
                }
            } catch (error) {
                showError('Lỗi khi gọi API!');
            }
            modal.hide();
        });
        modal.show();
    }

    return {
        setup: setupEventListeners
    };
})();

// Module: ChartManager - handle charts for location and handover status
const ChartManager = (function () {
    let locationChart, handoverChart, onlineChart, agingChart;
    let locationDetails = [];
    let agingDetails = {};
    let currentStatusData = [];
    let currentLocationData = [];
    let currentAgingData = [];


    async function drawLocationChart() {
        const container = document.getElementById('locationChart');
        if (!container) return;
        const result = await ApiService.getLocationCounts();
        if (!result || !result.locations) return;

        locationDetails = result.locations;
        const labels = result.locations.map(l => l.location);
        const counts = result.locations.map(l => l.totalCount);

        if (locationChart) locationChart.destroy();

        const options = {
            series: [{ name: 'So luon', data: counts }],
            chart: {
                height: 350,
                type: 'bar',
                events: {
                    dataPointSelection: function (event, chartContext, config) {
                        const idx = config.dataPointIndex;
                        const loc = labels[idx];
                        const detail = locationDetails.find(l => l.location === loc);
                        if (detail) {
                            showLocationDetailModal(loc, detail.details);
                        }
                    }
                }
            },
            title: { text: 'Số lượng SN trong RE', align: 'center' },
            plotOptions: {
                bar: {
                    borderRadius: 10,
                    columnWidth: '50%'
                }
            },
            dataLabels: { enabled: false },
            stroke: { width: 0 },
            grid: { row: { colors: ['#fff', '#f2f2f2'] } },
            xaxis: {
                categories: labels,
                labels: { rotate: -45 },
                tickPlacement: 'on'
            },
            yaxis: { title: { text: 'Số lượng' } },
            fill: {
                type: 'gradient',
                gradient: {
                    shade: 'light',
                    type: 'horizontal',
                    shadeIntensity: 0.25,
                    gradientToColors: undefined,
                    inverseColors: true,
                    opacityFrom: 0.85,
                    opacityTo: 0.85,
                    stops: [50, 0, 100]
                }
            }
        };

        locationChart = new ApexCharts(container, options);
        locationChart.render();
    }

    async function drawHandoverStatusChart() {

        const container = document.getElementById('handoverStatusChart');
        if (!container) return;

        const result = await ApiService.getStatusCounts('HANDOVER');
        if (!result || !result.success) return;

        const labels = result.data.map(d => d.status);
        const counts = result.data.map(d => d.count);

        if (handoverChart) handoverChart.destroy();

        const options = {
            series: [{ name: 'Số lượng', data: counts }],
            chart: {
                height: 350,
                type: 'bar',
                events: {
                    dataPointSelection: function (event, chartContext, config) {
                        const idx = config.dataPointIndex;
                        const status = labels[idx];
                        handleStatusChartClick(status, 'handover');
                    }
                }
            },
            title: { text: 'Số lượng SN Giao/Nhận', align: 'center' },
            plotOptions: {
                bar: {
                    borderRadius: 10,
                    columnWidth: '50%'
                }
            },
            dataLabels: { enabled: false },
            stroke: { width: 0 },
            grid: { row: { colors: ['#fff', '#f2f2f2'] } },
            xaxis: {
                categories: labels,
                labels: { rotate: -45 },
                tickPlacement: 'on'
            },
            yaxis: { title: { text: 'Số lượng' } },
            fill: {
                type: 'gradient',
                gradient: {
                    shade: 'light',
                    type: 'horizontal',
                    shadeIntensity: 0.25,
                    gradientToColors: undefined,
                    inverseColors: true,
                    opacityFrom: 0.85,
                    opacityTo: 0.85,
                    stops: [50, 0, 100]
                }
            }
        };

        handoverChart = new ApexCharts(container, options);
        handoverChart.render();
    }

    async function drawOnlineStatusChart() {
        const container = document.getElementById('onlineStatusChart');
        if (!container) return;

        const result = await ApiService.getStatusCounts('ONLINE');
        if (!result || !result.success) return;

        const labels = result.data.map(d => d.status);
        const counts = result.data.map(d => d.count);

        if (onlineChart) onlineChart.destroy();

        const options = {
            series: [{ name: 'Số lượng', data: counts }],
            chart: {
                height: 350,
                type: 'bar',
                events: {
                    dataPointSelection: function (event, chartContext, config) {
                        const idx = config.dataPointIndex;
                        const status = labels[idx];
                        handleStatusChartClick(status, 'online');
                    }
                }
            },
            title: { text: 'Số lượng SN Online', align: 'center' },
            plotOptions: {
                bar: {
                    borderRadius: 10,
                    columnWidth: '50%'
                }
            },
            dataLabels: { enabled: false },
            stroke: { width: 0 },
            grid: { row: { colors: ['#fff', '#f2f2f2'] } },
            xaxis: {
                categories: labels,
                labels: { rotate: -45 },
                tickPlacement: 'on'
            },
            yaxis: { title: { text: 'Số lượng' } },
            fill: {
                type: 'gradient',
                gradient: {
                    shade: 'light',
                    type: 'horizontal',
                    shadeIntensity: 0.25,
                    gradientToColors: undefined,
                    inverseColors: true,
                    opacityFrom: 0.85,
                    opacityTo: 0.85,
                    stops: [50, 0, 100]
                }
            }
        };

        onlineChart = new ApexCharts(container, options);
        onlineChart.render();
    }

    async function drawAgingChart() {
        const container = document.getElementById('agingChart');
        if (!container) return;

        const result = await ApiService.getAgingCounts();
        if (!result || !result.success) return;

        agingDetails = result.details || {};

        const series = [result.data.lessThanOneDay, result.data.oneToThreeDays, result.data.moreThanThreeDays];
        const labels = ['< 1 ngày', '1-3 ngày', '> 3 ngày'];

        if (agingChart) agingChart.destroy();

        const options = {
            series: series,
            chart: {
                height: 350,
                type: 'pie',
                events: {
                    dataPointSelection: function (event, chartContext, config) {
                        const idx = config.dataPointIndex;
                        const key = idx === 0 ? 'lessThanOneDay' : idx === 1 ? 'oneToThreeDays' : 'moreThanThreeDays';
                        currentAgingData = agingDetails[key] || [];
                        showAgingDetailModal(labels[idx], currentAgingData);
                    }
                }
            },
            labels: labels,
            legend: { position: 'bottom' }
        };

        agingChart = new ApexCharts(container, options);
        agingChart.render();
    }

    async function init() {
        await drawLocationChart();
        await drawHandoverStatusChart();
        await drawOnlineStatusChart();
        await drawAgingChart();
    }

    async function handleStatusChartClick(selectedStatus, chartType) {
        const payload = {
            serialNumbers: [],
            modelName: "",
            testCode: "",
            status: selectedStatus,
            data1: "",
            handoverStatus: ""
        };
        try {
            const result = await ApiService.searchFA(payload);
            if (result.success) {
                const filteredData = chartType === 'online'
                    ? result.data.filter(item => item.datA18 !== null && item.datA18 !== 'TRONG_KHO' && item.datA13 !== 'WAITING_HAND_OVER')
                    : result.data;
                currentStatusData = filteredData;
                updateStatusModalTable(filteredData);
                const modal = document.getElementById('statusModal');
                if (modal) new bootstrap.Modal(modal).show();
            } else {
                showError(`Không thể tìm thấy dữ liệu cho trạng thái: ${selectedStatus}`);
            }
        } catch (error) {
            showError('Lỗi khi gọi API!');
        }
    }

    function truncate(text, maxLength) {
        return text && text.length > maxLength ? text.substring(0, maxLength) + '...' : (text || '');
    }

    function updateStatusModalTable(data) {
        const table = document.querySelector('#modal-sn-table');
        if (!table) return;

        if ($.fn.DataTable.isDataTable('#modal-sn-table')) {
            $('#modal-sn-table').DataTable().destroy();
        }

        const tbody = table.querySelector('tbody') || table.appendChild(document.createElement('tbody'));
        tbody.innerHTML = '';

        data.forEach(item => {
            const row = document.createElement('tr');
            row.innerHTML = `
                <td title="${item.seriaL_NUMBER || ''}">${truncate(item.seriaL_NUMBER, 20)}</td>
                <td title="${item.modeL_NAME || ''}">${truncate(item.modeL_NAME, 20)}</td>
                <td title="${item.tesT_GROUP || ''}">${truncate(item.tesT_GROUP, 20)}</td>
                <td title="${item.tesT_CODE || ''}">${truncate(item.tesT_CODE, 20)}</td>
                <td title="${item.datA1 || ''}">${truncate(item.datA1, 20)}</td>
                <td>${item.datA12 || ''}</td>
                <td title="${item.datA11 || ''}">${truncate(item.datA11, 20)}</td>
                <td>${item.tester || ''}</td>
                <td title="${item.datE3 || ''}">${truncate(item.datE3, 20)}</td>
                <td>${item.datA13 || ''}</td>
                <td>${item.datA18 || ''}</td>`;
            tbody.appendChild(row);
        });

        $('#modal-sn-table').DataTable({
            destroy: true,
            paging: true,
            searching: true,
            ordering: true,
            fixedColumns: { leftColumns: 1 },
            language: {
                search: 'Tìm kiếm:',
                lengthMenu: 'Hiển thị _MENU_ dòng',
                info: 'Hiển thị _START_ đến _END_ của _TOTAL_ dòng',
                paginate: { first: 'Đầu', last: 'Cuối', next: 'Tiếp', previous: 'Trước' }
            }
        });
    }

    function showLocationDetailModal(location, details) {
        const tbody = document.querySelector('#locationDetailTable tbody');
        if (!tbody) return;
        tbody.innerHTML = '';
        currentLocationData = details || [];
        (details || []).forEach(d => {
            const tr = document.createElement('tr');
            tr.innerHTML = `
                <td>${d.serialNumber || d.SerialNumber || ''}</td>
                <td>${d.testCode || d.TestCode || ''}</td>
                <td>${d.errorDesc || d.ErrorDesc || ''}</td>
                <td>${d.moNumber || d.MONumber || ''}</td>
                <td>${d.modelName || d.ModelName || ''}</td>
                <td>${d.aging ?? d.Aging ?? ''}</td>`;
            tbody.appendChild(tr);
        });
        const title = document.querySelector('#locationDetailModal .modal-title');
        if (title) title.textContent = `SerialNumber tại vị trí ${location}`;
        const modalEl = document.getElementById('locationDetailModal');
        if (modalEl) new bootstrap.Modal(modalEl).show();
    }

    function showAgingDetailModal(label, details) {
        const tbody = document.querySelector('#agingDetailTable tbody');
        if (!tbody) return;
        tbody.innerHTML = '';
        (details || []).forEach(d => {
            const tr = document.createElement('tr');
            tr.innerHTML = `
                <td>${d.serialNumber || d.SerialNumber || ''}</td>
                <td>${d.testCode || d.TestCode || ''}</td>
                <td>${d.errorDesc || d.ErrorDesc || ''}</td>
                <td>${d.moNumber || d.MONumber || ''}</td>
                <td>${d.modelName || d.ModelName || ''}</td>
                <td>${d.aging ?? d.Aging ?? ''}</td>
                <td>${d.location ?? d.Location ?? ''}</td>`;
            tbody.appendChild(tr);
        });
        const title = document.querySelector('#agingDetailModal .modal-title');
        if (title) title.textContent = `SerialNumber nhóm ${label}`;
        const modalEl = document.getElementById('agingDetailModal');
        if (modalEl) new bootstrap.Modal(modalEl).show();
    }
    function getAllAgingData() {
        return Object.values(agingDetails).flat();
    }
    return {
        init,
        getCurrentLocationData: () => currentLocationData,
        getCurrentAgingData: () => currentAgingData,
        getAllAgingData
    };
})();

// Module: BackToTop - Quản lý nút Back to Top
const BackToTop = (function () {
    function setup() {
        const backToTop = document.querySelector('.back-to-top');
        if (backToTop) {
            const toggleBackToTop = () => {
                if (window.scrollY > 100) {
                    backToTop.classList.add('active');
                } else {
                    backToTop.classList.remove('active');
                }
            };

            window.addEventListener('load', toggleBackToTop);
            window.addEventListener('scroll', toggleBackToTop);

            backToTop.addEventListener('click', (e) => {
                e.preventDefault();
                window.scrollTo({ top: 0, behavior: 'smooth' });
            });
        }
    }

    return {
        setup
    };
})();

// Khởi tạo tất cả các module
$(document).ready(function () {
    DataTableManager.initSnTable();
    DataTableManager.initRepairHistoryTable();
    DataTableManager.initTesterInfoTable();
    FormHandler.setup();
    GuideManager.setup();
    StatusManager.setup();
    TesterInfoManager.setup();
    HandoverManager.setup();
    ChartManager.init();
    const exportSnTableBtn = document.getElementById('exportSnTableExcelBtn');
    if (exportSnTableBtn) {
        exportSnTableBtn.addEventListener('click', async () => {
            showSpinner();
            const table = $('#sn-table').DataTable();
            const allData = table.rows().data().toArray();
            if (!allData.length) {
                showError('No data!');
                hideSpinner();
                return;
            }
            const headers = ['SERIAL_NUMBER', 'PRODUCT_LINE', 'MODEL_NAME', 'WIP', 'TEST_GROUP', 'TEST_CODE', 'ERROR_DESC', 'PRE_STATUS', 'STATUS', 'DATE', 'ID_NV', 'CHECK_POINT', 'HANDOVER', 'VỊ_TRÍ', 'SỬA_CHỮA'];
            const rowPromises = allData.map(async row => {
                const serial = $('<div>').html(row[0]).text().trim();
                let repairInfo = '';
                try {
                    const result = await ApiService.getRepairInfo(serial);
                    repairInfo = result.data || '';
                } catch (error) {
                    console.error(`Lỗi lấy thông tin sửa chữa của ${serial}:`, error);
                }
                const baseRow = row.slice(0, 14).map(cell => $('<div>').html(cell).text());
                baseRow.push(repairInfo);
                return baseRow;
            });
            const rows = await Promise.all(rowPromises);
            const workbook = XLSX.utils.book_new();
            const worksheet = XLSX.utils.aoa_to_sheet([headers, ...rows]);
            XLSX.utils.book_append_sheet(workbook, worksheet, 'SNTable');
            XLSX.writeFile(workbook, 'sn-table.xlsx');
            hideSpinner();
        });
    }
    document.getElementById('exportExcelBtn').addEventListener('click', () => {
        showSpinner();
        const table = $('#modal-sn-table').DataTable();
        const allData = table.rows().data().toArray();
        if (!allData.length) {
            showError('No data!');
            hideSpinner();
            return;
        }

        const headers = ['Serial Number', 'ModelName', 'TestGroup', 'TestCode', 'Data1', 'Status', 'Date', 'ID_Owner', 'TimeConfirm', 'HandoverStatus', 'Location'];
        const rows = allData.map(row => headers.map((_, i) => row[i]));
        const workbook = XLSX.utils.book_new();
        const worksheet = XLSX.utils.aoa_to_sheet([headers, ...rows]);
        XLSX.utils.book_append_sheet(workbook, worksheet, 'DanhSachSN');
        XLSX.writeFile(workbook, 'listSN.xlsx');
        hideSpinner();
    });

    const exportLocationBtn = document.getElementById('exportLocationExcelBtn');
    if (exportLocationBtn) {
        exportLocationBtn.addEventListener('click', () => {
            showSpinner();
            const data = ChartManager.getCurrentLocationData();
            if (!data.length) {
                showError('No data!');
                hideSpinner();
                return;
            }
            const headers = ['SerialNumber', 'TestCode', 'ErrorDesc', 'MO Number', 'ModelName', 'Aging'];
            const rows = data.map(d => [
                d.serialNumber || d.SerialNumber || '',
                d.testCode || d.TestCode || '',
                d.errorDesc || d.ErrorDesc || '',
                d.moNumber || d.MONumber || '',
                d.modelName || d.ModelName || '',
                d.aging ?? d.Aging ?? ''
            ]);
            const workbook = XLSX.utils.book_new();
            const worksheet = XLSX.utils.aoa_to_sheet([headers, ...rows]);
            XLSX.utils.book_append_sheet(workbook, worksheet, 'LocationSN');
            XLSX.writeFile(workbook, 'locationSN.xlsx');
            hideSpinner();
        });
    }

    const exportAgingBtn = document.getElementById('exportAgingExcelBtn');
    if (exportAgingBtn) {
        exportAgingBtn.addEventListener('click', () => {
            showSpinner();
            const data = ChartManager.getCurrentAgingData();
            if (!data.length) {
                showError('No data!');
                hideSpinner();
                return;
            }
            const headers = ['SerialNumber', 'TestCode', 'ErrorDesc', 'MO Number', 'ModelName', 'Aging'];
            const rows = data.map(d => [
                d.serialNumber || d.SerialNumber || '',
                d.testCode || d.TestCode || '',
                d.errorDesc || d.ErrorDesc || '',
                d.moNumber || d.MONumber || '',
                d.modelName || d.ModelName || '',
                d.aging ?? d.Aging ?? ''
            ]);
            const workbook = XLSX.utils.book_new();
            const worksheet = XLSX.utils.aoa_to_sheet([headers, ...rows]);
            XLSX.utils.book_append_sheet(workbook, worksheet, 'AgingSN');
            XLSX.writeFile(workbook, 'agingSN.xlsx');
            hideSpinner();
        });
    }

    const exportAllLocationBtn = document.getElementById('exportAllLocationExcelBtn');
    if (exportAllLocationBtn) {
        exportAllLocationBtn.addEventListener('click', async () => {
            showSpinner();
            let data = ChartManager.getAllAgingData();
            if (!data.length) {
                const result = await ApiService.getAgingCounts();
                if (result && result.details) {
                    data = Object.values(result.details).flat();
                }
            }
            if (!data.length) {
                showError('No data!');
                hideSpinner();
                return;
            }
            const headers = ['SERIAL_NUMBER', 'MODEL_NAME', 'MO_NUMBER', 'TEST_GROUP', 'TEST_CODE', 'ERROR_DESC', 'LOCATION', 'AGING'];
            const rows = data.map(d => [
                d.serialNumber || d.SerialNumber || '',
                d.modelName || d.ModelName || '',
                d.moNumber || d.MONumber || '',
                d.testGroup || d.TestGroup || '',
                d.testCode || d.TestCode || '',
                d.errorDesc || d.ErrorDesc || '',
                d.location || d.data18 || d.DATA18 || '',
                d.aging ?? d.Aging ?? ''
            ]);
            const workbook = XLSX.utils.book_new();
            const worksheet = XLSX.utils.aoa_to_sheet([headers, ...rows]);
            XLSX.utils.book_append_sheet(workbook, worksheet, 'AllLocationSN');
            XLSX.writeFile(workbook, 'allLocationSN.xlsx');
            hideSpinner();
        });
    }
    BackToTop.setup();
});