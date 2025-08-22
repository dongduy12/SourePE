document.addEventListener('DOMContentLoaded', () => {
    // Set endDate thành thời gian hiện tại (múi giờ +07:00)
    const endDateInput = document.getElementById('endDate');
    const now = new Date();
    const offset = 7 * 60; // +07:00
    const localDate = new Date(now.getTime() + offset * 60 * 1000);
    const formattedDate = localDate.toISOString().slice(0, 16);
    endDateInput.value = formattedDate;

    // Danh sách trạng thái cố định
    //const statuses = ["Scrap", "WaitingLink", "CheckIn", "Repair", "Online", "WaitingApproveScrap", "CheckOut"];
    const statuses = ["Scrap", "WaitingLink", "CheckIn", "Repair", "Online", "WaitingKanBanIn", "WaitingApproveScrap", "CheckOut"];
    // Khởi tạo các biến
    let statusDonutChart, sumMaterialsTable;

    // Hàm gọi API cho số lượng trạng thái
    async function fetchStatusCounts() {

        showSpinner();
        const startDate = document.getElementById('startDate').value.replace('T', ' ').replace(/-/g, '/').slice(0, 16);
        const endDate = document.getElementById('endDate').value.replace('T', ' ').replace(/-/g, '/').slice(0, 16);

        const payload = {
            statuses: statuses,
            startDate: startDate,
            endDate: endDate
        };
        console.log('fetchStatusCounts payload:', payload); // Log payload

        try {
            const response = await axios.post('http://10.220.130.119:9090/api/Bonepile2/status-count', payload);
            const { count, statusCounts } = response.data;
            console.log('fetchStatusCounts response:', { count, statusCounts });
            updateStatusCounts(count, statusCounts);
        } catch (error) {
            console.error('Error fetching status counts:', error);
            alert('Lỗi khi lấy số lượng trạng thái: ' + error.message);
        } finally {
            hideSpinner();
        }
    }

    // Hàm gọi API cho dữ liệu chi tiết
    async function fetchData(selectedStatus = null) {
        if (typeof Chart === 'undefined') {
            console.error('Chart.js không được tải.');
            alert('Không thể tải Chart.js.');
            return;
        }
        if (typeof axios === 'undefined') {
            console.error('Axios không được tải.');
            alert('Không thể tải Axios.');
            return;
        }
        if (typeof $ === 'undefined') {
            console.error('jQuery không được tải.');
            alert('Không thể tải jQuery.');
            return;
        }
        if (typeof $.fn.DataTable === 'undefined') {
            console.error('DataTables không được tải.');
            alert('Không thể tải DataTables.');
            return;
        }

        showSpinner();

        // Đảm bảo Statuses không bao giờ rỗng
        let statusesToFetch;
        if (selectedStatus && typeof selectedStatus === 'string' && selectedStatus.length > 0) {
            statusesToFetch = [selectedStatus];
        } else {
            statusesToFetch = statuses;
        }
        const startDate = document.getElementById('startDate').value.replace('T', ' ').replace(/-/g, '/').slice(0, 16);
        const endDate = document.getElementById('endDate').value.replace('T', ' ').replace(/-/g, '/').slice(0, 16);

        const payload = {
            statuses: statusesToFetch,
            startDate: startDate,
            endDate: endDate
        };
        console.log('fetchData payload:', payload); // Log payload
        console.log('selectedStatus:', selectedStatus); // Log giá trị selectedStatus

        try {
            const response = await axios.post('http://10.220.130.119:9090/api/Bonepile2/data', payload);
            const { count, data } = response.data;
            console.log('fetchData response:', { count, data });
            updateDashboard(count, data);
        } catch (error) {
            console.error('Error fetching data:', error);
            alert('Lỗi khi lấy dữ liệu: ' + error.message);
        } finally {
            //document.getElementById('loading').style.display = 'none';
            hideSpinner();
        }
    }

    // Hàm cập nhật số lượng trạng thái
    function updateStatusCounts(count, statusCounts) {
        document.getElementById('totalCount').textContent = count || 'N/A';
        document.getElementById('waitingScrapCount').textContent = statusCounts.WaitingApproveScrap || 0;
        document.getElementById('repairCount').textContent = statusCounts.Repair || 0;
        document.getElementById('onlinePd').textContent = statusCounts.Online || 0;
        document.getElementById('kanbanIn').textContent = statusCounts.WaitingKanBanIn || 0;
        document.getElementById('waitingLink').textContent = statusCounts.WaitingLink || 0;
        document.getElementById('waitingOut').textContent = statusCounts.CheckOut || 0;
        document.getElementById('waitingIn').textContent = statusCounts.CheckIn || 0;

        const statusLabels = Object.keys(statusCounts);
        const statusData = Object.values(statusCounts);
        if (statusDonutChart) statusDonutChart.destroy();
        statusDonutChart = new Chart(document.getElementById('statusDonutChart'), {
            type: 'doughnut',
            data: {
                labels: statusLabels,
                datasets: [{
                    data: statusData,
                    //backgroundColor: ['#dc3545', '#ffc107', '#28a745', '#17a2b8', '#6610f2', '#fd7e14', '#6c757d']
                    backgroundColor: ['#ff8307', '#05b529', '#ffc107', '#75b507', '#17a2b8', '#17b86d', '#dc3545', '#fa0f0f']
                }]
            },
            options: {
                responsive: true,
                plugins: {
                    legend: { position: 'top' },
                    tooltip: {
                        callbacks: {
                            label: ctx => `${ctx.label}: ${ctx.raw} (${((ctx.raw / count) * 100).toFixed(1)}%)`
                        }
                    },
                    datalabels: {
                        formatter: (value, ctx) => {
                            const total = statusData.reduce((sum, d) => sum + d, 0);
                            const percentage = ((value / total) * 100).toFixed(1);
                            return `${percentage}%`;
                        },
                        color: '#000', // Màu chữ
                        font: { weight: 'bold', size: 12 }
                    }
                }
            },
            plugins: [ChartDataLabels]
        });
    }

    // Hàm cập nhật dashboard
    function updateDashboard(count, data) {
        // Cập nhật sumMaterialsTable
        if (!sumMaterialsTable) {
            sumMaterialsTable = $('#sumMaterialsTable').DataTable({
                data: data,
                columns: [
                    { data: 'sn' },
                    { data: 'productLine' },
                    { data: 'modelName' },
                    { data: 'moNumber' },
                    { data: 'wipGroup' },
                    { data: 'failStation' },
                    { data: 'symptom' },
                    { data: 'errorDescription' },
                    { data: 'time' },
                    { data: 'flag' },
                    { data: 'poNo' },
                    { data: 'poItem' },
                    { data: 'failureAging', render: data => data ? data.toFixed(1) + ' ngày' : 'N/A' },
                    { data: 'versionCode' },
                    { data: 'workFlag' },
                    { data: 'errorFlag' },
                    { data: 'moNew' },
                    { data: 'status' },
                    { data: 'checkinRepairTime' },
                    { data: 'checkoutRepairTime' },
                    { data: 'category' }
                ],
                pageLength: 10,
                responsive: true,
                scrollCollapse: true,
                scrollX: true,
                order: [[12, 'desc']], // Sắp xếp theo FAIL_AGING
                dom: '<"top d-flex align-items-center"<"status-filter">Blf>rt<"bottom"ip><"clear">',
                buttons: [
                    {
                        extend: 'excelHtml5',
                        text: '<img src="/assets/img/excel.png"  class="excel-icon excel-button"/>',
                        filename: function () {
                            const now = new Date();
                            const offset = 7 * 60; // +07:00
                            const localDate = new Date(now.getTime() + offset * 60 * 1000);
                            const dateStr = localDate.toISOString().slice(0, 10).replace(/-/g, '');
                            const timeStr = localDate.toTimeString().slice(0, 8).replace(/:/g, '');
                            return `Bonepile2_${dateStr}_${timeStr}`;
                        },
                        exportOptions: {
                            columns: ':visible'
                        }
                    }
                ],
                language: {
                    search: "",
                    lengthMenu: "_MENU_",
                    info: "Hiển thị _START_ đến _END_ của _TOTAL_ bản ghi",
                    paginate: {
                        first: "Đầu",
                        last: "Cuối",
                        next: "Tiếp",
                        previous: "Trước"
                    }
                },
                // Thêm statusFilter vào vị trí "status-filter"
                initComplete: function () {
                    const statusFilter = document.getElementById('statusFilter');
                    if (statusFilter) {
                        const statusFilterWrapper = this.api().table().container().querySelector('.status-filter');
                        if (statusFilterWrapper) {
                            statusFilterWrapper.appendChild(statusFilter);
                        }
                    }
                    // Set placeholder cho ô search
                    $('.dataTables_filter input[type="search"]').attr('placeholder', 'Tìm kiếm');
                }
            });
        } else {
            // Cập nhật dữ liệu mà không phá hủy DataTable
            sumMaterialsTable.clear(); // Xóa dữ liệu cũ
            sumMaterialsTable.rows.add(data); // Thêm dữ liệu mới
            sumMaterialsTable.draw(); // Vẽ lại bảng
        }
        
    }
    // Sự kiện áp dụng bộ lọc
    document.getElementById('applyFilters').addEventListener('click', () => {
        console.log('applyFilters clicked');
        fetchStatusCounts();
        fetchData();
    });

    // Sự kiện thay đổi select
    document.getElementById('statusFilter').addEventListener('change', () => {
        const statusFilter = document.getElementById('statusFilter').value;
        console.log('statusFilter changed:', statusFilter);
        fetchData(statusFilter || null);
    });

    // Tải dữ liệu lần đầu
    console.log('Initial page load');
    fetchStatusCounts();
    fetchData();
});