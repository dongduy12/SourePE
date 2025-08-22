


document.addEventListener("DOMContentLoaded", function () {
    const projectInput = document.getElementById("project");
    const projectDatalist = document.getElementById("projectSuggestions");

    // Gọi API lấy danh sách PROJECT_LINE
    function fetchProjectSuggestions() {
        fetch("http://10.220.130.119:9090/api/CheckList/get-product-lines")
            .then(response => response.json())
            .then(data => {
                // Xóa dữ liệu cũ trước khi thêm mới
                projectDatalist.innerHTML = "";
                data.forEach(project => {
                    let option = document.createElement("option");
                    option.value = project; // Gán giá trị từ API vào datalist
                    projectDatalist.appendChild(option);
                });
            })
            .catch(error => console.error("Error fetching project suggestions:", error));
    }

    // Gọi API khi người dùng nhập vào input
    projectInput.addEventListener("input", fetchProjectSuggestions);
});


document.addEventListener("DOMContentLoaded", function () {
    const projectInput = document.getElementById("project");
    const modelNameInput = document.getElementById("modelName");
    const modelNameDatalist = document.getElementById("modelNameSuggestions");

    // Gọi API lấy danh sách MODEL_NAME theo PROJECT (PRODUCT_LINE)
    function fetchModelNameSuggestions() {
        let projectValue = projectInput.value.trim();
        if (!projectValue) return; // Nếu Project trống, không gọi API

        fetch("http://10.220.130.119:9090/api/CheckList/get-model-names", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({ ProductLine: projectValue }) // Gửi Project làm ProductLine
        })
            .then(response => response.json())
            .then(data => {
                // Xóa dữ liệu cũ trước khi thêm mới
                modelNameDatalist.innerHTML = "";
                data.forEach(model => {
                    let option = document.createElement("option");
                    option.value = model; // Gán giá trị từ API vào datalist
                    modelNameDatalist.appendChild(option);
                });
            })
            .catch(error => console.error("Error fetching model name suggestions:", error));
    }

    // Gọi API khi người dùng nhập vào input Model Name
    modelNameInput.addEventListener("input", fetchModelNameSuggestions);
});


