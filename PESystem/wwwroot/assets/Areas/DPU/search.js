async function submitSerialNumberForm() {
    const snInput = document.getElementById("sn-input").value.trim();
    const searchType = document.getElementById("typeSearch").value;

    if (!snInput) {
        alert("Vui lòng nhập Serial Numbers!");
        return;
    }

    const serialNumbers = snInput.split("\n").map(sn => sn.trim()).filter(sn => sn);

    if (serialNumbers.length === 0) {
        alert("Danh sách Serial Numbers không hợp lệ.");
        return;
    }

    const baseUrl = "http://10.220.130.119:9090/api/Config";
    const apiUrl = searchType === "current"
        ? `${baseUrl}/SearchSNCurrent`
        : `${baseUrl}/SearchSNHistory`;

    try {
        const response = await fetch(apiUrl, {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(serialNumbers)
        });

        if (!response.ok) {
            const errorData = await response.json();
            alert(`Lỗi: ${errorData.message || "Không thể truy xuất dữ liệu."}`);
            return;
        }

        const result = await response.json();

        console.log("Kết quả từ API:", result);

        let data = [];
        if (searchType === "current") {
            // Sửa lại để xử lý danh sách đầy đủ trả về từ API
            data = Array.isArray(result.current) ? result.current : [];
        } else if (searchType === "history") {
            data = Array.isArray(result.history) ? result.history : [];
        }

        if (data.length === 0) {
            alert("Không có dữ liệu để hiển thị.");
            return;
        }

        currentSerialNumbers = serialNumbers; // Cập nhật danh sách Serial Numbers hiện tại
        updateTable(data);
    } catch (error) {
        console.error("Lỗi khi gọi API:", error);
        alert("Đã xảy ra lỗi khi gọi API. Vui lòng thử lại sau.");
    }
}


async function submitUpdate() {
    const statusSelect = document.getElementById("status-config");
    const status = statusSelect.value;
    const currentStation = document.getElementById("modal-station").innerText.trim();

    const nextStation = document.getElementById("nextStationInput")?.value.trim();
    const ddrToolFail = document.getElementById("ddrToolInput")?.value.trim();
    const ftOffFail = document.getElementById("ftOffInput")?.value.trim();
    const hassFail = document.getElementById("hassInput")?.value.trim();
    const ftHassFail = document.getElementById("ftHassInput")?.value.trim();
    const nvInstruction = document.getElementById("nvInstructionInput")?.value.trim();
    const RERepair = document.getElementById("RERepairInput")?.value.trim();
    const Owner = $('#repairPerson').val().trim();
    if (!status && !nextStation) {
        alert("Vui lòng chọn trạng thái hoặc nhập trạm kế tiếp.");
        return;
    }
    if (currentStation === "CUSTOMER" && !nextStation) {
        alert("Vui lòng chọn trạm tiếp theo cho CUSTOMER.");
        return;
    }

    const requests = currentSerialNumbers.map(sn => ({
        serialNumber: sn,
        status: status,
        ddrToolFailCode: currentStation === "DDR-TOOL" ? ddrToolFail : "",
        hassFailCode: currentStation === "HASS" ? hassFail : "",
        fT_OFFFailCode: currentStation === "FT-OFF" ? ftOffFail : "",
        fT_HASSFailCode: currentStation ==="FT-HASS" ? ftHassFail: "",
        nextStation: currentStation === "CUSTOMER" ? nextStation :"", // Lấy giá trị trạm kế tiếp nếu người dùng cung cấp
        customerInput: currentStation ==="CUSTOMER" ? nvInstruction : "", // Lấy giá trị Customer nếu người dùng nhập
        RERepairInput: currentStation === "REPAIR" ? RERepair : "", // Lấy giá trị Customer nếu người dùng nhập
        owner: Owner
    }));
    try {
        const response = await fetch("http://10.220.130.119:9090/api/Config/ProcessSNFlow", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(requests)
        });

        if (!response.ok) {
            const errorData = await response.json();
            alert(`Lỗi: ${errorData.message || "Không thể cập nhật trạng thái."}`);
            return;
        }

        const result = await response.json();
        alert(result.message || "Cập nhật trạng thái thành công!");

        // Làm mới bảng dữ liệu
        await submitSerialNumberForm();

        // Đóng modal sau khi cập nhật
        const modal = bootstrap.Modal.getInstance(document.getElementById("updateModal"));
        if (modal) {
            modal.hide();
        }
    } catch (error) {
        console.error("Lỗi khi cập nhật trạng thái:", error);
        alert("Đã xảy ra lỗi khi cập nhật trạng thái. Vui lòng thử lại sau.");
    }
}


