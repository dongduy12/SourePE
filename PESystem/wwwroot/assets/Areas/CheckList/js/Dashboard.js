document.addEventListener("DOMContentLoaded", function () {
    fetch("http://10.220.130.119:9090/api/CheckList/get-checklist", {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify({ id: "ALL" })
    })
        .then(response => response.json())
        .then(data => {
            renderCharts(data);
        })
        .catch(error => console.error("Error fetching data:", error));
});

function renderCharts(data) {
    const statusCounts = { Open: 0, "On-going": 0, Monitor: 0, Close: 0 };
    const ownerStatusCounts = {};

    // Đếm số lượng status và phân bố theo owner
    data.forEach(issue => {
        const status = issue.status || "Unknown";
        const owner = issue.owner || "Unknown";

        statusCounts[status] = (statusCounts[status] || 0) + 1;

        if (!ownerStatusCounts[owner]) {
            ownerStatusCounts[owner] = { "Open": 0, "On-going": 0, "Monitor": 0, "Close": 0, "Total": 0 };
        }
        ownerStatusCounts[owner][status] = (ownerStatusCounts[owner][status] || 0) + 1;
        ownerStatusCounts[owner].Total += 1; // Tính tổng số lượng
    });

    // Tổng số issue
    const totalIssues = Object.values(statusCounts).reduce((acc, count) => acc + count, 0);

    // Pie Chart (Phân bố theo status)
    const pieChart = new Chart(document.getElementById("statusPieChart").getContext("2d"), {
        type: "pie",
        data: {
            labels: Object.keys(statusCounts),
            datasets: [{
                data: Object.values(statusCounts),
                backgroundColor: ["#e74c3c", "#f1c40f", "#5dade2", "#27ae60"],
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    position: 'bottom' // Đưa phần ghi chú xuống dưới
                },
                tooltip: {
                    callbacks: {
                        label: function (context) {
                            let label = context.label || '';
                            let value = context.parsed || 0;
                            let percentage = ((value / totalIssues) * 100).toFixed(2);
                            return `${label}: ${value} (${percentage}%)`;
                        }
                    }
                },
                datalabels: {
                    color: '#000',
                    font: {
                        weight: 'bold'
                    },
                    formatter: (value) => {
                        return value > 0 ? `${value}` : ''; // Chỉ hiển thị số nếu > 0
                    }
                }
            }
        },
        plugins: [ChartDataLabels] // Plugin để hiển thị số lượng trực tiếp trên biểu đồ
    });

    // Bar Chart (Số lượng theo owner và trạng thái)
    const owners = Object.keys(ownerStatusCounts);
    const statuses = ["Open", "On-going", "Monitor", "Close"];
    const datasets = statuses.map(status => ({
        label: status,
        data: owners.map(owner => ownerStatusCounts[owner][status] || 0),
        backgroundColor: getStatusColor(status),
    }));

    const barChart = new Chart(document.getElementById("ownerBarChart").getContext("2d"), {
        type: "bar",
        data: {
            labels: owners,
            datasets: datasets
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            scales: {
                x: { stacked: true },
                y: { stacked: true, beginAtZero: true }
            },
            plugins: {
                legend: {
                    position: 'bottom' // Đưa phần ghi chú xuống dưới
                },
                tooltip: {
                    callbacks: {
                        label: function (context) {
                            let value = context.raw;
                            return `${context.dataset.label}: ${value}`;
                        }
                    }
                },
                datalabels: {
                    anchor: 'center',
                    align: 'center',
                    color: '#000',
                    font: {
                        weight: 'bold'
                    },
                    /*formatter: (value, context) => {
                        const owner = context.chart.data.labels[context.dataIndex];
                        const total = ownerStatusCounts[owner].Total;
                        // Chỉ hiển thị tổng trên đỉnh cột khi đang ở cột cuối cùng (Close) và giá trị hiện tại là tổng
                        if (context.dataset.label === "Close" && context.dataIndex === context.chart.data.labels.indexOf(owner)) {
                            return total > 0 ? `${total}` : '';
                        }
                        // Hiển thị số lượng trạng thái chỉ khi giá trị > 0
                        return value > 0 ? `${value}` : '';
                    }*/

                    formatter: (value) => {
                        return value > 0 ? `${value}` : ''; // Chỉ hiển thị số nếu > 0
                    }
                }
            }
        },
        plugins: [ChartDataLabels]
    });
}


function getStatusColor(status) {
    switch (status) {
        case "Open": return "#e74c3c"; // Đỏ dịu
        case "On-going": return "#f1c40f"; // Vàng nhạt
        case "Monitor": return "#5dade2"; // Xanh da trời nhẹ
        case "Close": return "#27ae60"; // Xanh lá cây dịu
        default: return "#bdc3c7"; // Xám nhẹ
    }
}