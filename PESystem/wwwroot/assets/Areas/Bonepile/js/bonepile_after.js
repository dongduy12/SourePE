document.addEventListener("DOMContentLoaded", async function () {
    const apiBase = "http://10.220.130.119:9090/api/Bonepile2";
    const apiCountUrl = `${apiBase}/bonepile-after-kanban-count`;
    const apiAgingCountUrl = `${apiBase}/bonepile-after-kanban-aging-count`;
    const apiBasicUrl = `${apiBase}/bonepile-after-kanban-basic`;
    const apiTestInfoUrl = `${apiBase}/bonepile-after-kanban-testinfo`;
    const locationUrl = 'http://10.220.130.119:9090/api/Search/GetLocations';
    // Định nghĩa tất cả trạng thái hợp lệ
    const validStatuses = [
        "ScrapHasTask",
        "ScrapLackTask",
        "WatitingScrap",
        "ApproveBGA",
        "RepairInRE",
        "WaitingLink",
        "Linked",
    ];

    const statusColorMap = {
        "ScrapHasTask": "#05b529",
        "ScrapLackTask": "#ffc107",
        "WatitingScrap": "#dc3545",
        "RepairInRE": "#ff8307",
        "ApproveBGA": "#17b86d",
        "WaitingLink": "#17a2b8"
    };

    let dataTable;
    let modalTable;
    let agingData = [];

    // Tạo nội dung ô có tooltip
    function createTooltipCell(data) {
        return `<span class="tooltip-trigger" data-tooltip="${data || ''}">${data || ''}</span>`;
    }

    // Gắn sự kiện tooltip
    function attachTooltipEvents() {
        $(document).on('mouseover', '.tooltip-trigger', function (e) {
            const $this = $(this);
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
        }).on('mousemove', '.tooltip-trigger', function (e) {
            const tooltip = document.querySelector('.custom-tooltip');
            if (tooltip && tooltip.style.display === 'block') {
                tooltip.style.left = (e.pageX + 10) + 'px';
                tooltip.style.top = (e.pageY - 20) + 'px';
            }
        }).on('mouseout', '.tooltip-trigger', function () {
            const tooltip = document.querySelector('.custom-tooltip');
            if (tooltip) {
                tooltip.style.display = 'none';
            }
        });
    }

    // Load KPI + Donut chart
    async function loadDashboardData() {
        try {
            // Hiển thị spinner
            showSpinner();
            const [res, agingRes] = await Promise.all([
                axios.get(apiCountUrl),
                axios.get(apiAgingCountUrl)
            ]);
            const { totalCount, statusCounts } = res.data;
            const { agingCounts } = agingRes.data;
            agingData = agingCounts;

            // Gán KPI
            document.getElementById("totalCount").innerText = totalCount || 0;
            document.getElementById("noTaskscrapCount").innerText = statusCounts.find(s => s.status === "ScrapLackTask")?.count || 0;
            document.getElementById("scrapCount").innerText = statusCounts.find(s => s.status === "ScrapHasTask")?.count || 0;
            document.getElementById("waitingScrapCount").innerText = statusCounts.find(s => s.status === "WatitingScrap")?.count || 0;
            document.getElementById("repairInRE").innerText = statusCounts.find(s => s.status === "RepairInRE")?.count || 0;
            document.getElementById("waitingBGA").innerText = statusCounts.find(s => s.status === "ApproveBGA")?.count || 0;
            document.getElementById("waitingLink").innerText = statusCounts.find(s => s.status === "WaitingLink")?.count || 0;

            // Tính phần trăm cho biểu đồ
            const total = statusCounts.reduce((sum, s) => sum + s.count, 0);
            const percentages = statusCounts.map(s => total > 0 ? ((s.count / total) * 100).toFixed(1) : 0);

            // Vẽ Donut chart với phần trăm
            const donutCtx = document.getElementById("statusDonutChart").getContext("2d");
            new Chart(donutCtx, {
                type: "doughnut",
                data: {
                    labels: statusCounts.map(s => s.status),
                    datasets: [{
                        data: statusCounts.map(s => s.count),
                        backgroundColor: statusCounts.map(s => statusColorMap[s.status] || "#ccc"),
                    }]
                },
                options: {
                    responsive: true,
                    plugins: {
                        legend: {
                            position: "bottom",
                            labels: {
                                boxWidth: 20,
                                boxHeight: 20,
                                generateLabels: (chart) => {
                                    const data = chart.data;
                                    return data.labels.map((label, i) => ({
                                        text: `${label} (${percentages[i]}%)`,
                                        fillStyle: data.datasets[0].backgroundColor[i],
                                        strokeStyle: data.datasets[0].backgroundColor[i],
                                        lineWidth: 1,
                                        hidden: isNaN(data.datasets[0].data[i]) || data.datasets[0].data[i] === 0,
                                        index: i
                                    }));
                                }
                            },
                            maxWidth: 300,
                            align: "center"
                        },
                        tooltip: {
                            callbacks: {
                                label: function (context) {
                                    const label = context.label || '';
                                    const value = context.raw || 0;
                                    const percentage = percentages[context.dataIndex];
                                    return `${label}: ${value} (${percentage}%)`;
                                }
                            }
                        },
                        datalabels: {
                            formatter: (value, ctx) => {
                                const total = statusCounts.reduce((sum, d) => sum + d.count, 0);
                                const percentage = total > 0 ? ((value / total) * 100).toFixed(1) : 0;
                                return `${percentage}%`;
                            },
                            color: '#000', // Màu chữ
                            font: { weight: 'bold', size: 12 }
                        }

                    }
                },
                plugins: [ChartDataLabels]
            });

            // Tính phần trăm cho biểu đồ aging
            const agingTotal = agingCounts.reduce((sum, a) => sum + a.count, 0);
            const agingPercentages = agingCounts.map(a => agingTotal > 0 ? ((a.count / agingTotal) * 100).toFixed(1) : 0);

            // Vẽ biểu đồ Aging
            const agingCtx = document.getElementById("agingPieChart").getContext("2d");
            const agingChart = new Chart(agingCtx, {
                type: "pie",
                data: {
                    labels: agingCounts.map(a => a.ageRange),
                    datasets: [{
                        data: agingCounts.map(a => a.count),
                        backgroundColor: ["#4e73df", "#1cc88a", "#e74a3b"],
                    }]
                },
                options: {
                    responsive: true,
                    plugins: {
                        legend: {
                            position: "bottom",
                            labels: {
                                boxWidth: 20,
                                boxHeight: 20,
                                generateLabels: (chart) => {
                                    const data = chart.data;
                                    return data.labels.map((label, i) => ({
                                        text: `${label} (${agingPercentages[i]}%)`,
                                        fillStyle: data.datasets[0].backgroundColor[i],
                                        strokeStyle: data.datasets[0].backgroundColor[i],
                                        lineWidth: 1,
                                        hidden: isNaN(data.datasets[0].data[i]) || data.datasets[0].data[i] === 0,
                                        index: i
                                    }));
                                }
                            },
                            align: "center"
                        },
                        tooltip: {
                            callbacks: {
                                label: function (context) {
                                    const label = context.label || '';
                                    const value = context.raw || 0;
                                    const percentage = agingPercentages[context.dataIndex];
                                    return `${label}: ${value} (${percentage}%)`;
                                }
                            }
                        },
                        datalabels: {
                            formatter: (value, ctx) => {
                                const percentage = agingPercentages[ctx.dataIndex];
                                return `${percentage}%`;
                            },
                            color: '#000',
                            font: { weight: 'bold', size: 12 }
                        }
                    }
                },
                plugins: [ChartDataLabels]

            });

            agingChart.canvas.onclick = function (evt) {
                const points = agingChart.getElementsAtEventForMode(evt, 'nearest', { intersect: true }, true);
                if (points.length) {
                    const index = points[0].index;
                    const label = agingChart.data.labels[index];
                    const records = agingData.find(a => a.ageRange === label)?.records || [];
                    loadTableFromRecords(records);
                }
            };

            // Load dữ liệu bảng ban đầu (Tất cả trạng thái)
            await loadTableData(validStatuses);
        } catch (error) {
            console.error("Lỗi khi tải dashboard:", error);
            alert("Không thể tải dữ liệu dashboard. Vui lòng thử lại!");
        } finally {
            hideSpinner();
        }
    }

    // Load dữ liệu bảng dựa trên trạng thái
    async function loadTableData(statuses) {
        try {
            // Hiển thị spinner
            showSpinner();
            // Log payload để debug
            console.log("Sending payload:", { statuses });

            const payload = { statuses: statuses.length > 0 ? statuses : null };
            const basicRes = await axios.post(apiBasicUrl, payload);
            const tableData = basicRes.data?.data || [];

            const serials = Array.from(new Set(tableData.map(r => r.sn).filter(Boolean)));

            let testInfoData = [];
            if (serials.length) {
                try {
                    const testInfoPayload = { serialNumbers: serials };
                    const testInfoRes = await axios.post(apiTestInfoUrl, testInfoPayload);
                    testInfoData = (testInfoRes.data?.data || []).map(item => ({
                        sn: item.seriaL_NUMBER,                  // map về sn
                        testGroup: item.tesT_GROUP,
                        testTime: item.tesT_TIME,
                        testCode: item.tesT_CODE,
                        errorDesc: item.erroR_DESC,
                        aging: item.aging
                    }));
                } catch (err) {
                    console.error('Error fetching test info', err);
                }
            }

            // Hợp nhất dữ liệu test_code/aging theo sn
            const testInfoMap = {};
            testInfoData.forEach(item => { if (item.sn) testInfoMap[item.sn] = item; });
            tableData.forEach(row => {
                const info = testInfoMap[row.sn];
                if (info) {
                    row.testGroup = info.testGroup;
                    row.testTime = info.testTime;
                    row.testCode = info.testCode;
                    row.errorDesc = info.errorDesc;
                    row.aging = info.aging;
                }
            });

            let locationMap = {};
            if (serials.length) {
                try {
                    const [locRes] = await Promise.all([axios.post(locationUrl, serials)]);
                    locationMap = locRes.data?.data || {};
                } catch (err) {
                    console.error('Error fetching locations', err);
                }
            }
            tableData.forEach(r => { r.location = locationMap[r.sn] || ''; });

            if (dataTable) {
                dataTable.clear().rows.add(tableData).draw();
            } else {
                dataTable = $('#sumMaterialsTable').DataTable({
                    data: tableData,
                    scrollX: true,
                    ordering: true,
                    info: true,
                    autoWidth: false,
                    order: [[3, "desc"]],
                    columns: [
                        { data: "sn" },
                        { data: "productLine" },
                        { data: "modelName" },
                        { data: "moNumber" },
                        { data: "wipGroupSFC" },
                        { data: "wipGroupKANBAN" },
                        { data: "errorFlag" },
                        { data: "workFlag" },
                        { data: "testGroup" },
                        { data: "testTime" },
                        { data: "testCode" },
                        { data: "errorDesc" },
                        { data: "status" },
                        { data: "aging" },
                        { data: "location" },
                    ],
                    dom: '<"top d-flex align-items-center"flB>rt<"bottom"ip>',

                    buttons: [
                        {
                            extend: 'excelHtml5',
                            text: '<img src="/assets/img/excel.png" class="excel-icon excel-button"/>',
                            title: '', // Loại bỏ tiêu đề mặc định
                            filename: function () {
                                const now = new Date();
                                const offset = 7 * 60; // +07:00
                                const localDate = new Date(now.getTime() + offset * 60 * 1000);
                                const dateStr = localDate.toISOString().slice(0, 10).replace(/-/g, '');
                                const timeStr = localDate.toTimeString().slice(0, 8).replace(/:/g, '');
                                return `Bonepile_after_${dateStr}_${timeStr}`;
                            },
                            exportOptions: {
                                columns: ':visible',
                                modifier: {
                                    selected: null
                                },
                                format: {
                                    header: function (data, columnIdx) {
                                        // Loại bỏ khoảng trắng hoặc ký tự không mong muốn
                                        return data.trim();
                                    }
                                }
                            }
                        }
                    ],
                    destroy: true,
                    language: {
                        search: "",
                        emptyTable: "Không có dữ liệu để hiển thị",
                        zeroRecords: "Không tìm thấy bản ghi phù hợp"
                    },
                    initComplete: function () {
                        // Tạo select filter
                        var selectHtml = `
                                                                    <div class="form-group mb-0" style="min-width: 200px;">
                                                                        <select id="statusFilterDt" class="form-control">
                                                                            <option value="">Tất cả trạng thái</option>
                                                                            <option value="ScrapHasTask">Scrap Has Task</option>
                                                                            <option value="ScrapLackTask">Scrap Lacks Task</option>
                                                                            <option value="ApproveBGA">SPE Approve to BGA</option>
                                                                            <option value="WatitingScrap">Waiting SPE Approve Scrap</option>
                                                                            <option value="RepairInRE">Under Repair in RE</option>
                                                                            <option value="WaitingLink">Waiting Link</option>
                                                                            <option value="Linked">Waiting Link</option>
                                                                        </select>
                                                                    </div>
                                                                `;

                        // Chèn vào phần `top` bên trái (trước nút excel)
                        $('.dataTables_wrapper .top').prepend(selectHtml);

                        // Gắn sự kiện
                        $('#statusFilterDt').on('change', async function () {
                            const selectedStatus = this.value;
                            const statuses = selectedStatus ? [selectedStatus] : validStatuses;
                            await loadTableData(statuses);
                        });


                        // Set placeholder cho ô search
                        $('.dataTables_filter input[type="search"]').attr('placeholder', 'Tìm kiếm');
                    }
                });
            }
        } catch (error) {
            console.error("Lỗi khi tải dữ liệu bảng:", error);
            alert("Không thể tải dữ liệu bảng. Vui lòng thử lại!");
        } finally {
            //document.getElementById("spinner-overlay").style.display = "none";
            hideSpinner();
        }
    }

    async function loadTableFromRecords(records) {
        try {
            showSpinner();
            const serials = Array.from(new Set(records.map(r => r.sn).filter(Boolean)));
            let locationMap = {};
            try {
                const locRes = await axios.post(locationUrl, serials);
                locationMap = locRes.data?.data || {};
            } catch (err) {
                console.error('Error fetching locations for modal', err);
            }
            records.forEach(r => { r.location = locationMap[r.sn] || ''; });
            if (modalTable) {
                modalTable.clear().rows.add(records).draw();
                attachTooltipEvents();
            } else {
                modalTable = $('#recordsTable').DataTable({
                    data: records,
                    scrollX: true,
                    searching: true,
                    ordering: false,
                    info: true,
                    columns: [
                        { data: "sn" },
                        { data: "fg" },
                        { data: "productLine" },
                        { data: "modelName" },
                        { data: "moNumber" },
                        { data: "wipGroupSFC" },
                        { data: "wipGroupKANBAN" },
                        { data: "errorFlag" },
                        { data: "workFlag" },
                        { data: "testGroup" },
                        { data: "testTime" },
                        { data: "testCode" },
                        { data: "errorDesc" },
                        { data: "aging" },
                        { data: "location" }
                    ],
                    columnDefs: [
                        {
                            targets: '_all',
                            width: '120px',
                            render: function (data) { return createTooltipCell(data); }
                        }
                    ],
                    buttons: [
                        {
                            extend: 'excelHtml5',
                            text: '<img src="/assets/img/excel.png" class="excel-icon excel-button"/>',
                            title: '',
                            filename: function () {
                                const now = new Date();
                                const offset = 7 * 60;
                                const localDate = new Date(now.getTime() + offset * 60 * 1000);
                                const dateStr = localDate.toISOString().slice(0, 10).replace(/-/g, '');
                                const timeStr = localDate.toTimeString().slice(0, 8).replace(/:/g, '');
                                return `Bonepile_after_aging_${dateStr}_${timeStr}`;
                            },
                            exportOptions: { columns: ':visible' }
                        }
                    ],
                    destroy: true,
                    language: {
                        search: '',
                        emptyTable: 'Không có dữ liệu để hiển thị',
                        zeroRecords: 'Không tìm thấy bản ghi phù hợp'
                    }
                });
            }
            const modalEl = document.getElementById('recordsModal');
            const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
            modal.show();
            attachTooltipEvents();
        } finally {
            hideSpinner();
        }
    }

    // Khởi tạo dashboard
    await loadDashboardData();
});