document.addEventListener("DOMContentLoaded", function () {
    const tableBody = document.getElementById("checklistTableBody");
    const paginationContainer = document.getElementById("pagination");
    const pageInfoContainer = document.getElementById("pageInfo");
    const issueDetailsModal = new bootstrap.Modal(document.getElementById("issueDetailsModal"));
    const issueDetailsContainer = document.getElementById("issueDetails");
    const editButton = document.getElementById("editIssue");
    const saveButton = document.getElementById("saveIssueEdit");



    // save new issue
    document.getElementById("saveIssue").addEventListener("click", function () {
        let isValid = true;
        let errorMessage = "";

        // Kiểm tra tất cả các ô input và select có class 'required'
        document.querySelectorAll(".required").forEach(input => {
            if (input.value.trim() === "" || input.value === null) {
                isValid = false;
                errorMessage = "Please fill in all required fields.";
                input.classList.add("is-invalid");
            } else {
                input.classList.remove("is-invalid");
            }
        });

        if (!isValid) {
            document.getElementById("errorMessage").textContent = errorMessage;
            return;
        } else {
            document.getElementById("errorMessage").textContent = "";
        }

        let formData = new FormData();
        formData.append("Publisher", document.getElementById("publisher").value);
        formData.append("Time", document.getElementById("time").value);
        formData.append("Project", document.getElementById("project").value);
        formData.append("ModelName", document.getElementById("modelName").value);
        formData.append("Station", document.getElementById("station").value);
        formData.append("Error", document.getElementById("error").value);
        formData.append("Input", document.getElementById("input").value);
        formData.append("NGQty", document.getElementById("ngQty").value);
        formData.append("RootCause", document.getElementById("rootCause").value);
        formData.append("Location", document.getElementById("location").value);
        formData.append("Status", document.getElementById("status").value);
        formData.append("Owner", document.getElementById("owner").value);

        let fileInput = document.getElementById("locationPicture");
        if (fileInput.files.length > 0) {
            formData.append("Picture", fileInput.files[0]);
        }

        fetch("http://10.220.130.119:9090/api/CheckList/create-new-issue", {
            method: "POST",
            body: formData
        })
            .then(response => response.json())
            .then(data => {
                alert("Issue created successfully! ID: " + data.id);
                document.getElementById("issueForm").reset();
                let modalElement = document.getElementById('createIssueModal');
                if (modalElement) {
                    let modal = bootstrap.Modal.getInstance(modalElement);
                    modal.hide();
                }

                fetchChecklistData(); // Làm mới dữ liệu
            })
            .catch(error => {
                console.error("Error:", error);
                alert("Error saving issue.");
            });

    });

    let currentPage = 1;
    let rowsPerPage = 8;
    let data = [];
    let originalData = []; // Lưu dữ liệu gốc

    /** 🔹 Lấy dữ liệu từ API */
    function fetchChecklistData() {
        fetch("http://10.220.130.119:9090/api/CheckList/get-checklist", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({ id: "ALL" })
        })
            .then(response => response.json())
            .then(responseData => {
                data = responseData;
                originalData = [...data]; // Lưu dữ liệu gốc
                displayTable();
                setupPagination();
            })
            .catch(error => console.error("Error fetching checklist data:", error));
    }

    /** 🔹 Tạo phân trang */
    function setupPagination() {
        paginationContainer.innerHTML = "";

        let totalPages = Math.ceil(data.length / rowsPerPage);

        // Nút First
        let firstButton = document.createElement("button");
        firstButton.classList.add("btn", "btn-outline-primary", "btn-sm", "me-1");
        firstButton.textContent = "First";
        firstButton.addEventListener("click", () => {
            currentPage = 1;
            displayTable();
            updatePageInfo();
        });
        paginationContainer.appendChild(firstButton);

        // Nút Previous
        let prevButton = document.createElement("button");
        prevButton.classList.add("btn", "btn-outline-primary", "btn-sm", "me-1");
        prevButton.textContent = "Previous";
        prevButton.addEventListener("click", () => {
            if (currentPage > 1) {
                currentPage--;
                displayTable();
                updatePageInfo();
            }
        });
        paginationContainer.appendChild(prevButton);

        // Nút Next
        let nextButton = document.createElement("button");
        nextButton.classList.add("btn", "btn-outline-primary", "btn-sm", "me-1");
        nextButton.textContent = "Next";
        nextButton.addEventListener("click", () => {
            if (currentPage < totalPages) {
                currentPage++;
                displayTable();
                updatePageInfo();
            }
        });
        paginationContainer.appendChild(nextButton);

        // Nút Last
        let lastButton = document.createElement("button");
        lastButton.classList.add("btn", "btn-outline-primary", "btn-sm");
        lastButton.textContent = "Last";
        lastButton.addEventListener("click", () => {
            currentPage = totalPages;
            displayTable();
            updatePageInfo();
        });
        paginationContainer.appendChild(lastButton);
    }

    /** 🔹 Hiển thị thông tin trang hiện tại */
    function updatePageInfo() {
        let totalPages = Math.ceil(data.length / rowsPerPage);
        pageInfoContainer.innerHTML = `Page ${currentPage} of ${totalPages}`;
    }


    /** 🔹 Hiển thị bảng dữ liệu */
    function displayTable() {
        tableBody.innerHTML = "";
        let start = (currentPage - 1) * rowsPerPage;
        let end = start + rowsPerPage;
        let paginatedItems = data.slice(start, end);

        paginatedItems.forEach(item => {
            let statusColorClass = getStatusColorClass(item.status);
            let row = document.createElement("tr");
            row.innerHTML = `
                <td><a href="#" class="issue-link" data-id="${item.id}">${item.id ?? ""}</a></td>
                <td>${item.owner ?? ""}</td>
                <td><span class="badge ${statusColorClass}">${item.status ?? ""}</span></td>
                <td>${item.time ? new Date(item.time).toLocaleString() : "N/A"}</td>
                <td>${item.project ?? ""}</td>
                <td>${item.modelName ?? ""}</td>
                <td>${item.station ?? ""}</td>
                <td>${item.error ?? ""}</td>
                <td>${item.input ?? 0}</td>
                <td>${item.ngQty ?? 0}</td>
                <td>${item.dfr ?? "0%"}</td>
                <td>${item.rootCause ?? ""}</td>
                <td>${item.location ?? ""}</td>
                <td>${item.fa ?? ""}</td>
                <td>${item.action ?? ""}</td>
                <td>${item.publisher ?? ""}</td>
                <td>
                    <button class="btn btn-outline-danger btn-sm delete-issue-btn" data-id="${item.id}">
                        <i class="fa fa-trash"></i>
                    </button>
                </td>
            `;
            tableBody.appendChild(row);
        });

        addClickEventToIds();
        addDeleteEvent(); // Gọi hàm thêm sự kiện xóa
        updatePageInfo();
    }

    /** 🔥 Lấy currentUsername từ thẻ HTML */
    function getCurrentUsername() {
        const usernameElement = document.querySelector(".d-none.d-md-block.ps-2");
        return usernameElement ? usernameElement.textContent.trim() : null;
    }


    /** 🔥 Hàm gọi API xóa issue */
    function deleteIssue(issueId, publisher) {
        const currentUsername = getCurrentUsername(); // Lấy username từ HTML

        if (!currentUsername) {
            alert("Failed to get the current username.");
            return;
        }
        // Gọi API kiểm tra department của user
        fetch(`http://10.220.130.119:9090/api/CheckList/check-department/${currentUsername}`)
            .then(response => response.json())
            .then(data => {
                if (data.department === publisher) {
                    // Nếu khớp, tiếp tục xóa
                    fetch(`http://10.220.130.119:9090/api/CheckList/delete-issue/${issueId}`, {
                        method: "DELETE"
                    })
                        .then(response => response.json())
                        .then(data => {
                            alert(data.message);
                            fetchChecklistData(); // Làm mới dữ liệu
                        })
                        .catch(error => {
                            console.error("Error deleting issue:", error);
                            alert("Failed to delete the issue.");
                        });
                } else {
                    alert("You do not have permission to delete this issue.");
                }
            })
            .catch(error => {
                console.error("Error checking department:", error);
                alert("Failed to verify user permission.");
            });
    }

    /** 🔥 Thêm sự kiện xóa issue */
    function addDeleteEvent() {
        document.querySelectorAll(".delete-issue-btn").forEach(button => {
            button.addEventListener("click", function () {
                const issueId = this.getAttribute("data-id");
                const publisher = this.closest("tr").querySelector("td:nth-child(16)").textContent.trim(); // Lấy giá trị Publisher
                if (confirm(`Are you sure you want to delete issue with ID: ${issueId}?`)) {
                    deleteIssue(issueId, publisher);
                }
            });
        });
    }


    /** 🔹 Hàm đổi màu cho Status */
    function getStatusColorClass(status) {
        switch (status?.toLowerCase()) {
            case "open":
                return "bg-danger"; // đỏ
            case "on-going":
                return "bg-warning text-dark"; // vàng
            case "monitor":
                return "bg-info"; // xanh nhạt
            case "close":
                return "bg-success"; // xanh lá
            default:
                return "bg-secondary"; // màu xám cho trạng thái không xác định
        }
    }


    /** 🔹 Thêm sự kiện click vào ID */
    function addClickEventToIds() {
        document.querySelectorAll(".issue-link").forEach(link => {
            link.addEventListener("click", function (event) {
                event.preventDefault();
                const issueId = this.getAttribute("data-id");
                saveButton.classList.add("d-none");
                editButton.classList.remove("d-none");
                fetchIssueDetails(issueId);
            });
        });
    }

    /** 🔹 Gọi API lấy chi tiết issue */
    function fetchIssueDetails(issueId) {
        fetch("http://10.220.130.119:9090/api/CheckList/get-checklist", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({ id: issueId })
        })
            .then(response => response.json())
            .then(issue => {
                issueDetailsContainer.innerHTML = `
                    <p><strong>ID:</strong> ${issue.id}</p>
                    <p><strong>Publisher:</strong> ${issue.owner}</p>
                    <p><strong>Status:</strong><span data-editable="Status">${issue.status}</span></p>
                    <p><strong>Time:</strong> ${issue.time ? new Date(issue.time).toLocaleString() : "N/A"}</p>
                    <p><strong>Project:</strong> ${issue.project}</p>
                    <p><strong>Model Name:</strong> ${issue.modelName}</p>
                    <p><strong>Station:</strong> ${issue.station}</p>
                    <p><strong>Error:</strong> ${issue.error}</p>
                    <p><strong>Root Cause:</strong> ${issue.rootCause}</p>
                    <p><strong>FA:</strong><span data-editable="FA">${issue.fa}</span></p>
                    <p><strong>Action:</strong><span data-editable="Action">${issue.action}</span></p>
                    <p><strong>Picture:</strong></p>
                    ${issue.linkPicture ? `<img src="http://10.220.130.119:9090/ChecklistPicture/${issue.id}.png" alt="Issue Image" style="max-width:100%">` : ""}

                `;
                issueDetailsModal.show();
            })
            .catch(error => console.error("Error fetching issue details:", error));
    }


    /** 🔹 Nút Edit */
    editButton.addEventListener("click", function () {
        const issueId = document.querySelector("#issueDetails p strong").nextSibling.textContent.trim();
        const currentUsername = getCurrentUsername(); // Lấy username từ HTML

        if (!currentUsername) {
            alert("Failed to get the current username.");
            return;
        }

        // Gọi API để lấy chi tiết issue trước
        fetch("http://10.220.130.119:9090/api/CheckList/get-checklist", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({ id: issueId })
        })
            .then(response => response.json())
            .then(issue => {
                // Kiểm tra quyền chỉnh sửa
                checkEditPermission(issue, currentUsername);
            })
            .catch(error => {
                console.error("Error fetching issue details:", error);
                alert("Failed to fetch issue details.");
            });
    });

    /** 🔹 Kiểm tra quyền chỉnh sửa */
    function checkEditPermission(issue, currentUsername) {
        fetch(`http://10.220.130.119:9090/api/CheckList/check-department/${currentUsername}`)
            .then(response => response.json())
            .then(data => {
                const userDepartment = data.department;
                const owner = issue.owner?.trim();
                const publisher = issue.publisher?.trim();

                if (!owner || !publisher) {
                    alert("Error: Cannot find Owner or Publisher data.");
                    return;
                }

                // Kiểm tra quyền: Chỉ Owner hoặc Publisher mới được edit
                if (userDepartment === owner || userDepartment === publisher) {
                    enableEditing();
                } else {
                    alert("You do not have permission to edit this issue.");
                }
            })
            .catch(error => {
                console.error("Error checking department:", error);
                alert("Failed to verify user permission.");
            });
    }

    /** 🔹 Bật chế độ chỉnh sửa */
    function enableEditing() {
        editButton.classList.add("d-none");
        saveButton.classList.remove("d-none");

        issueDetailsContainer.querySelectorAll("[data-editable]").forEach(element => {
            let fieldName = element.getAttribute("data-editable");
            let value = element.innerText.trim();

            if (fieldName === "Status") {
                let selectField = document.createElement("select");
                selectField.className = "form-select form-select-sm";
                ["Open", "On-going", "Monitor", "Close"].forEach(status => {
                    let option = document.createElement("option");
                    option.value = status;
                    option.text = status;
                    if (status.toLowerCase() === value.toLowerCase()) {
                        option.selected = true;
                    }
                    selectField.appendChild(option);
                });
                element.innerHTML = "";
                element.appendChild(selectField);
            } else if (fieldName === "FA" || fieldName === "Action") {
                let textAreaField = document.createElement("textarea");
                textAreaField.className = "form-control form-control-sm";
                textAreaField.style.height = "80px"; // Mở rộng chiều cao để nhập nhiều hơn
                textAreaField.value = value;
                element.innerHTML = "";
                element.appendChild(textAreaField);
            } else {
                let inputField = document.createElement("input");
                inputField.type = "text";
                inputField.className = "form-control form-control-sm";
                inputField.value = value;
                element.innerHTML = "";
                element.appendChild(inputField);
            }
        });
    }


    /** 🔹 Nút Save */
    saveButton.addEventListener("click", function () {
        saveButton.classList.add("d-none");
        editButton.classList.remove("d-none");

        // Lấy ID của Issue
        const issueId = document.querySelector("#issueDetails p strong").nextSibling.textContent.trim();

        // Tạo đối tượng dữ liệu JSON để gửi
        let updatedIssue = {
            ID: issueId
        };

        // Lấy dữ liệu từ các trường nhập
        issueDetailsContainer.querySelectorAll("[data-editable]").forEach(element => {
            let inputField = element.querySelector("input, select, textarea");
            if (inputField) {
                let newValue = inputField.value.trim();
                let fieldName = element.getAttribute("data-editable");

                // Gửi dữ liệu theo trường cụ thể
                updatedIssue[fieldName] = newValue;

                // Hiển thị lại dữ liệu sau khi chỉnh sửa
                element.innerHTML = `<span data-editable="${fieldName}">${newValue}</span>`;
            }
        });


        // Gửi dữ liệu đến API
        fetch("http://10.220.130.119:9090/api/CheckList/update-issue", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(updatedIssue)
        })
            .then(response => response.json())
            .then(data => {
                alert("Issue updated successfully!");
                fetchChecklistData(); // Làm mới bảng dữ liệu
            })
            .catch(error => {
                console.error("Error updating issue:", error);
                alert("Error updating issue.");
            });

    });

    // phần search input
    const searchInput = document.querySelector(".search-bar");


    /** 🔍 Tìm kiếm trên toàn bộ dữ liệu */
    searchInput.addEventListener("keyup", function () {
        const query = searchInput.value.toLowerCase();

        if (query === "") {
            // Nếu không có từ khóa, hiển thị lại dữ liệu gốc
            data = [...originalData];
        } else {
            // Lọc dữ liệu dựa trên từ khóa tìm kiếm
            data = originalData.filter(issue => {
                return (
                    issue.id?.toString().toLowerCase().includes(query) ||
                    issue.owner?.toLowerCase().includes(query) ||
                    issue.status?.toLowerCase().includes(query) ||
                    issue.project?.toLowerCase().includes(query) ||
                    issue.modelName?.toLowerCase().includes(query) ||
                    issue.station?.toLowerCase().includes(query) ||
                    issue.error?.toLowerCase().includes(query) ||
                    issue.rootCause?.toLowerCase().includes(query) ||
                    issue.location?.toLowerCase().includes(query) ||
                    issue.action?.toLowerCase().includes(query) ||
                    issue.publisher?.toLowerCase().includes(query)
                );
            });
        }

        // Hiển thị dữ liệu đã được lọc hoặc khôi phục
        displayTable();
        setupPagination();
    });

    // Gọi API lấy dữ liệu khi load trang
    fetchChecklistData();
});

