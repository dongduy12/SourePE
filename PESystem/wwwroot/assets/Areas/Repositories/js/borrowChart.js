// Chart hiển thị số lượng mượn/trả và aging

document.addEventListener("DOMContentLoaded", async function () {
    const borrowReturnEl = document.getElementById("borrow-return-chart");
    const borrowAgingEl = document.getElementById("borrow-aging-chart");

    const modalEl = document.getElementById("borrowDetailModal");
    const modal = modalEl ? new bootstrap.Modal(modalEl) : null;
    let currentDetails = [];

    async function showDetails(url) {
        if (!modal) return;
        try {
            const res = await fetch(url);
            const json = await res.json();
            if (json.success) {
                currentDetails = json.data || [];
                const tbody = document.querySelector('#borrow-detail-table tbody');
                if ($.fn.DataTable.isDataTable('#borrow-detail-table')) {
                    $('#borrow-detail-table').DataTable().clear().destroy();
                }
                tbody.innerHTML = '';
                currentDetails.forEach(item => {
                    const row = `<tr>
                        <td>${item.serialNumber || ''}</td>
                        <td>${item.borrower || ''}</td>
                        <td>${item.borrowDate ? new Date(item.borrowDate).toLocaleString() : ''}</td>
                        <td>${item.returnDate ? new Date(item.returnDate).toLocaleString() : ''}</td>
                        <td>${item.location || ''}</td>
                    </tr>`;
                    tbody.insertAdjacentHTML('beforeend', row);
                });
                $('#borrow-detail-table').DataTable({
                    dom: 'Bfrtip',
                    buttons: [
                        {
                            extend: 'excelHtml5',
                            text: '<img src="/assets/img/excel.png" class="excel-icon"/>',
                            className: 'excel-button',
                            title: '',
                            filename: function () {
                                const d = new Date();
                                return `BorrowDetails_${d.toISOString().slice(0,10)}`;
                            }
                        }
                    ]
                });
                modal.show();
            }
        } catch (err) {
            console.error('detail fetch error', err);
        }
    }

    if (borrowReturnEl) {
        try {
            const res = await fetch("http://10.220.130.119:9090/api/product/borrowed/daily");
            const json = await res.json();
            if (json.success) {
                const chart = echarts.init(borrowReturnEl);
                chart.setOption({
                    tooltip: { trigger: 'axis' },
                    xAxis: { type: 'category', data: ['Borrowed', 'Returned'] },
                    yAxis: { type: 'value' },
                    series: [{
                        type: 'bar',
                        data: [json.borrowedToday, json.returnedToday],
                        itemStyle: { color: '#2eca6a' }
                    }]
                });
                chart.on('click', function (params) {
                    if (params.name === 'Borrowed') {
                        showDetails('http://10.220.130.119:9090/api/product/borrowed/daily/details?type=borrowed');
                    } else if (params.name === 'Returned') {
                        showDetails('http://10.220.130.119:9090/api/product/borrowed/daily/details?type=returned');
                    }
                });
                window.addEventListener('resize', () => chart.resize());
            }
        } catch (err) {
            console.error('Borrow/Return chart error', err);
        }
    }

    if (borrowAgingEl) {
        try {
            const res = await fetch("http://10.220.130.119:9090/api/product/borrowed/aging");
            const json = await res.json();
            if (json.success) {
                const labels = json.aging.map(a => a.days);
                const values = json.aging.map(a => a.count);
                const chart = echarts.init(borrowAgingEl);
                chart.setOption({
                    tooltip: { trigger: 'axis' },
                    xAxis: { type: 'category', data: labels },
                    yAxis: { type: 'value' },
                    series: [{ type: 'line', data: values, smooth: true, color: '#ff771d' }]
                });
                chart.on('click', function (params) {
                    showDetails(`http://10.220.130.119:9090/api/product/borrowed/aging/details?days=${params.name}`);
                });
                window.addEventListener('resize', () => chart.resize());
            }
        } catch (err) {
            console.error('Aging chart error', err);
        }
    }
});
