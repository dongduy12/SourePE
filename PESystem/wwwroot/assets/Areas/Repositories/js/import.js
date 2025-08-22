document.addEventListener("DOMContentLoaded", function () {
    const trayInput = document.querySelector('input[name="location"]');
    if (!trayInput) {
        console.error("Phần tử input[name='location'] không tồn tại.");
        return;
    }
    const serialInput = document.getElementById("sn-input");
    const entryButton = document.getElementById("entry-btn");
    const serialNumberTable = document.getElementById("serial-number-in-tray");
    const serialCount = document.getElementById("serial-count");
    const modelNameField = document.querySelector(".model-name-field");
    const productLineField = document.querySelector(".product-line-field");
    const serialLimitWarning = document.getElementById("serial-limit-warning");
    const duplicateWarning = document.getElementById("duplicate-warning");

    const apiBaseUrl = "http://10.220.130.119:9090/api/Product"; // Đường dẫn API base
    let trayData = []; // Số serial đã tồn tại trong tray từ API TrayInfo

    var maxSlots = 20; // Gia tri mac dinh
    // Lấy thông tin khay (TrayInfo)
    trayInput.addEventListener("input", function () {
        let location = trayInput.value.trim().toUpperCase();

        // Kiểm tra định dạng đầu vào
        var patternWithoutLevel = /^(PE|[A-Z]+)(\d+)-K(\d+)$/;
        var matchWithoutLevel = patternWithoutLevel.exec(location);

        if (matchWithoutLevel) {
            // Nếu không có level, thêm "0" vào vị trí level
            location = `${matchWithoutLevel[1]}${matchWithoutLevel[2]}-0-K${matchWithoutLevel[3]}`;
            trayInput.value = location; // Cập nhật giá trị trong ô input
        }

        var locationPattern = /^(PE|[A-Z]+)(\d+)-(\d+)-K(\d+)$/;
        var match = locationPattern.exec(location);

        if (match) {
            const shelf = match[1];
            const column = match[2];
            const level = match[3];
            const tray = match[4];

            // Điền thông tin vào các trường readonly
            document.querySelector('input[name="shelf"]').value = shelf.toUpperCase();
            document.querySelector('input[name="column"]').value = column;
            document.querySelector('input[name="level"]').value = level;
            document.querySelector('input[name="tray"]').value = tray;

            // Gọi API để lấy thông tin khay
            fetch(`${apiBaseUrl}/TrayInfo?shelf=${shelf}&column=${column}&level=${level}&tray=${tray}`)
                .then(response => response.json())
                .then(data => {
                    if (data.success) {

                        trayData = data.occupiedPositions || [];
                        maxSlots = data.maxSlots;
                        // Hiển thị số lượng trống còn lại
                        serialCount.textContent = `Tổng số serial đã nhập: ${trayData.length}/${maxSlots}`;
                        console.log("======maxSlots ne: =====", maxSlots);
                        // Cập nhật bảng serial number trong tray
                        serialNumberTable.innerHTML = trayData.map((item) => `
                            <tr>
                                <td>${item.positionInTray}</td>
                                <td>${item.serialNumber}</td>
                            </tr>
                        `).join("");
                        // Kiểm tra nếu khay đầy
                        if (trayData.length >= maxSlots) {
                            serialLimitWarning.style.display = "block";
                        } else {
                            serialLimitWarning.style.display = "none";
                        }
                    } else {
                        showError("Error!");
                    }
                })
                .catch(error => {
                    console.error("Lỗi khi tải thông tin khay:", error);
                    showError("Error!");
                });
        } else {
            // Xóa dữ liệu nếu không đúng định dạng
            document.querySelector('input[name="shelf"]').value = "";
            document.querySelector('input[name="column"]').value = "";
            document.querySelector('input[name="level"]').value = "";
            document.querySelector('input[name="tray"]').value = "";
            serialCount.textContent = "Tổng số serial đã nhập: 0/...";
            serialNumberTable.innerHTML = "";
            trayData = [];
        }
    });

    // Thêm sự kiện 'keydown' cho ô "Vị trí khay"
    trayInput.addEventListener("keydown", function (event) {
        if (event.key === "Enter") {
            event.preventDefault(); // Ngăn chặn hành vi mặc định của phím Enter
            serialInput.focus(); // Chuyển con trỏ đến ô "SN"
        }
    });


    // Cập nhật thông tin modelName và productLine
    async function updateSerialDetails(serialNumbers) {
        const modelNames = [];
        const productLines = [];

        for (const serial of serialNumbers) {
            try {
                const response = await fetch(`${apiBaseUrl}/GetSNInfo?serialNumber=${serial}`);
                const data = await response.json();
                if (data.success) {
                    modelNames.push(data.modelName || "");
                    productLines.push(data.productLine || "");
                } else {
                    modelNames.push("");
                    productLines.push("");
                }
            } catch (error) {
                console.error(`Lỗi khi lấy thông tin SN ${serial}:`, error);
                modelNames.push("");
                productLines.push("");
            }
        }

        modelNameField.value = modelNames.join("\n");
        productLineField.value = productLines.join("\n");
    }

    // Kiểm tra trùng lặp
    function hasDuplicateSerials(serialNumbers) {
        const uniqueSerials = new Set(serialNumbers);
        return uniqueSerials.size !== serialNumbers.length;
    }

    // Theo dõi thay đổi trong textarea
    serialInput.addEventListener("input", function () {
        const serialNumbers = serialInput.value
            .trim()
            .split("\n")
            .map(sn => sn.trim().toUpperCase()) 
            .filter(sn => sn);

        // Kiểm tra trùng lặp giữa danh sách nhập và khay
        const duplicatesInTray = serialNumbers.filter(sn => trayData.some(item => item.serialNumber === sn));
        const hasDuplicates = hasDuplicateSerials(serialNumbers) || duplicatesInTray.length > 0;

        // Hiển thị cảnh báo nếu có trùng lặp
        if (hasDuplicates) {
            duplicateWarning.style.display = "block";
        } else {
            duplicateWarning.style.display = "none";
        }
        const totalSerials = trayData.length + serialNumbers.length;
        // Kiểm tra giới hạn số lượng
        if (totalSerials > maxSlots) {
            serialLimitWarning.style.display = "block";
        } else {
            serialLimitWarning.style.display = "none";
        }
        // Cập nhật tổng số serial đã nhập
        serialCount.textContent = `Tổng số serial đã nhập: ${totalSerials}/${maxSlots}`;
        console.log("=======maxSlots ne 2: =====", maxSlots);
        // Cập nhật thông tin modelName và productLine
        updateSerialDetails(serialNumbers);
    });

    // Gửi dữ liệu lên API khi nhấn nút "Nhập kho"
    entryButton.addEventListener("click", async function () {
        const shelf = document.querySelector('input[name="shelf"]').value;
        const column = document.querySelector('input[name="column"]').value;
        const level = document.querySelector('input[name="level"]').value;
        const tray = document.querySelector('input[name="tray"]').value;
        const currentUser = document.getElementById("entryPerson").value;
        const serialNumbers = serialInput.value
            .trim()
            .split("\n")
            .map(sn => sn.trim().toUpperCase())
            .filter(sn => sn);
        if (!shelf || !column || !level || !tray || serialNumbers.length === 0) {
            //alert("Vui lòng điền đầy đủ thông tin vị trí và danh sách serial.");
            showWarning("Vui lòng điền đây đủ!");
            return;
        }

        const duplicatesInTray = serialNumbers.filter(sn => trayData.includes(sn));
        if (duplicatesInTray.length > 0 || hasDuplicateSerials(serialNumbers)) {
            //alert("Danh sách serial có trung lặp hoặc đã tồn tại trong khay.");
            showWarning("Vui lòng điền đây đủ!");
            return;
        }
        // Gửi API lưu sản phẩm
        try {
            const saveResponse = await fetch(`${apiBaseUrl}/SaveProducts`, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json"
                },
                body: JSON.stringify({
                    Shelf: shelf,
                    Column: column,
                    Level: level,
                    Tray: tray,
                    SerialNumbers: serialNumbers,
                    EntryPerson: currentUser
                })
            });

            const saveData = await saveResponse.json();

            if (saveData.success) {
                const failedSerials = saveData.results.filter(result => !result.success);
                if (failedSerials.length > 0) {
                    const failedMessages = failedSerials.map(result => `Serial: ${result.serialNumber}, Error: ${result.message}`).join("\n");
                    showError(`Có lỗi xảy ra với các serial:\n${failedMessages}`);
                    return;
                }
                showSuccess("Success!");
                trayInput.dispatchEvent(new Event("input"));
                serialInput.value = "";
            } else {
                showError(saveData.message);
                return;
            }

            // Gửi API nhận bản
            const payload = {
                serialnumbers: serialNumbers.join(","),
                owner: currentUser,
                location: trayInput.value.trim(),
                tag: "Nhận(Nhập kho)"
            };

            const receiveResponse = await fetch("http://10.220.130.119:9090/api/RepairStatus/receiving-status", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json"
                },
                body: JSON.stringify(payload)
            });

            const receiveResult = await receiveResponse.json();
            showInfo("Trạng thái nhận bản:" + receiveResult.message);

            if (receiveResult.message.replace(/"/g, '') === "OK") {
                location.reload();
            }
        } catch (error) {
            console.error("Lỗi khi xử lý yêu cầu:", error);
            showError("Error!");
        }
    });
});

