document.addEventListener("DOMContentLoaded", async () => {
    try {
        const response = await fetch("http://10.220.130.119:9090/api/Config/GetStation");
        const result = await response.json();

        if (result.success) {
            const data = result.data;

            const labels = data.map(item => item.station); // Extract 'station' field
            const counts = data.map(item => item.count);   // Extract 'count' field

            if (labels.length === 0 || counts.length === 0) {
                alert("Không có dữ liệu để hiển thị.");
                return;
            }

            const canvas = document.getElementById("statusChart");
            if (!canvas) {
                console.error("Không tìm thấy phần tử canvas với id='statusChart'.");
                return;
            }

            const ctx = canvas.getContext("2d");
            const chart = new Chart(ctx, {
                type: "bar",
                data: {
                    labels: labels,
                    datasets: [{
                        label: "Số lượng Station",
                        data: counts,
                        backgroundColor: "rgba(255, 255, 0, 0.8)",
                        borderColor: "rgba(255, 255, 0, 1)",
                        borderWidth: 1
                    }]
                },
                options: {
                    responsive: true,
                    plugins: {
                        legend: {
                            display: true,
                            position: "top",
                            labels: {
                                font: {
                                    size: 16,
                                    weight: "bold"
                                }
                            }
                        },
                        title: {
                            display: true,
                            text: "Thống Kê Số Lượng Station",
                            font: {
                                size: 20
                            }
                        },
                        tooltip: {
                            callbacks: {
                                label: (context) => `${context.raw}`
                            }
                        }
                    },
                    scales: {
                        x: {
                            title: {
                                display: true,
                                text: "Trạng thái",
                                font: {
                                    size: 16,
                                    weight: "bold"
                                }
                            },
                            ticks: {
                                font: {
                                    size: 14,
                                    weight: "bold"
                                }
                            }
                        },
                        y: {
                            beginAtZero: true,
                            title: {
                                display: true,
                                text: "Số lượng",
                                font: {
                                    size: 16,
                                    weight: "bold"
                                }
                            },
                            ticks: {
                                font: {
                                    size: 14,
                                    weight: "bold"
                                }
                            }
                        }
                    },
                    onClick: async (event, elements) => {
                        if (elements.length > 0) {
                            const index = elements[0].index;
                            const selectedStation = labels[index];

                            // Call the API with the selected station
                            try {
                                const snResponse = await fetch(`http://10.220.130.119:9090/api/Config/GetSNByStation`, {
                                    method: "POST",
                                    headers: {
                                        "Content-Type": "application/json"
                                    },
                                    body: JSON.stringify([selectedStation])
                                });
                                const snResult = await snResponse.json();

                                if (snResult.success) {
                                    // Update the table with Serial Numbers
                                    updateSNTable(snResult.data);

                                    // Show the modal with the updated table
                                    const snModal = new bootstrap.Modal(document.getElementById("statusModal"));
                                    snModal.show();
                                } else {
                                    alert("Không thể tìm thấy dữ liệu cho Station: " + selectedStation);
                                }
                            } catch (error) {
                                console.error("Lỗi khi gọi API lấy danh sách Serial Numbers:", error);
                            }
                        }
                    }
                },
                plugins: [{
                    id: "datalabels",
                    beforeDatasetsDraw: (chart) => {
                        const ctx = chart.ctx;
                        chart.data.datasets.forEach((dataset, i) => {
                            const meta = chart.getDatasetMeta(i);
                            meta.data.forEach((bar, index) => {
                                const value = dataset.data[index];
                                ctx.fillStyle = "black";
                                ctx.font = "14px Arial";
                                ctx.textAlign = "center";
                                ctx.fillText(value, bar.x, bar.y - 10);
                            });
                        });
                    }
                }]
            });
        } else {
            alert("Không thể lấy dữ liệu từ API: " + result.message);
        }
    } catch (error) {
        console.error("Lỗi khi gọi API hoặc vẽ biểu đồ:", error);
    }


    // Hàm cập nhật bảng Serial Numbers
    function updateSNTable(data) {
        // Hủy DataTables nếu đã được khởi tạo
        if ($.fn.DataTable.isDataTable('#modal-sn-table')) {
            $('#modal-sn-table').DataTable().destroy();
        }

        const tableBody = document.querySelector("#modal-sn-table tbody");
        if (!tableBody) {
            console.error("Không tìm thấy phần tử tbody trong bảng Serial Numbers.");
            return;
        }
        tableBody.innerHTML = ""; // Xóa dữ liệu cũ

        // Chèn dữ liệu vào bảng
        data.forEach(item => {
            const row = document.createElement("tr");
            row.innerHTML = `
            <td title="${item.serialNumber}">${truncateText(item.serialNumber, 20)}</td>
            <td title="${item.modelName}">${truncateText(item.modelName, 20)}</td>
            <td title="${item.wipGroup}">${truncateText(item.wipGroup, 20)}</td>
            <td title="${item.testCode}">${truncateText(item.testCode, 20)}</td>
            <td title="${item.data1}">${truncateText(item.data1, 20)}</td>
            <td title="${item.testTime}">${truncateText(item.testTime, 20)}</td>
            <td title="${item.testGroup}">${truncateText(item.testGroup, 20)}</td>
            <td title="${item.ddrToolFail}">${truncateText(item.ddrToolFail, 20)}</td>
            <td title="${item.customerInput}">${truncateText(item.customerInput, 20)}</td>
            <td title="${item.peInstruction}">${truncateText(item.peInstruction, 20)}</td>
            <td title="${item.reRepairInput}">${truncateText(item.reRepairInput, 20)}</td>
            <td title="${item.ftOffFail}">${truncateText(item.ftOffFail, 20)}</td>
            <td title="${item.hassFail}">${truncateText(item.hassFail, 20)}</td>
            <td title="${item.fthassFail}">${truncateText(item.fthassFail, 20)}</td>
            <td title="${item.previousStation}">${truncateText(item.previousStation, 20)}</td>
            <td title="${item.station}">${truncateText(item.station, 20)}</td>
        `;
            tableBody.appendChild(row);
        });

        // Khởi tạo DataTables
        $('#modal-sn-table').DataTable({
            destroy: true,
            paging: true,
            searching: true,
            ordering: true,
            language: {
                search: "Tìm kiếm:",
                lengthMenu: "Hiển thị _MENU_ dòng",
                info: "Hiển thị _START_ đến _END_ của _TOTAL_ dòng",
                paginate: {
                    first: "Đầu",
                    last: "Cuối",
                    next: "Tiếp",
                    previous: "Trước"
                }
            }
        });

        // Thêm nút xuất Excel
        const exportButton = document.getElementById("exportExcelBtn");
        if (exportButton) {
            exportButton.addEventListener("click", () => exportTableToExcel(data));
        }
    }

    // Hàm xuất bảng ra file Excel
    function exportTableToExcel(data) {
        const headers = ["SerialNumber", "ModelName", "WIPGroup", "TestCode", "Mô tả lỗi", "TestTime", "TestGroup", "DDR-TOOL", "NVInstruction", "PEInstruction", "RERepair", "FT-OFF", "HASS", "FT-HASS", "PreviousGroup", "GroupCurrent"];
        const rows = data.map(item => [
            item.serialNumber || "",
            item.modelName || "",
            item.wipGroup || "",
            item.testCode || "",
            item.data1 || "",
            item.testTime || "",
            item.testGroup || "",
            item.ddrToolFail || "",
            item.customerInput || "",
            item.peInstruction || "",
            item.reRepairInput || "",
            item.ftOffFail || "",
            item.hassFail || "",
            item.fthassFail || "",
            item.previousStation || "",
            item.station || ""
        ]);

        const worksheet = XLSX.utils.aoa_to_sheet([headers, ...rows]);
        const workbook = XLSX.utils.book_new();
        XLSX.utils.book_append_sheet(workbook, worksheet, "Serial Numbers");

        XLSX.writeFile(workbook, "dpuTool.xlsx");
    }

    // Hàm xử lý sự kiện cho nút đồng bộ
    const syncButton = document.getElementById("syncButton");
    if (syncButton) {
        syncButton.addEventListener("click", async () => {
            try {
                const syncResponse = await fetch("http://10.220.130.119:9090/api/Config/SyncSerialNumbersFromOracle", {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json"
                    }
                });

                const syncResult = await syncResponse.json();

                if (syncResult.success) {
                    alert(syncResult.message);
                    // Làm mới dữ liệu sau khi đồng bộ
                    location.reload();
                } else {
                    alert("Lỗi: " + syncResult.message);
                }
            } catch (error) {
                console.error("Lỗi khi đồng bộ dữ liệu:", error);
                alert("Đã xảy ra lỗi khi đồng bộ dữ liệu.");
            }
        });
    } else {
        console.error("Không tìm thấy nút đồng bộ.");
    }

    // Hàm cắt bớt chuỗi và thêm dấu "..." nếu chuỗi dài hơn giới hạn
    function truncateText(text, maxLength) {
        if (text && text.length > maxLength) {
            return text.substring(0, maxLength) + "...";
        }
        return text || "";
    }
});