function handleStatusChange() {
    const statusSelectElement = document.getElementById("status-config");
    const currentStation = document.getElementById("modal-station")?.innerText.trim();

    if (!currentStation) {
        console.error("Không tìm thấy station hiện tại.");
        return;
    }

    const ddrToolFailElement = document.getElementById("DDR_TOOL_FAIL");
    const ftOffFailElement = document.getElementById("FT_OFF_FAIL");
    const hassFailElement = document.getElementById("HASS_FAIL");
    const ftHassFailElement = document.getElementById("FT_HASS_FAIL");
    const nextStationElement = document.getElementById("nextStationInputWrapper");
    const nvInstructionElement = document.getElementById("NVInstruction");
    const RERepairElement = document.getElementById("RERepair");

    // Ẩn tất cả các input
    if (ddrToolFailElement) ddrToolFailElement.style.display = "none";
    if (ftOffFailElement) ftOffFailElement.style.display = "none";
    if (hassFailElement) hassFailElement.style.display = "none";
    if (ftHassFailElement) ftHassFailElement.style.display = "none";
    if (nextStationElement) nextStationElement.style.display = "none";
    if (nvInstructionElement) nvInstructionElement.style.display = "none";
    if (RERepairElement) RERepairElement.style.display = "none";
    if (statusSelectElement) statusSelectElement.style.display = "block";

    // Hiển thị input dựa vào station hiện tại và trạng thái
    if (currentStation === "CUSTOMER") {
        if (statusSelectElement) statusSelectElement.style.display = "none";
        if (nextStationElement) nextStationElement.style.display = "block";
        if (nvInstructionElement) nvInstructionElement.style.display = "block";
    } else if (currentStation === "REPAIR") {
        if (statusSelectElement) statusSelectElement.style.display = "none";
        if (RERepairElement) RERepairElement.style.display = "block";
    } else if (statusSelectElement?.value === "FAIL") {
        document.getElementById("ddrToolInput").value = "";
        document.getElementById("ftOffInput").value = "";
        document.getElementById("hassInput").value = "";
        document.getElementById("ftHassInput").value = "";
        switch (currentStation) {
            case "DDR-TOOL":
                if (ddrToolFailElement) ddrToolFailElement.style.display = "block";
                break;
            case "FT-OFF":
                if (ftOffFailElement) ftOffFailElement.style.display = "block";
                break;
            case "HASS":
                if (hassFailElement) hassFailElement.style.display = "block";
                break;
            case "FT-HASS":
                if (ftHassFailElement) ftHassFailElement.style.display = "block";
                break;
            default:
                if (nextStationElement) nextStationElement.style.display = "block";
                break;
        }
    }else if (statusSelectElement?.value === "PASS") {
        switch (currentStation) {
            case "DDR-TOOL":
                document.getElementById("ddrToolInput").value = statusSelectElement.value;
                break;
            case "FT-OFF":
                document.getElementById("ftOffInput").value = statusSelectElement.value;
                break;
            case "HASS":
                document.getElementById("hassInput").value = statusSelectElement.value;
                break;
            case "FT-HASS":
                document.getElementById("ftHassInput").value = statusSelectElement.value;
                break;
            default:
                break;
        }
        return;
    }
}

async function openUpdateModal() {
    if (!currentSerialNumbers || currentSerialNumbers.length === 0) {
        alert("Vui lòng tìm kiếm Serial Numbers trước.");
        return;
    }

    try {
        const response = await fetch("http://10.220.130.119:9090/api/Config/SearchSNCurrent", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(currentSerialNumbers)
        });
        console.log("SERIAL NUMBER :", currentSerialNumbers); // Kiểm tra phản hồi từ API
        const result = await response.json();
        console.log("Kết quả API:", result); // Kiểm tra phản hồi từ API

        if (!result.success || !result.current || result.current.length === 0) {
            alert("Không thể tải thông tin hiện tại của Serial Numbers.");
            return;
        }

        // Lấy giá trị station từ phần tử cuối cùng của mảng current
        const lastRecord = result.current[result.current.length - 1]; // Lấy phần tử cuối cùng
        const station = lastRecord.station || ""; // Lấy giá trị station của phần tử cuối cùng
        const stationElement = document.getElementById("modal-station");
        if (stationElement) {
            console.log("====STATION:=====", station); // Debug thông tin station
            stationElement.innerText = station;
        } else {
            console.error("Không tìm thấy phần tử 'modal-station'.");
        }

        // Tự động áp dụng logic hiển thị
        handleStatusChange();
        // Hiển thị modal
        const modal = new bootstrap.Modal(document.getElementById("updateModal"));
        modal.show();
    } catch (error) {
        console.error("Lỗi trong openUpdateModal:", error);
        alert("Đã xảy ra lỗi khi tải dữ liệu cập nhật.");
    }
}


