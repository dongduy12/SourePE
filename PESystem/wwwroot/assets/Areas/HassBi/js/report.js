
//$(document).ready(function () {
//    let tableData = []; // Biến để lưu dữ liệu từ API

//    // Hiển thị spinner
//    function showSpinner() {
//        document.getElementById("spinner-overlay").style.display = "flex";
//    }

//    // Ẩn spinner
//    function hideSpinner() {
//        const spinner = document.getElementById('spinner-overlay');
//        if (spinner) {
//            spinner.style.display = 'none';
//            console.log("Spinner hidden");
//        }

//        const hasVisibleModal = document.querySelector('.modal.show');
//        if (!hasVisibleModal) {
//            const backdrop = document.querySelector('.modal-backdrop');
//            if (backdrop) {
//                backdrop.remove();
//                console.log("Backdrop removed (no visible modal)");
//            }
//        } else {
//            console.log("Modal still visible, skipping backdrop removal");
//        }
//    }

//    // Function to format date to datetime-local input format (YYYY-MM-DDThh:mm)
//    function formatDateToInput(date) {
//        return date.getFullYear() + '-' +
//            ('0' + (date.getMonth() + 1)).slice(-2) + '-' +
//            ('0' + date.getDate()).slice(-2) + 'T' +
//            ('0' + date.getHours()).slice(-2) + ':' +
//            ('0' + date.getMinutes()).slice(-2);
//    }

//    // Function to format date to API format (YYYY-MM-DD HH:mm:ss)
//    function formatDateToApi(date) {
//        return date.getFullYear() + '-' +
//            ('0' + (date.getMonth() + 1)).slice(-2) + '-' +
//            ('0' + date.getDate()).slice(-2) + ' ' +
//            ('0' + date.getHours()).slice(-2) + ':' +
//            ('0' + date.getMinutes()).slice(-2) + ':' +
//            ('0' + date.getSeconds()).slice(-2);
//    }

//    // Set default dates: start of today (00:00:00) to current time
//    let now = new Date();
//    let startOfDay = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 0, 0, 0);

//    $('#startDate').val(formatDateToInput(startOfDay));
//    $('#endDate').val(formatDateToInput(now));

//    // Initialize DataTable
//    let table = $('#hassBiTable').DataTable({
//        "paging": true,
//        "searching": true,
//        "ordering": true,
//        "info": true,
//        "autoWidth": false,
//        "columns": [
//            { "data": "moNumber" },
//            { "data": "modelName" },
//            {
//                "data": "inStationTime",
//                "render": function (data) {
//                    return data ? new Date(data).toLocaleString() : "N/A";
//                }
//            },
//            { "data": "targetQty" },
//            { "data": "skipHassBi" },
//            {
//                "data": "skipReason",
//                "render": function (data) {
//                    return data ? data : "N/A";
//                }
//            },
//            { "data": "type" }
//        ]
//    });

//    // Function to load data
//    function loadData() {
//        let startDate = $('#startDate').val();
//        let endDate = $('#endDate').val();

//        // Validate inputs
//        if (!startDate || !endDate) {
//            alert("Please select both start date and end date.");
//            return;
//        }

//        // Convert datetime-local format to "YYYY-MM-DD HH:mm:ss"
//        let startDateFormatted = startDate.replace("T", " ") + ":00";
//        let endDateFormatted = endDate.replace("T", " ") + ":00";

//        // Prepare data for POST request
//        let requestData = {
//            startDate: startDateFormatted,
//            endDate: endDateFormatted
//        };

//        // Show spinner before sending request
//        showSpinner();

//        // Send POST request to API
//        $.ajax({
//            url: 'http://10.220.130.119:9090/api/ReportSkipHass/GetMoData',
//            type: 'POST',
//            contentType: 'application/json',
//            data: JSON.stringify(requestData),
//            success: function (data) {
//                // Lưu dữ liệu để xuất Excel
//                tableData = data;
//                // Clear existing data and load new data into DataTable
//                table.clear().rows.add(data).draw();
//                // Hide spinner after success
//                hideSpinner();
//            },
//            error: function (xhr, status, error) {
//                let errorMessage = xhr.responseJSON ? xhr.responseJSON.message : "An error occurred while fetching data.";
//                alert(errorMessage);
//                // Hide spinner after error
//                hideSpinner();
//            }
//        });
//    }

//    // Load data automatically when the page loads
//    loadData();

