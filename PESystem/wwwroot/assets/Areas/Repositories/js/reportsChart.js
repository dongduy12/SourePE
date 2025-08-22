document.addEventListener("DOMContentLoaded", async function () {
    const chartContainer = document.getElementById("reportsChart");
    const chartFilter = document.getElementById("chart-filter");
    let reportsChart = null;

    async function fetchChartData(filterType) {
        try {
            const now = new Date();
            const startDate = new Date(now.getFullYear(), now.getMonth(), now.getDate() - 6, 7, 30, 0);
            const endDate = new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1, 7, 30, 0);

            const response = await fetch("http://10.220.130.119:9090/api/product/report", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    startDate: startDate.toISOString(),
                    endDate: endDate.toISOString(),
                }),
            });

            const data = await response.json();
            if (!data.success) {
                console.error("Lỗi dữ liệu từ API:", data.message);
                return;
            }

            const importedData = data.report.importedData;
            const exportedData = data.report.exportedData;

            // ✅ Gộp dữ liệu theo ngày
            const aggregateDataByDay = (dataList) => {
                return dataList.reduce((acc, item) => {
                    const date = new Date(item.entryDate || item.exportDate);
                    const key = `${date.getFullYear()}-${(date.getMonth() + 1).toString().padStart(2, "0")}-${date.getDate().toString().padStart(2, "0")}`;
                    acc[key] = (acc[key] || 0) + 1;
                    return acc;
                }, {});
            };

            // ✅ Gộp dữ liệu theo ca làm việc
            const aggregateDataByShift = (dataList, shiftType) => {
                return dataList.reduce((acc, item) => {
                    const date = new Date(item.entryDate || item.exportDate);
                    const hours = date.getHours();
                    const minutes = date.getMinutes();
                    const totalMinutes = hours * 60 + minutes;

                    if (
                        (shiftType === "shift1" && totalMinutes >= 450 && totalMinutes < 1170) || // 7:30 AM - 7:30 PM
                        (shiftType === "shift2" && (totalMinutes >= 1170 || totalMinutes < 450)) // 7:30 PM - 7:30 AM
                    ) {
                        const key = `${date.getFullYear()}-${(date.getMonth() + 1).toString().padStart(2, "0")}-${date.getDate().toString().padStart(2, "0")}`;
                        acc[key] = (acc[key] || 0) + 1;
                    }
                    return acc;
                }, {});
            };

            let datasetSource;
            let xAxisType = "category";
            let xAxisData;

            if (filterType === "day") {
                const importedPerDay = aggregateDataByDay(importedData);
                const exportedPerDay = aggregateDataByDay(exportedData);
                xAxisData = [...new Set([...Object.keys(importedPerDay), ...Object.keys(exportedPerDay)])].sort();

                datasetSource = xAxisData.map(date => ({
                    date,
                    Nhập: importedPerDay[date] || 0,
                    Xuất: exportedPerDay[date] || 0,
                }));
            } else if (filterType === "shift1" || filterType === "shift2") {
                const importedPerShift = aggregateDataByShift(importedData, filterType);
                const exportedPerShift = aggregateDataByShift(exportedData, filterType);
                xAxisData = [...new Set([...Object.keys(importedPerShift), ...Object.keys(exportedPerShift)])].sort();

                datasetSource = xAxisData.map(date => ({
                    date,
                    Nhập: importedPerShift[date] || 0,
                    Xuất: exportedPerShift[date] || 0,
                }));
            }

            const option = {
                legend: { bottom: 0 },
                tooltip: { trigger: "axis" },
                dataset: {
                    dimensions: ["date", "Nhập", "Xuất"],
                    source: datasetSource,
                },grid: {
                    left: "5%", // ✅ Giữ lề trái nhỏ để không bị cắt chữ
                    right: "5%",
                    bottom: "10%",
                    containLabel: true,
                },
                xAxis: {
                    type: "category",
                    data: xAxisData,
                    axisLabel: {
                        rotate: 0,
                        fontSize: 10,
                        color: "#000000",
                        formatter: function (value) {
                            return value.length > 6 ? value.substring(0, 6) + "..." : value; // ✅ Ẩn bớt ký tự nếu quá dài
                        }
                    }
                },
                yAxis: { name: "Số lượng" },
                series: [
                    {
                        type: "bar",
                        name: "Nhập",
                        color: "#2eca6a",
                        barWidth: "35%", // ✅ Giảm kích thước cột để tạo khoảng cách
                        barCategoryGap: "30%", // ✅ Tăng khoảng cách giữa các cột
                        itemStyle: { borderRadius: [5, 5, 5, 5] }, // ✅ Bo tròn góc trên
                        label: { show: true, position: "top", color: "#000", fontWeight: "bold" }
                    },
                    {
                        type: "bar",
                        name: "Xuất",
                        color: "#ff771d",
                        barWidth: "35%",
                        barCategoryGap: "30%", // ✅ Giữ khoảng cách đồng đều
                        itemStyle: { borderRadius: [5, 5, 5, 5] },
                        label: { show: true, position: "top", color: "#000", fontWeight: "bold" }
                    }
                ]
            };

            // ✅ Xóa biểu đồ cũ trước khi render lại
            if (reportsChart) {
                reportsChart.dispose();
            }

            reportsChart = echarts.init(chartContainer);
            reportsChart.setOption(option);

            // ✅ Lắng nghe sự kiện resize để tự động thay đổi kích thước biểu đồ
            window.addEventListener("resize", () => {
                reportsChart.resize();
            });

        } catch (error) {
            console.error("Lỗi khi tải dữ liệu biểu đồ:", error);
        }
    }

    // ✅ Gọi hàm vẽ biểu đồ khi trang load
    fetchChartData("day");

    // ✅ Cập nhật biểu đồ khi chọn chế độ hiển thị
    chartFilter.addEventListener("change", function () {
        fetchChartData(this.value);
    });
});
