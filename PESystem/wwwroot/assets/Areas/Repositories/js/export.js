document.addEventListener("DOMContentLoaded", () => {
    const apiBaseUrl = "http://10.220.130.119:9090/api";
    const elements = {
        serialNumbersInput: document.getElementById("sn-input"),
        modelNameField: document.querySelector(".model-name-field"),
        productLineField: document.querySelector(".product-line-field"),
        saveButton: document.getElementById("export-btn"),
        duplicateWarning: document.getElementById("duplicate-warning"),
        exportPerson: document.getElementById("exportPerson")
    };

    const parseSerialNumbers = () => elements.serialNumbersInput.value
        .trim()
        .split("\n")
        .map(sn => sn.trim().toUpperCase())
        .filter(sn => sn);

    const hasDuplicates = serials => new Set(serials).size !== serials.length;

    const notify = (type, message) => {
        Swal.fire({
            icon: type,
            text: message,
            toast: true,
            position: 'top-end',
            showConfirmButton: false,
            timer: 3000,
            timerProgressBar: true
        });
    };

    const updateSerialDetails = async serials => {
        const [modelNames, productLines] = [[], []];
        await Promise.all(serials.map(async serial => {
            try {
                const response = await fetch(`${apiBaseUrl}/Product/GetSNInfo?serialNumber=${serial}`);
                if (!response.ok) {
                    if (response.status === 404) {
                        console.error(`Không tìm thấy API cho SN ${serial}`);
                    }
                    modelNames.push("");
                    productLines.push("");
                    return;
                }
                const data = await response.json();
                modelNames.push(data.success ? data.modelName || "" : "");
                productLines.push(data.success ? data.productLine || "" : "");
            } catch (error) {
                console.error(`Lỗi khi lấy SN ${serial}:`, error);
                modelNames.push("");
                productLines.push("");
            }
        }));
        elements.modelNameField.value = modelNames.join("\n");
        elements.productLineField.value = productLines.join("\n");
    };

    const saveExportData = async (serials, ignoreNonExisting = false) => {
        const payload = {
            ExportPerson: elements.exportPerson.value,
            SerialNumbers: serials
        };
        try {
            const response = await fetch(`${apiBaseUrl}/Export/ExportSerialNumber`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            });
            const data = await response.json();
            if (data.success) {
                notify("success", `Đã xuất ${data.totalInserted} serial numbers mới và cập nhật ${data.totalUpdated}!`);
                if (data.nonExistingSerials?.length) {
                    notify("info", `Serial numbers không tồn tại đã xuất: ${data.nonExistingSerials.join(", ")}`);
                }
                elements.serialNumbersInput.value = "";
                elements.modelNameField.value = "";
                elements.productLineField.value = "";
                elements.duplicateWarning.style.display = "none";
            } else if (!ignoreNonExisting && data.nonExistingSerials?.length) {
                const confirmContinue = await Swal.fire({
                    title: "Serial Numbers Không Tồn Tại",
                    text: `Serial numbers không tồn tại: ${data.nonExistingSerials.join(", ")}!\nTiếp tục xuất kho?`,
                    icon: "warning",
                    showCancelButton: true,
                    confirmButtonText: "Có",
                    cancelButtonText: "Không"
                });
                if (confirmContinue.isConfirmed) {
                    await saveExportData(serials, true);
                } else {
                    notify("warning", "Đã hủy xuất kho!");
                }
            } else {
                notify("error", data.message || "Xuất kho thất bại!");
            }
        } catch (error) {
            console.error("Lỗi xuất kho:", error);
            notify("error", "Lỗi hệ thống!");
        }
    };

    elements.serialNumbersInput.addEventListener("input", async () => {
        const serials = parseSerialNumbers();
        elements.duplicateWarning.style.display = hasDuplicates(serials) ? "block" : "none";
        await updateSerialDetails(serials);
    });

    elements.saveButton.addEventListener("click", async () => {
        const serials = parseSerialNumbers();
        if (!serials.length) {
            notify("warning", "Nhập ít nhất 1 SN!");
            return;
        }
        if (hasDuplicates(serials)) {
            notify("warning", "Có SN bị trùng!");
            return;
        }

        const confirmExport = await Swal.fire({
            title: "Xác Nhận Xuất Kho",
            text: `Bạn có chắc muốn xuất ${serials.length} serial numbers?`,
            icon: "question",
            showCancelButton: true,
            confirmButtonText: "Có",
            cancelButtonText: "Không"
        });

        if (confirmExport.isConfirmed) {
            await saveExportData(serials);
        } else {
            notify("warning", "Đã hủy xuất kho!");
        }
    });
});