function updateTable(data) {
    if (!Array.isArray(data)) {
        console.error("Dữ liệu không phải là mảng:", data);
        return;
    }

    const tableData = data.map(item => [
        `<input type="checkbox" class="row-checkbox" data-serial="${item.serialNumber || ''}">`, // Cột checkbox
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
        item.station || "",
        item.owner || ""
    ]);
    if (tableData.length === 0) {
        // Ẩn bảng nếu không có dữ liệu
        document.getElementById("keyPartTable").style.display = "none";
        return;
    }

    // Hiển thị bảng khi có dữ liệu
    document.getElementById("keyPartTable").style.display = "table";


    // Kiểm tra xem số lượng cột trong mỗi hàng có khớp với số lượng cột định nghĩa
    const columnsCount = 18; // Số lượng cột trong cấu hình DataTables
    const invalidRows = tableData.filter(row => row.length !== columnsCount);

    if (invalidRows.length > 0) {
        console.error("Số lượng cột không khớp với cấu hình bảng. Dữ liệu không hợp lệ:", invalidRows);
        return;
    }

    $('#keyPartTable').DataTable({
        destroy: true, // Xóa bảng cũ trước khi tạo mới
        data: tableData,
        columns: [
            { title: `<input type="checkbox" id="select-all" onclick="toggleAllCheckboxes(this)">`, orderable: false, searchable: false, className: "text-center" },
            { title: "SerialNumber" },
            { title: "ModelName" },
            { title: "WIPGroup" },
            { title: "TestCode" },
            { title: "Mô tả lỗi" },
            { title: "TestTime" },
            { title: "TestGroup" },
            { title: "DDR-TOOL" },
            { title: "NVInstruction" },
            { title: "PEInstruction" },
            { title: "RERepair" },
            { title: "FT-OFF" },
            { title: "HASS" },
            { title: "FT-HASS" },
            { title: "PreviousGroup" },
            { title: "GroupCurrent" },
            { title: "Owner" }
        ],
        columnDefs: [
            { targets: 0, width: "40px" } // Thu hẹp cột checkbox (cột đầu tiên)
        ],
        paging: true,
        searching: true,
        ordering: false,
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
}


// Xóa lớp phủ khi modal đóng
document.getElementById("updateModal").addEventListener("hidden.bs.modal", () => {
    removeBackdrop();
});

function removeBackdrop() {
    const backdrop = document.querySelector(".modal-backdrop");
    if (backdrop) {
        backdrop.parentNode.removeChild(backdrop); // Xóa lớp phủ nếu còn tồn tại
    }
}


let selectedSerialNumbers = [];

// Hàm xử lý khi nhấn nút Update
function openNoteModal() {
    selectedSerialNumbers = $(".row-checkbox:checked")
        .map((_, checkbox) => $(checkbox).data("serial"))
        .get();

    if (selectedSerialNumbers.length === 0) {
        alert("Vui lòng chọn ít nhất một Serial Number.");
        return;
    }

    // Hiển thị modal
    const modal = new bootstrap.Modal(document.getElementById("noteModal"));
    modal.show();
}

// Hàm xử lý cập nhật ghi chú
async function updateInstruction() {
    const NVInstruction = document.getElementById("nvInstruction-input").value.trim();
    const PEInstruction = document.getElementById("peInstruction-input").value.trim();
    const RERepair = document.getElementById("reRepair-input").value.trim();

    try {
        const requests = selectedSerialNumbers.map(sn => ({
            serialNumber: sn,
            peInstruction: PEInstruction,
            nvInstruction: NVInstruction,
            reRepair: RERepair
        }));

        const response = await fetch("http://10.220.130.119:9090/api/Config/UpdateInstruction", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(requests)
        });

        if (!response.ok) {
            const errorData = await response.json();
            alert(`Lỗi: ${errorData.message || "Không thể cập nhật ghi chú."}`);
            return;
        }

        const result = await response.json();
        alert(result.message || "Cập nhật ghi chú thành công!");

        // Làm mới bảng dữ liệu
        await submitSerialNumberForm();

        // Đóng modal sau khi cập nhật
        const modal = bootstrap.Modal.getInstance(document.getElementById("noteModal"));
        modal.hide();
    } catch (error) {
        console.error("Lỗi khi cập nhật ghi chú:", error);
        alert("Đã xảy ra lỗi khi cập nhật ghi chú. Vui lòng thử lại sau.");
    }
}

// Hàm xử lý khi chọn checkbox "Select All"
function toggleAllCheckboxes(source) {
    const checkboxes = document.querySelectorAll(".row-checkbox");
    checkboxes.forEach(checkbox => {
        checkbox.checked = source.checked;
        $(checkbox).trigger("change"); // Gọi sự kiện change
    });
}

// Hàm xử lý khi chọn checkbox hàng
function handleRowCheckboxChange() {
    const anyChecked = $(".row-checkbox:checked").length > 0;
    document.getElementById("peInstruction-btn").style.display = anyChecked ? "block" : "none";
    document.getElementById("nvInstruction-btn").style.display = anyChecked ? "block" : "none";
    document.getElementById("reRepair-btn").style.display = anyChecked ? "block" : "none";
}
// Lắng nghe sự kiện change cho các checkbox hàng
$(document).on("change", ".row-checkbox", handleRowCheckboxChange);

function showInput(buttonId) {
    document.getElementById("peInstruction-input-wrapper").style.display = "none";
    document.getElementById("nvInstruction-input-wrapper").style.display = "none";
    document.getElementById("reRepair-input-wrapper").style.display = "none";
    if (buttonId === "peInstruction-btn") {
        document.getElementById("peInstruction-input-wrapper").style.display = "block";
    } else if (buttonId === "nvInstruction-btn") {
        document.getElementById("nvInstruction-input-wrapper").style.display = "block";
    } else if (buttonId === "reRepair-btn") {
        document.getElementById("reRepair-input-wrapper").style.display = "block";
    }
}

// Lắng nghe sự kiện click cho các nút
document.getElementById("peInstruction-btn").addEventListener("click", () => showInput("peInstruction-btn"));
document.getElementById("nvInstruction-btn").addEventListener("click", () => showInput("nvInstruction-btn"));
document.getElementById("reRepair-btn").addEventListener("click", () => showInput("reRepair-btn"));



// Lắng nghe sự kiện click vào từng ô
document.getElementById("keyPartTable").addEventListener("click", function (event) {
    const target = event.target;

    if (target.tagName === "TD") {
        const row = target.parentElement; // Lấy hàng của ô được click
        const columnIndex = target.cellIndex; // Vị trí cột
        const serialNumber = row.cells[1]?.innerText; // Giả định cột thứ 2 là Serial Number

        if (!serialNumber) {
            alert("Không tìm thấy Serial Number.");
            return;
        }

        // Lấy giá trị hiện tại trong ô
        const currentValue = target.innerText;

        // Hiển thị modal để cập nhật
        openEditModal(serialNumber, columnIndex, currentValue);
    }
});

// Hiển thị modal để chỉnh sửa
function openEditModal(serialNumber, columnIndex, currentValue) {
    const modal = document.getElementById("editModal");
    const inputField = document.getElementById("editInput");

    inputField.value = currentValue; // Điền giá trị hiện tại vào input

    // Lưu thông tin serial và cột vào các thuộc tính để dùng khi submit
    inputField.dataset.serialNumber = serialNumber;
    inputField.dataset.columnIndex = columnIndex;

    // Hiển thị modal
    const bootstrapModal = new bootstrap.Modal(modal);
    bootstrapModal.show();
}

// Xử lý sự kiện cập nhật khi click nút "Cập nhật"
document.getElementById("updateValueBtn").addEventListener("click", async function () {
    const inputField = document.getElementById("editInput");
    const serialNumber = inputField.dataset.serialNumber;
    const columnIndex = inputField.dataset.columnIndex;
    const newValue = inputField.value.trim();

    if (!newValue) {
        alert("Giá trị không được để trống.");
        return;
    }

    // Gửi yêu cầu cập nhật qua API
    try {
        const response = await fetch("http://10.220.130.119:9090/api/Config/UpdateValue", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                serialNumber: serialNumber,
                columnIndex: columnIndex, // Cột sẽ được cập nhật
                newValue: newValue
            })
        });

        if (!response.ok) {
            const errorData = await response.json();
            alert(`Lỗi: ${errorData.message || "Không thể cập nhật giá trị."}`);
            return;
        }

        const result = await response.json();
        alert(result.message || "Cập nhật thành công!");

        // Làm mới bảng sau khi cập nhật
        await submitSerialNumberForm();

        // Đóng modal
        const modal = bootstrap.Modal.getInstance(document.getElementById("editModal"));
        if (modal) {
            modal.hide();
        }
    } catch (error) {
        console.error("Lỗi khi cập nhật giá trị:", error);
        alert("Đã xảy ra lỗi khi cập nhật giá trị. Vui lòng thử lại sau.");
    }
});