//    // Handle Load Data button click
//    $('#loadDataBtn').click(function () {
//        loadData();
//    });

//    // Handle Export Excel button click
//    $('#exportExcelBtn').click(function () {
//        if (tableData.length === 0) {
//            alert("No data available to export.");
//            return;
//        }

//        // Chuẩn bị dữ liệu cho Excel
//        const exportData = tableData.map(item => ({
//            MO_NUMBER: item.moNumber,
//            MODEL_NAME: item.modelName,
//            IN_STATION_TIME: item.inStationTime ? new Date(item.inStationTime).toLocaleString() : "N/A",
//            TARGET_QTY: item.targetQty,
//            BI_HASS: item.biHass,
//            SKIP: item.skipReason || "N/A",
//            TYPE: item.type
//        }));

//        // Tạo worksheet từ dữ liệu
//        const worksheet = XLSX.utils.json_to_sheet(exportData);
//        // Tạo workbook và thêm worksheet
//        const workbook = XLSX.utils.book_new();
//        XLSX.utils.book_append_sheet(workbook, worksheet, "Hass BI Report");
//        // Xuất file Excel
//        XLSX.writeFile(workbook, "HASSBI_Report.xlsx");
//    });
//});


$(document).ready(function () {
    let tableData = []; // Biến để lưu dữ liệu từ API

    // Hiển thị spinner
    function showSpinner() {
        document.getElementById("spinner-overlay").style.display = "flex";
    }

    // Ẩn spinner
    function hideSpinner() {
        const spinner = document.getElementById('spinner-overlay');
        if (spinner) {
            spinner.style.display = 'none';
            console.log("Spinner hidden");
        }

        const hasVisibleModal = document.querySelector('.modal.show');
        if (!hasVisibleModal) {
            const backdrop = document.querySelector('.modal-backdrop');
            if (backdrop) {
                backdrop.remove();
                console.log("Backdrop removed (no visible modal)");
            }
        } else {
            console.log("Modal still visible, skipping backdrop removal");
        }
    }

    // Function to format date to datetime-local input format (YYYY-MM-DDThh:mm)
    function formatDateToInput(date) {
        return date.getFullYear() + '-' +
            ('0' + (date.getMonth() + 1)).slice(-2) + '-' +
            ('0' + date.getDate()).slice(-2) + 'T' +
            ('0' + date.getHours()).slice(-2) + ':' +
            ('0' + date.getMinutes()).slice(-2);
    }

    // Function to format date to API format (YYYY-MM-DD HH:mm:ss)
    function formatDateToApi(date) {
        return date.getFullYear() + '-' +
            ('0' + (date.getMonth() + 1)).slice(-2) + '-' +
            ('0' + date.getDate()).slice(-2) + ' ' +
            ('0' + date.getHours()).slice(-2) + ':' +
            ('0' + date.getMinutes()).slice(-2) + ':' +
            ('0' + date.getSeconds()).slice(-2);
    }

    // Set default dates: start of today (00:00:00) to current time
    let now = new Date();
    let startOfDay = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 0, 0, 0);

    $('#startDate').val(formatDateToInput(startOfDay));
    $('#endDate').val(formatDateToInput(now));

    // Initialize DataTable
    let table = $('#hassBiTable').DataTable({
        "paging": true,
        "searching": true,
        "ordering": true,
        "info": true,
        "autoWidth": false,
        "columns": [
            { "data": "moNumber" },
            { "data": "modelName" },
            {
                "data": "inStationTime",
                "render": function (data) {
                    return data ? new Date(data).toLocaleString() : "N/A";
                }
            },
            { "data": "targetQty" },
            { "data": "skipHassBi" },
            {
                "data": "skipReason",
                "render": function (data) {
                    return data ? data : "N/A";
                }
            },
            { "data": "type" },
            {
                "data": "lotSizeMin",
                "render": function (data) {
                    return data !== null && data !== undefined ? data : "N/A";
                }
            },
            {
                "data": "lotSizeMax",
                "render": function (data) {
                    return data !== null && data !== undefined ? data : "N/A";
                }
            },
            {
                "data": "sampleSize",
                "render": function (data) {
                    return data !== null && data !== undefined ? data : "N/A";
                }
            },
            {
                "data": "keyPartNo",
                "render": function (data) {
                    return data ? data : "N/A";
                }
            }
        ]
    });

    // Function to load data from both APIs
    function loadData() {
        let startDate = $('#startDate').val();
        let endDate = $('#endDate').val();

        // Validate inputs
        if (!startDate || !endDate) {
            alert("Please select both start date and end date.");
            return;
        }

        // Convert datetime-local format to "YYYY-MM-DD HH:mm:ss"
        let startDateFormatted = startDate.replace("T", " ") + ":00";
        let endDateFormatted = endDate.replace("T", " ") + ":00";

        // Prepare data for POST request
        let requestData = {
            startDate: startDateFormatted,
            endDate: endDateFormatted
        };

        // Show spinner before sending requests
        showSpinner();

        // Call both APIs concurrently
        Promise.allSettled([
            $.ajax({
                url: 'http://10.220.130.119:9090/api/ReportSkipHass/GeMoBI',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(requestData)
            }).catch(() => []), // Return empty array if GeMoBI fails
            $.ajax({
                url: 'http://10.220.130.119:9090/api/ReportSkipHass/GetMoData',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(requestData)
            }).catch(() => []) // Return empty array if GetMoData fails
        ])
            .then(([geMoBiResult, getMoDataResult]) => {
                // Extract data from results, default to empty array if no data
                const geMoBiData = geMoBiResult.status === 'fulfilled' ? geMoBiResult.value : [];
                const getMoData = getMoDataResult.status === 'fulfilled' ? getMoDataResult.value : [];

                // Normalize GetMoData to match BIData structure
                const normalizedGetMoData = getMoData.map(item => ({
                    moNumber: item.moNumber,
                    modelName: item.modelName,
                    inStationTime: item.inStationTime, // Rename inStationTime to inStationTime
                    targetQty: item.targetQty,
                    skipHassBi: item.skipHassBi,
                    skipReason: item.skipReason,
                    type: item.type,
                    lotSizeMin: null, // Not available in GetMoData
                    lotSizeMax: null, // Not available in GetMoData
                    sampleSize: null, // Not available in GetMoData
                    keyPartNo: null   // Not available in GetMoData
                }));

                // Combine data without removing duplicates
                const combinedData = [...geMoBiData, ...normalizedGetMoData];

                // Store data for export
                tableData = combinedData;

                // Clear existing data and load new data into DataTable
                table.clear().rows.add(combinedData).draw();

                // Hide spinner after success
                hideSpinner();

                // Show alert if no data is found
                if (combinedData.length === 0) {
                    alert("No data found for the specified criteria.");
                }
            })
            .catch(error => {
                let errorMessage = error.responseJSON ? error.responseJSON.message : "An error occurred while fetching data.";
                alert(errorMessage);
                // Hide spinner after error
                hideSpinner();
            });
    }

    // Load data automatically when the page loads
    loadData();

    // Handle Load Data button click
    $('#loadDataBtn').click(function () {
        loadData();
    });

    // Handle Export Excel button click
    $('#exportExcelBtn').click(function () {
        if (tableData.length === 0) {
            alert("No data available to export.");
            return;
        }

        // Prepare data for Excel
        const exportData = tableData.map(item => ({
            MO_NUMBER: item.moNumber,
            MODEL_NAME: item.modelName,
            TEST_TIME: item.inStationTime ? new Date(item.inStationTime).toLocaleString() : "N/A",
            TARGET_QTY: item.targetQty,
            SKIP_HASS_BI: item.skipHassBi,
            SKIP_REASON: item.skipReason || "N/A",
            TYPE: item.type,
            LOT_SIZE_MIN: item.lotSizeMin !== null && item.lotSizeMin !== undefined ? item.lotSizeMin : "N/A",
            LOT_SIZE_MAX: item.lotSizeMax !== null && item.lotSizeMax !== undefined ? item.lotSizeMax : "N/A",
            SAMPLE_SIZE: item.sampleSize !== null && item.sampleSize !== undefined ? item.sampleSize : "N/A",
            KEY_PART_NO: item.keyPartNo || "N/A"
        }));

        // Create worksheet from data
        const worksheet = XLSX.utils.json_to_sheet(exportData);
        // Create workbook and append worksheet
        const workbook = XLSX.utils.book_new();
        XLSX.utils.book_append_sheet(workbook, worksheet, "Hass BI Report");
        // Export Excel file
        XLSX.writeFile(workbook, "HASSBI_Report.xlsx");
    });
});