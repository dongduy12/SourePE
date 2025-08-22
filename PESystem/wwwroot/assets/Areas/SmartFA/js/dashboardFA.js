// Biến toàn cục
let statusModalInstance = null;
let statusModalElement = null;
let cioModalInstance = null;
let cioModalElement = null;
let allModalData = []; // Lưu toàn bộ dữ liệu từ API
let cioModalData = []; // Lưu dữ liệu chi tiết Check In/Out

// Hàm cắt bớt chuỗi
function truncateText(text, maxLength) {
    return (text && typeof text === 'string' && text.length > maxLength)
        ? text.substring(0, maxLength) + "..."
        : text || "";
}

// Hàm cập nhật DataTable trong modal với lưu toàn bộ dữ liệu
async function updateModalSNTable(data) {
    try {
        const tableBody = document.querySelector("#modal-sn-table tbody");
        if (!tableBody) {
            console.error("Không tìm thấy tbody của bảng modal!");
            return false;
        }

        tableBody.innerHTML = "";

        // Xóa DataTable cũ nếu đã khởi tạo
        if ($.fn.DataTable.isDataTable('#modal-sn-table')) {
            $('#modal-sn-table').DataTable().clear().destroy();
        }

        // Kiểm tra dữ liệu
        if (!Array.isArray(data) || data.length === 0) {
            tableBody.innerHTML = "<tr><td colspan='18'>Không có dữ liệu</td></tr>";
            allModalData = []; // Xóa dữ liệu cũ nếu rỗng
            console.warn("Dữ liệu rỗng hoặc không phải mảng:", data);
            return false;
        }

        // Lưu toàn bộ dữ liệu
        allModalData = [...data];

        // Tạo các hàng cho bảng (chỉ render phần hiển thị)
        data.forEach(item => {
            const row = document.createElement("tr");
            row.innerHTML = `
                <td title="${item.seriaL_NUMBER || ''}">${truncateText(item.seriaL_NUMBER || '', 20)}</td>
                <td title="${item.productLine || ''}">${truncateText(item.productLine || '', 20)}</td>
                <td title="${item.modeL_NAME || ''}">${truncateText(item.modeL_NAME || '', 20)}</td>
                <td>${item.wiP_GROUP || ''}</td>
                <td title="${item.tesT_GROUP || ''}">${truncateText(item.tesT_GROUP || '', 20)}</td>
                <td title="${item.tesT_CODE || ''}">${truncateText(item.tesT_CODE || '', 20)}</td>
                <td title="${item.datA1 || ''}">${truncateText(item.datA1 || '', 20)}</td>
                <td title="${item.datA11 || ''}">${item.datA11 || ''}</td>
                <td>${item.tester || ''}</td>
                <td title="${item.datE3 || ''}">${truncateText(item.datE3 || '', 20)}</td>
                <td>${item.datA13 || ''}</td>
                <td>${item.datA18 || ''}</td>
                <td>${item.borrowStatus || ''}</td>`;
            tableBody.appendChild(row);
        });

        // Khởi tạo DataTable
        $('#modal-sn-table').DataTable({
            paging: true,
            searching: true,
            ordering: false,
            scrollX: true,
            autoWidth: false,
            destroy: true
        }).on('draw.dt', () => console.log("DataTable drawn successfully"))
            .on('error.dt', (e, settings, techNote, message) => console.error("DataTable error:", message));

        return true;
    } catch (error) {
        console.error("Lỗi khi cập nhật DataTable:", error);
        return false;
    }
}

