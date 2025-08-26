// Bonepile Summary (Before + After) – rewritten with concurrency & normalization
document.addEventListener('DOMContentLoaded', async function () {
    // ===== API endpoints =====
    const apiBase = 'http://10.220.130.119:9090/api/Bonepile2';
    const beforeCountUrl = `${apiBase}/adapter-repair-status-count`;
    const beforeDetailUrl = `${apiBase}/adapter-repair-records`;

    const afterCountUrl = `${apiBase}/bonepile-after-kanban-count`;
    const afterDetailUrl = `${apiBase}/bonepile-after-kanban-basic`;
    const afterTestInfoUrl = `${apiBase}/bonepile-after-kanban-testinfo`;

    const locationUrl = 'http://10.220.130.119:9090/api/Search/GetLocations';

    // ===== Status lists to request =====
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
        'WaitingApproveBGA',
        'RepairInRE',
        'WaitingLink',
        'Linked'
    ];

    // ===== Status alias (normalize to one UI label) =====
    const statusAliasMap = {
        // Scrap
        'ScrapHasTask': 'Scrap Has Scrap',
        'ScrapHasScrap': 'Scrap Has Scrap',
        'Scrap Has Scrap': 'Scrap Has Scrap',

        'ScrapLackTask': 'Scrap Lacks Task',
        'Scrap Lacks Scrap': 'Scrap Lacks Task',
        'Scrap Lacks Task': 'Scrap Lacks Task',

        // Approve/Waiting
        'WatitingScrap': 'Waiting SPE approve scrap',
        'Waiting SPE approve scrap': 'Waiting SPE approve scrap',

        'ApproveBGA': 'SPE approve to BGA',
        'SPE approve to BGA': 'SPE approve to BGA',

        'WaitingApproveBGA': 'Waiting approve BGA',
        'Waiting approve BGA': 'Waiting approve BGA',

        // Repair/Checkout/PD
        'RepairInRE': 'Under repair in RE',
        'Under repair in RE': 'Under repair in RE',
        'Under repair in PD': 'Under repair in PD',
        'Waiting Check Out': 'Waiting Check Out',

        // Link/Online
        'Online': 'Online',
        'WaitingLink': 'Waiting Link',
        'Waiting Link': 'Waiting Link',
        'Linked': 'Linked'
    };
    const normalizeStatus = (s) => statusAliasMap[s] || s;

    // ===== Colors =====
    const statusColorMap = {
        'Scrap Lacks Task': '#ffc107',
        'Scrap Has Scrap': '#05b529',
        'SPE approve to BGA': '#17b86d',
        'Waiting approve BGA': '#00bcd4',
        'Waiting SPE approve scrap': '#dc3545',
        'Rework FG': '#6c757d',
        'Under repair in RE': '#ff8307',
        'Under repair in PD': '#fe8307',
        'Waiting Check Out': '#fe8307',
        'Online': '#28a745',
        'Waiting Link': '#17a2b8',
        'Linked': '#28a745'
    };

    // ===== Utils =====
    function chunk(arr, size) {
        const out = [];
        for (let i = 0; i < arr.length; i += size) out.push(arr.slice(i, i + size));
        return out;
    }
    function uniq(arr) {
        return Array.from(new Set(arr));
    }
    function nowLocalStringTZ7() {
        const now = new Date();
        const offset = 7 * 60; // +07:00
        const localDate = new Date(now.getTime() + offset * 60 * 1000);
        const dateStr = localDate.toISOString().slice(0, 10).replace(/-/g, '');
        const timeStr = localDate.toTimeString().slice(0, 8).replace(/:/g, '');
        return `${dateStr}_${timeStr}`;
    }

    // ===== Fetch AFTER testinfo in batches, return a map by SN =====
    async function fetchAfterTestInfoMap(serials, chunkSize = 800) {
        const out = {};
        if (!serials?.length) return out;

        const batches = chunk(uniq(serials), chunkSize).map(list =>
            axios.post(afterTestInfoUrl, { serialNumbers: list })
        );

        const settled = await Promise.allSettled(batches);
        for (const r of settled) {
            if (r.status !== 'fulfilled') {
                console.warn('A testinfo batch failed:', r.reason);
                continue;
            }
            const list = r.value?.data?.data || [];
            list.forEach(x => {
                const sn = x.seriaL_NUMBER || x.SERIAL_NUMBER || x.serial_number || '';
                if (!sn) return;
                out[sn] = {
                    sn,
                    testGroup: x.tesT_GROUP || x.TEST_GROUP || '',
                    testTime: x.tesT_TIME || x.TEST_TIME || '',
                    testCode: x.tesT_CODE || x.TEST_CODE || '',
                    errorDesc: x.erroR_DESC || x.ERROR_DESC || '',
                    aging: x.aging
                };
            });
        }
        return out;
    }

    // ===== Chart (donut) =====
    function renderDonut(statusCounts, total) {
        const labels = statusCounts.map(s => s.status);
        const dataVals = statusCounts.map(s => s.count);
        const percentages = dataVals.map(v => total > 0 ? ((v / total) * 100).toFixed(1) : 0);

        const donutEl = document.getElementById('statusDonutChart');
        if (!donutEl) return;

        const ctx = donutEl.getContext('2d');
        new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels,
                datasets: [{
                    data: dataVals,
                    backgroundColor: labels.map(l => statusColorMap[l] || '#ccc')
                }]
            },
            options: {
                responsive: true,
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: {
                            boxWidth: 20, boxHeight: 20,
                            generateLabels: chart => {
                                const d = chart.data;
                                return d.labels.map((label, i) => ({
                                    text: `${label} (${percentages[i]}%)`,
                                    fillStyle: d.datasets[0].backgroundColor[i],
                                    strokeStyle: d.datasets[0].backgroundColor[i],
                                    lineWidth: 1,
                                    hidden: isNaN(d.datasets[0].data[i]) || d.datasets[0].data[i] === 0,
                                    index: i
                                }));
                            }
                        },
                        maxWidth: 300,
                        align: 'center'
                    },
                    tooltip: {
                        callbacks: {
                            label: function (ctx) {
                                const label = ctx.label || '';
                                const value = ctx.raw || 0;
                                const percent = percentages[ctx.dataIndex];
                                return `${label}: ${value} (${percent}%)`;
                            }
                        }
                    }
                }
            }
        });
    }

    // ===== Dashboard (KPI + donut) – run BEFORE/AFTER count concurrently =====
    async function loadDashboardData() {
        try {
            showSpinner();
            const [beforeRes, afterRes] = await Promise.all([
                axios.get(beforeCountUrl),
                axios.get(afterCountUrl)
            ]);

            const beforeTotal = Number(beforeRes.data?.totalCount || 0);
            const afterTotal = Number(afterRes.data?.totalCount || 0);

            const statusCountsMap = {};

            (beforeRes.data?.statusCounts || []).forEach(s => {
                const k = normalizeStatus(s.status ?? s.Status);
                const c = Number(s.count ?? s.Count ?? 0);
                statusCountsMap[k] = (statusCountsMap[k] || 0) + c;
            });

            (afterRes.data?.statusCounts || []).forEach(s => {
                const k = normalizeStatus(s.status ?? s.Status);
                const c = Number(s.count ?? s.Count ?? 0);
                statusCountsMap[k] = (statusCountsMap[k] || 0) + c;
            });

            const statusCounts = Object.keys(statusCountsMap).map(k => ({ status: k, count: statusCountsMap[k] }));
            const total = beforeTotal + afterTotal;

            const totalEl = document.getElementById('totalCount');
            if (totalEl) totalEl.innerText = total;

            renderDonut(statusCounts, total);

            await loadTableData(); // then render table
        } catch (e) {
            console.error('Error loading dashboard', e);
            alert('Không thể tải dữ liệu dashboard.');
        } finally {
            hideSpinner();
        }
    }

    // ===== Table – run BEFORE detail + AFTER basic concurrently, then testinfo + locations concurrently =====
    let dataTable;
    async function loadTableData() {
        try {
            showSpinner();

            // 1) BEFORE + AFTER BASIC (song song)
            const [beforeRes, afterBasicRes] = await Promise.all([
                axios.post(beforeDetailUrl, { statuses: beforeStatuses }),
                axios.post(afterDetailUrl, { statuses: afterStatuses })
            ]);

            const beforeData = beforeRes.data?.data || [];
            const afterBasic = afterBasicRes.data?.data || [];

            // 2) SN list for AFTER testinfo + locations
            const afterSNs = uniq(afterBasic.map(a => a.sn || a.SN).filter(Boolean));

            // 3) testinfo + locations (song song, chịu lỗi cục bộ)
            const [tiSettled, locSettled] = await Promise.allSettled([
                fetchAfterTestInfoMap(afterSNs, 800),
                axios.post(locationUrl, afterSNs)
            ]);

            const testInfoMap = (tiSettled.status === 'fulfilled') ? tiSettled.value : {};
            const locationMap = (locSettled.status === 'fulfilled') ? (locSettled.value?.data?.data || {}) : {};

            // 4) Map BEFORE rows
            const mappedBefore = beforeData.map(b => ({
                type: 'Before',
                sn: b.sn || b.SN,
                productLine: b.productLine || b.ProductLine,
                modelName: b.modelName || b.ModelName,
                moNumber: b.moNumber || b.MoNumber,
                wipGroup: b.wipGroup || b.WipGroup,
                wipGroupKANBAN: '',
                testGroup: b.testGroup || b.TestGroup || '',
                testCode: b.testCode || b.TestCode || '',
                testTime: b.testTime || b.TestTime || '',
                errorDesc: b.errorDesc || b.ErrorDesc || '',
                status: normalizeStatus(b.status || b.Status || ''),
                aging: b.agingDay || b.AgingDay || '',
                location: locationMap[b.sn || b.SN] || '',
                repair: b.repair || b.Repair || ''
            }));

            // 5) Map AFTER rows (+merge testinfo/location)
            const mappedAfter = afterBasic.map(a => {
                const sn = a.sn || a.SN;
                const info = testInfoMap[sn] || {};
                return {
                    type: 'After',
                    sn,
                    productLine: a.productLine || a.ProductLine,
                    modelName: a.modelName || a.ModelName,
                    moNumber: a.moNumber || a.MoNumber,
                    wipGroup: a.wipGroupSFC || a.WipGroupSFC || '',
                    wipGroupKANBAN: a.wipGroupKANBAN || a.WipGroupKANBAN || '',
                    testGroup: info.testGroup || '',
                    testCode: info.testCode || '',
                    testTime: info.testTime || '',
                    errorDesc: info.errorDesc || '',
                    status: normalizeStatus(a.status || a.Status || ''),
                    aging: info.aging ?? '',
                    location: locationMap[sn] || '',
                    repair: ''
                };
            });

            // 6) Combine & render DataTable
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
                    buttons: [{
                        extend: 'excelHtml5',
                        text: '<img src="/assets/img/excel.png" class="excel-icon excel-button"/>',
                        title: '',
                        filename: () => `Bonepile_summary_${nowLocalStringTZ7()}`,
                        exportOptions: {
                            columns: ':visible',
                            modifier: { selected: null },
                            format: { header: d => (d || '').trim() }
                        }
                    }],
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

    // ===== Kick off =====
    await loadDashboardData();
});
