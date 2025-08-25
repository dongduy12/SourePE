document.addEventListener('DOMContentLoaded', async function () {
    const apiBase = 'http://10.220.130.119:9090/api/Bonepile2';
    const beforeCountUrl = `${apiBase}/adapter-repair-status-count`;
    const beforeDetailUrl = `${apiBase}/adapter-repair-records`;
    const afterCountUrl = `${apiBase}/bonepile-after-kanban-count`;
    const afterDetailUrl = `${apiBase}/bonepile-after-kanban`;
    const locationUrl = 'http://10.220.130.119:9090/api/Search/GetLocations';

    const beforeStatuses = [
        'Scrap Lacks Task',
        'Scrap Has Scrap',
        'SPE approve to BGA',
        'Waiting SPE approve scrap',
        'Rework FG',
        'Under repair in RE',
        'Waiting Check Out',
        'Under repair in PD'
    ];

    const afterStatuses = [
        'ScrapHasTask',
        'ScrapLackTask',
        'WatitingScrap',
        'ApproveBGA',
        'RepairInRE',
        'WaitingLink',
        'Linked'
    ];

    const statusAliasMap = {
        'ScrapHasTask': 'Scrap Has Scrap',
        'ScrapHasScrap': 'Scrap Has Scrap',
        'Scrap Has Scrap': 'Scrap Has Scrap',
        'ScrapLackTask': 'Scrap Lacks Task',
        'Scrap Lacks Scrap': 'Scrap Lacks Task',
        'Scrap Lacks Task': 'Scrap Lacks Task',
        'WatitingScrap': 'Waiting SPE approve scrap',
        'Waiting SPE approve scrap': 'Waiting SPE approve scrap',
        'ApproveBGA': 'SPE approve to BGA',
        'SPE approve to BGA': 'SPE approve to BGA',
        'RepairInRE': 'Under repair in RE',
        'Under repair in RE': 'Under repair in RE',
        'Under repair in PD': 'Under repair in PD',
        'Online': 'Online',
        'WaitingLink': 'Waiting Link',
        'Waiting Link': 'Waiting Link',
        'Linked': 'Linked'
    };

    const normalizeStatus = status => statusAliasMap[status] || status;

    const statusColorMap = {
        'Scrap Lacks Task': '#ffc107',
        'Scrap Has Scrap': '#05b529',
        'SPE approve to BGA': '#17b86d',
        'Waiting SPE approve scrap': '#dc3545',
        'Rework FG': '#6c757d',
        'Under repair in RE': '#ff8307',
        'Waiting Check Out': '#fe8307',
        'Online': '#28a745',
        'Waiting Link': '#17a2b8',
        'Linked': '#28a745'
    };

    let dataTable;

    async function loadDashboardData() {
        try {
            const beforeRes = await axios.get(beforeCountUrl);
            const afterRes = await axios.get(afterCountUrl);
            const statusCountsMap = {};
            const beforeTotal = beforeRes.data.totalCount || 0;
            const afterTotal = afterRes.data.totalCount || 0;

            (beforeRes.data.statusCounts || []).forEach(s => {
                const key = normalizeStatus(s.status);
                statusCountsMap[key] = (statusCountsMap[key] || 0) + s.count;
            });
            (afterRes.data.statusCounts || []).forEach(s => {
                const key = normalizeStatus(s.status);
                statusCountsMap[key] = (statusCountsMap[key] || 0) + s.count;
            });

            const statusCounts = Object.keys(statusCountsMap).map(k => ({ status: k, count: statusCountsMap[k] }));
            const total = beforeTotal + afterTotal;
            document.getElementById('totalCount').innerText = total;
            const percentages = statusCounts.map(s => total > 0 ? ((s.count / total) * 100).toFixed(1) : 0);
            const donutCtx = document.getElementById('statusDonutChart').getContext('2d');
            new Chart(donutCtx, {
                type: 'doughnut',
                data: {
                    labels: statusCounts.map(s => s.status),
                    datasets: [{
                        data: statusCounts.map(s => s.count),
                        backgroundColor: statusCounts.map(s => statusColorMap[s.status] || '#ccc'),
                    }]
                },
                options: {
                    responsive: true,
                    plugins: {
                        legend: {
                            position: 'bottom',
                            labels: {
                                boxWidth: 20,
                                boxHeight: 20,
                                generateLabels: chart => {
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
                            align: 'center'
                        },
                        tooltip: {
                            callbacks: {
                                label: function (context) {
                                    const label = context.label || '';
                                    const value = context.raw || 0;
                                    const percent = percentages[context.dataIndex];
                                    return `${label}: ${value} (${percent}%)`;
                                }
                            }
                        }
                    }
                }
            });
            await loadTableData();
        } catch (e) {
            console.error('Error loading dashboard', e);
            alert('Không thể tải dữ liệu dashboard.');
        }
    }

    async function loadTableData() {
        try {
            showSpinner();
            const beforeRes = await axios.post(beforeDetailUrl, { statuses: beforeStatuses });
            const afterRes = await axios.post(afterDetailUrl, { statuses: afterStatuses });
            const beforeData = beforeRes.data?.data || [];
            const afterData = afterRes.data?.data || [];

            // Chỉ lấy vị trí cho SERIAL_NUMBER(FG)
            const serials = Array.from(new Set(
                afterData.map(a => a.sn).filter(Boolean)
            ));

            let locationMap = {};
            try {
                const locRes = await axios.post(locationUrl, serials);
                locationMap = locRes.data?.data || {};
            } catch (err) {
                console.error('Error fetching location', err);
            }
            const mappedBefore = beforeData.map(b => ({
                type: 'Before',
                sn: b.sn,
                productLine: b.productLine,
                modelName: b.modelName,
                moNumber: b.moNumber,
                wipGroup: b.wipGroup,
                wipGroupKANBAN: '',
                testGroup: b.testGroup,
                testCode: b.testCode,
                testTime: b.testTime,
                errorDesc: b.errorDesc,
                status: normalizeStatus(b.status),
                aging: b.agingDay,
                location: locationMap[b.sn] || '',
                repair: b.repair || ''
            }));
            const mappedAfter = afterData.map(a => ({
                type: 'After',
                sn: a.sn,
                productLine: a.productLine,
                modelName: a.modelName,
                moNumber: a.moNumber,
                wipGroup: a.wipGroupSFC,
                wipGroupKANBAN: a.wipGroupKANBAN,
                testGroup: a.testGroup,
                testCode: a.testCode,
                testTime: a.testTime,
                errorDesc: a.errorDesc,
                status: normalizeStatus(a.status),
                aging: a.fgAging,
                location: locationMap[a.sn] || '',
                repair: ''
            }));
            const combined = mappedBefore.concat(mappedAfter);
            if (dataTable) {
                dataTable.clear().rows.add(combined).draw();
            } else {
                dataTable = $('#summaryTable').DataTable({
                    data: combined,
                    scrollX: true,
                    columns: [
                        { data: 'type' },
                        { data: 'sn' },
                        { data: 'productLine' },
                        { data: 'modelName' },
                        { data: 'moNumber' },
                        { data: 'wipGroup' },
                        { data: 'wipGroupKANBAN' },
                        { data: 'testGroup' },
                        { data: 'testCode' },
                        { data: 'testTime' },
                        { data: 'errorDesc' },
                        { data: 'status' },
                        { data: 'aging' },
                        { data: 'location' },
                        { data: 'repair' }
                    ],
                    dom: '<"top d-flex align-items-center"flB>rt<"bottom"ip>',
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
                                return `Bonepile_summary_${dateStr}_${timeStr}`;
                            },
                            exportOptions: {
                                columns: ':visible',
                                modifier: { selected: null },
                                format: { header: data => data.trim() }
                            }
                        }
                    ],
                    destroy: true,
                    language: {
                        search: '',
                        emptyTable: 'Không có dữ liệu để hiển thị',
                        zeroRecords: 'Không tìm thấy bản ghi phù hợp'
                    },
                    initComplete: function () {
                        $('.dataTables_filter input[type="search"]').attr('placeholder', 'Tìm kiếm');
                    }
                });
            }
        } catch (e) {
            console.error('Error loading table', e);
            alert('Không thể tải dữ liệu bảng.');
        } finally {
            hideSpinner();
        }
    }

    await loadDashboardData();
});