// Hiển thị bảng chi tiết Check In/Out
async function showCioModal(data, title) {
    try {
        const tableBody = document.querySelector('#cio-modal-table tbody');
        if (!tableBody) {
            console.error('Không tìm thấy tbody của bảng CheckInOut!');
            return;
        }

        tableBody.innerHTML = '';

        cioModalData = Array.isArray(data) ? [...data] : [];

        if ($.fn.DataTable.isDataTable('#cio-modal-table')) {
            $('#cio-modal-table').DataTable().clear().destroy();
        }

        let locationMap = {};
        try {
            const serials = data.map(d => d.seriaL_NUMBER || d.SERIAL_NUMBER);
            const locRes = await fetch('http://10.220.130.119:9090/api/Search/GetLocations', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(serials)
            });
            if (locRes.ok) {
                const locJson = await locRes.json();
                if (locJson.success && locJson.data) {
                    locationMap = locJson.data;
                }
            }
        } catch (err) {
            console.error('Location API error:', err);
        }

        data.forEach(item => {
            const sn = item.seriaL_NUMBER || item.SERIAL_NUMBER;
            item.location = locationMap[sn] || null;
            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${sn || ''}</td>
                <td>${item.modeL_NAME || ''}</td>
                <td>${item.producT_LINE || ''}</td>
                <td>${item.location || ''}</td>
                <td>${item.p_SENDER || ''}</td>
                <td>${item.iN_DATETIME || ''}</td>
                <td>${item.ouT_DATETIME || ''}</td>
                <td>${item.repairer || ''}</td>
                <td>${item.statioN_NAME || ''}</td>
                <td>${item.erroR_CODE || ''}</td>
                <td>${item.erroR_DESC || ''}</td>
                <td>${item.checkiN_STATUS|| ''}</td>`;
            tableBody.appendChild(row);
        });

        $('#cio-modal-table').DataTable({
            paging: true,
            searching: true,
            ordering: false,
            scrollX: true,
            autoWidth: false,
            destroy: true
        });

        const titleEl = document.getElementById('cioModalLabel');
        if (titleEl) titleEl.textContent = title;
        if (cioModalInstance) cioModalInstance.show();
    } catch (error) {
        console.error('Lỗi hiển thị bảng CheckInOut:', error);
    }
}

// Hàm gọi API và vẽ biểu đồ trạng thái
async function loadStatusChart() {
    try {
        const response = await fetch("http://10.220.130.119:9090/api/SearchFA/get-status-counts", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify("ALL")
        });

        if (!response.ok) throw new Error(`API Error: ${response.status} ${response.statusText}`);

        const result = await response.json();
        console.log("Dữ liệu API (Status):", result);

        if (result.success && result.data && Array.isArray(result.data) && result.data.length > 0) {
            const categories = result.data.map(item => item.status);
            const seriesData = result.data.map(item => item.count);
            const lineData = seriesData.map(value => Number(((value / result.totalCount) * 100).toFixed(2)));

            if (!document.getElementById("statusChart")) {
                console.error("Lỗi: Không tìm thấy thẻ có ID 'statusChart'");
                return;
            }

            Highcharts.chart('statusChart', {
                chart: { zoomType: 'xy', height: null, backgroundColor: '#ffffff' },
                xAxis: {
                    categories: categories,
                    labels: {
                        style: { color: '#000000', fontSize: '10px' },
                        formatter: function () {
                            return this.value.length > 6 ? this.value.substring(0, 6) + '...' : this.value;
                        }
                    }
                },
                yAxis: [{
                    title: { text: 'Số lượng', style: { color: '#000000' } },
                    labels: { style: { color: '#000000' } },
                    tickInterval: 500
                }, {
                    title: { text: 'Tỷ lệ (%)', style: { color: '#000000' } },
                    labels: { style: { color: '#000000' }, format: '{value}%' },
                    opposite: true,
                    min: 0,
                    max: 100,
                    tickInterval: 10
                }],
                tooltip: {
                    shared: true,
                    backgroundColor: 'rgba(0, 0, 0, 0.8)',
                    style: { color: '#FFFFFF' },
                    formatter: function () {
                        let s = `<b>${this.x}</b><br/>`;
                        this.points.forEach(point => {
                            s += `<span style="color:${point.color}">\u25CF</span> ${point.series.name}: <b>${point.y}${point.series.name === 'Tỷ lệ' ? '%' : ''}</b><br/>`;
                        });
                        return s;
                    }
                },
                legend: { enabled: false },
                plotOptions: {
                    column: {
                        borderRadius: 5,
                        pointWidth: 25,
                        groupPadding: 0.1,
                        dataLabels: {
                            enabled: true,
                            style: { color: '#000000', fontWeight: 'bold' },
                            formatter: function () { return Highcharts.numberFormat(this.y, 0); }
                        },
                        pointPadding: 0.1,
                        point: {
                            events: {
                                click: async function () {
                                    const selectedStatus = this.category;
                                    try {
                                        const payload = { serialNumbers: [], modelName: '', testCode: '', status: selectedStatus, data1: '', handoverStatus: '', location: '' };
                                        const searchResponse = await fetch("http://10.220.130.119:9090/api/SearchFA/search", {
                                            method: "POST",
                                            headers: { "Content-Type": "application/json" },
                                            body: JSON.stringify(payload)
                                        });

                                        if (!searchResponse.ok) throw new Error(`API Error: ${searchResponse.status} ${searchResponse.statusText}`);
                                        const searchResult = await searchResponse.json();

                                        if (searchResult.success && searchResult.data && Array.isArray(searchResult.data) && searchResult.data.length > 0) {
                                            const success = await updateModalSNTable(searchResult.data);
                                            if (success && statusModalInstance) statusModalInstance.show();
                                            else showError("Không thể cập nhật DataTable!");
                                        } else {
                                            showError(`No data for status: ${selectedStatus}`);
                                        }
                                    } catch (error) {
                                        console.error("Lỗi khi gọi API search:", error);
                                        showError("Lỗi khi gọi API search: " + (error.message || "Không xác định"));
                                    }
                                }
                            }
                        }
                    },
                    spline: {
                        dataLabels: {
                            enabled: true,
                            style: { color: '#000000', fontWeight: 'bold' },
                            formatter: function () { return `${Highcharts.numberFormat(this.y, 2)}%`; }
                        },
                        marker: { enabled: true, symbol: 'circle', radius: 4 },
                        lineWidth: 2,
                        color: '#00E5FF',
                        point: {
                            events: {
                                click: async function () {
                                    const selectedStatus = this.category;
                                    try {
                                        const payload = { serialNumbers: [], modelName: '', testCode: '', status: selectedStatus, data1: '', handoverStatus: '', location: '' };
                                        const searchResponse = await fetch("http://10.220.130.119:9090/api/SearchFA/search", {
                                            method: "POST",
                                            headers: { "Content-Type": "application/json" },
                                            body: JSON.stringify(payload)
                                        });

                                        if (!searchResponse.ok) throw new Error(`API Error: ${searchResponse.status} ${searchResponse.statusText}`);
                                        const searchResult = await searchResponse.json();

                                        if (searchResult.success && searchResult.data && Array.isArray(searchResult.data) && searchResult.data.length > 0) {
                                            const success = await updateModalSNTable(searchResult.data);
                                            if (success && statusModalInstance) statusModalInstance.show();
                                            else showError("Không thể cập nhật DataTable!");
                                        } else {
                                            showError(`No data for status: ${selectedStatus}`);
                                        }
                                    } catch (error) {
                                        console.error("Lỗi khi gọi API search:", error);
                                        showError("Lỗi khi gọi API search: " + (error.message || "Không xác định"));
                                    }
                                }
                            }
                        }
                    }
                },
                series: [{
                    name: "Số lượng",
                    type: 'column',
                    data: seriesData,
                    color: '#FF9800'
                }, {
                    name: "Tỷ lệ",
                    type: 'spline',
                    data: lineData,
                    yAxis: 1
                }]
            });
        } else {
            console.error("Dữ liệu API không hợp lệ hoặc rỗng!");
        }
    } catch (error) {
        console.error("Lỗi khi gọi API hoặc vẽ biểu đồ:", error);
    }
}

// Hàm gọi API và vẽ biểu đồ TOP MODEL
async function fetchChartData() {
    try {
        const response = await fetch("http://10.220.130.119:9090/api/DataChart/getCountModelName");
        if (!response.ok) throw new Error(`API Error: ${response.status} ${response.statusText}`);

        const data = await response.json();
        console.log("Dữ liệu API (Model):", data);

        if (!data || !data.models || !Array.isArray(data.models)) {
            console.error("API không trả về dữ liệu hợp lệ", data);
            return;
        }

        const categories = data.models.map(item => item.modelName);
        const seriesData = data.models.map(item => item.totalCount);
        const totalAllModels = data.totalAllModels || 1;
        const lineData = seriesData.map(value => parseFloat(((value / totalAllModels) * 100).toFixed(2)));

        Highcharts.chart('container', {
            chart: { zoomType: 'xy', height: null, backgroundColor: '#ffffff' },
            xAxis: {
                categories: categories,
                labels: {
                    style: { color: '#000000', fontSize: '10px' },
                    formatter: function () {
                        return this.value.length > 6 ? this.value.substring(0, 6) + '...' : this.value;
                    }
                }
            },
            yAxis: [{
                title: { text: 'Số lượng', style: { color: '#000000' } },
                labels: { style: { color: '#000000' } },
                tickInterval: 500
            }, {
                title: { text: 'Tỷ lệ (%)', style: { color: '#000000' } },
                labels: { style: { color: '#000000' }, format: '{value}%' },
                opposite: true,
                min: 0,
                max: 100,
                tickInterval: 10
            }],
            tooltip: { shared: true, backgroundColor: 'rgba(0, 0, 0, 0.8)', style: { color: '#FFFFFF' } },
            legend: { enabled: false },
            plotOptions: {
                column: {
                    borderRadius: 5,
                    pointWidth: 25,
                    dataLabels: { enabled: true, style: { color: '#000000' } },
                    pointPadding: 0.1,
                    groupPadding: 0.1,
                    borderRadiusBottomLeft: 10,
                    borderRadiusBottomRight: 10
                },
                spline: {
                    dataLabels: {
                        enabled: true,
                        style: { color: '#000000' },
                        formatter: function () { return this.y.toFixed(2) + "%"; }
                    },
                    marker: { enabled: true, symbol: 'circle', radius: 4 },
                    lineWidth: 2,
                    color: '#00E5FF'
                }
            },
            series: [{
                name: "Số lượng",
                type: 'column',
                data: seriesData,
                color: '#FF9800'
            }, {
                name: "Tỷ lệ (%)",
                type: 'spline',
                data: lineData,
                yAxis: 1
            }]
        });
    } catch (error) {
        console.error("Lỗi khi lấy dữ liệu:", error);
    }
}

// Hàm lấy dữ liệu Check In/Out và vẽ biểu đồ
async function loadCheckInOutChart() {
    const startInput = document.getElementById("cioStartDate");
    const endInput = document.getElementById("cioEndDate");
    if (!startInput || !endInput) {
        console.error("Không tìm thấy input ngày");
        return;
    }

    try {
        const url = new URL('http://10.220.130.119:9090/api/CheckInOut/GetCheckInOutBeforeKanban');
        url.searchParams.append('startDate', startInput.value);
        url.searchParams.append('endDate', endInput.value);
        const response = await fetch(url);

        if (!response.ok) throw new Error(`API Error: ${response.status} ${response.statusText}`);
        const result = await response.json();

        const checkInData = result?.checkIn?.data || [];
        const checkOutTon = result?.checkOut?.tonKhoCu?.data || [];
        const checkOutTrongNgay = result?.checkOut?.trongNgay?.data || [];
        const tonKhoTrongNgay = result?.tonKhoTrongNgay?.data || [];

        Highcharts.chart('checkInOutChart', {
            chart: { type: 'column', backgroundColor: '#ffffff' },
            title: { text: null },
            xAxis: { categories: ['Check In', 'Check Out tồn', 'Check Out trong ngày', 'Tồn kho trong ngày'] },
            yAxis: { title: { text: 'Số lượng' } },
            plotOptions: {
                column: {
                    cursor: 'pointer',
                    dataLabels: { enabled: true },
                    point: {
                        events: {
                            click: function () {
                                if (this.custom && this.custom.records) {
                                    showCioModal(this.custom.records, this.custom.title || '');
                                }
                            }
                        }
                    }
                }
            },
            series: [{
                name: 'Số lượng',
                data: [
                    { y: checkInData.length, color: '#00E5FF', custom: { records: checkInData, title: 'Danh sách Check In' } },
                    { y: checkOutTon.length, color: '#FF5722', custom: { records: checkOutTon, title: 'Check Out hàng tồn' } },
                    { y: checkOutTrongNgay.length, color: '#FF9800', custom: { records: checkOutTrongNgay, title: 'Check Out trong ngày' } },
                    { y: tonKhoTrongNgay.length, color: '#4CAF50', custom: { records: tonKhoTrongNgay, title: 'Tồn kho trong ngày' } }
                ]
            }]
        });
    } catch (error) {
        console.error("Lỗi khi lấy dữ liệu Check In/Out:", error);
    }
}

// Hàm khởi chạy khi DOM sẵn sàng
document.addEventListener("DOMContentLoaded", () => {
    // Khởi tạo modal
    statusModalElement = document.getElementById("statusModal");
    if (statusModalElement && !statusModalInstance) {
        statusModalInstance = new bootstrap.Modal(statusModalElement, { backdrop: true, keyboard: true });
        statusModalElement.addEventListener('shown.bs.modal', () => console.log("Modal shown"));
        statusModalElement.addEventListener('hidden.bs.modal', () => console.log("Modal closed"));
    }
    cioModalElement = document.getElementById('cioModal');
    if (cioModalElement && !cioModalInstance) {
        cioModalInstance = new bootstrap.Modal(cioModalElement, { backdrop: true, keyboard: true });
    }

    // Gọi API và vẽ biểu đồ
    loadStatusChart().catch(error => console.error("Error loading status chart:", error));
    fetchChartData().catch(error => console.error("Error loading model chart:", error));

    const now = new Date();
    const shift1Start = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 7, 30, 0);
    const shift1End = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 19, 30, 0);
    let start, end;
    if (now >= shift1Start && now < shift1End) {
        start = shift1Start;
        end = shift1End;
    } else if (now >= shift1End) {
        start = shift1End;
        end = new Date(shift1Start.getTime() + 24 * 60 * 60 * 1000);
    } else {
        start = new Date(shift1End.getTime() - 24 * 60 * 60 * 1000);
        end = shift1Start;
    }
    const format = d => d.toISOString().slice(0,16);
    const startInput = document.getElementById('cioStartDate');
    const endInput = document.getElementById('cioEndDate');
    if (startInput && endInput) {
        startInput.value = format(start);
        endInput.value = format(end);
        const loadBtn = document.getElementById('loadCioBtn');
        if (loadBtn) loadBtn.addEventListener('click', loadCheckInOutChart);
        loadCheckInOutChart();
    }

    // Gắn sự kiện xuất Excel
    const exportExcelBtn = document.getElementById("exportExcelBtn");
    if (exportExcelBtn) {
        exportExcelBtn.addEventListener("click", () => {
            if (allModalData.length === 0) {
                console.error("Không có dữ liệu để xuất Excel!");
                showError("Không có dữ liệu để xuất!");
                return;
            }

            const worksheetData = allModalData.map(item => ({
                "SERIAL_NUMBER": item.seriaL_NUMBER || "",
                "PRODUCT_LINE": item.productLine || "",
                "MODEL_NAME": item.modeL_NAME || "",
                "WIP_GROUP": item.wiP_GROUP || "",
                "TEST_GROUP": item.tesT_GROUP || "",
                "ERROR_CODE": item.tesT_CODE || "",
                "ERROR_DESC": item.datA1 || "",
                "STATUS_FA": item.datA11 || "",
                "PERSON_CONFIRM": item.tester || "",
                "TIME_CONFIRM": item.datE3 || "",
                "HANDLER": item.datA13 || "",
                "LOCATION": item.datA18 || "",
                "BORROW_STATUS": item.borrowStatus || ""
            }));

            const workbook = XLSX.utils.book_new();
            const worksheet = XLSX.utils.json_to_sheet(worksheetData);
            XLSX.utils.book_append_sheet(workbook, worksheet, "SerialNumbers");
            XLSX.writeFile(workbook, `SerialNumbers_${new Date().toISOString().slice(0, 10)}.xlsx`);
            console.log("Excel exported successfully with all data");
        });
    }

    const exportCioExcelBtn = document.getElementById("exportCioExcelBtn");
    if (exportCioExcelBtn) {
        exportCioExcelBtn.addEventListener("click", () => {
            if (cioModalData.length === 0) {
                console.error("Không có dữ liệu để xuất Excel!");
                showError("Không có dữ liệu để xuất!");
                return;
            }

            const worksheetData = cioModalData.map(item => ({
                "SERIAL_NUMBER": item.seriaL_NUMBER || "",
                "MODEL_NAME": item.modeL_NAME || "",
                "PRODUCT_LINE": item.producT_LINE || "",
                "LOCATION": item.location || "",
                "PERSON_IN": item.p_SENDER || "",
                "IN_DATE": item.iN_DATETIME || "",
                "OUT_DATE": item.ouT_DATETIME || "",
                "REPAIRER": item.repairer || "",
                "STATION": item.statioN_NAME || "",
                "ERROR_CODE": item.erroR_CODE || "",
                "ERROR_DESC": item.erroR_DESC || "",
                "TYPE": item.checkiN_STATUS || ""
            }));

            const workbook = XLSX.utils.book_new();
            const worksheet = XLSX.utils.json_to_sheet(worksheetData);
            XLSX.utils.book_append_sheet(workbook, worksheet, "CheckInOut");
            XLSX.writeFile(workbook, `CheckInOut_${new Date().toISOString().slice(0, 10)}.xlsx`);
            console.log("Excel exported successfully with Check In/Out data");
        });
    }
});