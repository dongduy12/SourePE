$(document).ready(function () {
    let pathHistory = ['D:\\DataCloud']; // Khởi tạo với đường dẫn mặc định
    let selectedItem = null; // Lưu thông tin item được click chuột phải
    const API_BASE = `${window.location.origin}/api/data`;

    // Hàm chuẩn hóa đường dẫn
    function normalizePath(path) {
        return path
            .replace(/\\+/g, '\\') // Loại bỏ dấu \\ thừa
            .replace(/\\$/, '') // Loại bỏ \\ ở cuối
            .replace(/^D:\\DataCloud\\DataCloud/, 'D:\\DataCloud'); // Loại bỏ lặp DataCloud
    }

    //Định dạng button
    const swalWithBootstrapButtons = Swal.mixin({
        customClass: {
            confirmButton: "btn btn-ne me-2",
            cancelButton: "btn btn-ne me-2"
        },
        buttonsStyling: false
    });


    // Tải dữ liệu từ server (bình thường)
    function loadData(path) {
        path = normalizePath(path); // Chuẩn hóa trước khi gửi
        console.log("Gửi yêu cầu với đường dẫn:", path);
        $.ajax({
            url: `${API_BASE}/get-data`,
            type: 'GET',
            data: { path: path },
            success: function (response) {
                console.log("Phản hồi từ server:", response);
                if (response && response.items && Array.isArray(response.items)) { // Sửa từ response.items thành response.Items
                    updateBreadcrumb(response.currentPath); // Sửa từ response.currentPath thành response.CurrentPath
                    $('#data-cloud-items').empty(); // Sử dụng #data-cloud-items để hiển thị file/folder
                    response.items.forEach(item => {
                        const icon = item.type === "Folder" ? "fas fa-folder" : "fas fa-file"; // Sửa item.type thành item.Type
                        const colElement = $('<div>').addClass('col');
                    const itemElement = $('<div>')
                        .addClass('data-item')
                        .attr('custom-path', normalizePath(item.path)) // Sửa item.path thành item.Path
                        .attr('custom-type', item.type) // Sửa item.type thành item.Type
                        .html(`
                                <i class="${icon}"></i>
                                <span title="${item.name}">${item.name}</span>
                            `);
                        colElement.append(itemElement);
                        $('#data-cloud-items').append(colElement);
                    });
                } else {
                    $('#data-cloud-items').html('<p class="text-muted">Không có dữ liệu để hiển thị.</p>');

                }
            },
            error: function (xhr) {
                console.error("Lỗi AJAX:", xhr.status, xhr.responseText);
                alert(`Không tìm thấy thư mục: ${path}. Vui lòng kiểm tra lại đường dẫn. (Lỗi: ${xhr.status} - ${xhr.responseText})`);
                swalWithBootstrapButtons.fire({
                    title: "Lỗi!",
                    text: `Không tìm thấy thư mục: ${path}! (Lỗi: ${xhr.status} - ${xhr.responseText})`,
                    icon: "error"
                });
            }
        });
    }

    // Tìm kiếm dữ liệu
    function searchData(keyword) {
        const currentPath = normalizePath(pathHistory[pathHistory.length - 1]);
        if (!keyword || keyword.trim() === '') {
            loadData(currentPath); // Nếu không có từ khóa, tải dữ liệu bình thường
            return;
        }

        console.log("Gửi yêu cầu tìm kiếm với từ khóa:", keyword);
        $.ajax({
            url: `${API_BASE}/search`,
            type: 'GET',
            data: {
                keyword: keyword,
                path: currentPath // Giới hạn tìm kiếm trong thư mục hiện tại
            },
            success: function (response) {
                console.log("Phản hồi từ server khi tìm kiếm:", response);
                if (response && response.items && Array.isArray(response.items)) { // Sửa từ response.items thành response.Items
                    updateBreadcrumb('Search Results'); // Cập nhật breadcrumb khi tìm kiếm
                    $('#data-cloud-items').empty();
                    if (response.items.length === 0) {
                        $('#data-cloud-items').html('<p class="text-muted">Không tìm thấy kết quả.</p>');
                    } else {
                        response.items.forEach(item => {
                            const icon = item.type === "Folder" ? "fas fa-folder" : "fas fa-file"; // Sửa item.type thành item.Type
                            const colElement = $('<div>').addClass('col');
                            const itemElement = $('<div>')
                                .addClass('data-item')
                                .attr('custom-path', normalizePath(item.path)) // Sửa item.path thành item.Path
                                .attr('custom-type', item.type) // Sửa item.type thành item.Type
                                .html(`
                                    <i class="${icon}"></i>
                                    <span title="${item.name}">${item.name}</span>
                                `);
                            colElement.append(itemElement);
                            $('#data-cloud-items').append(colElement);
                        });
                    }
                } else {
                    $('#data-cloud-items').html('<p class="text-muted">Không có dữ liệu để hiển thị.</p>');
                }
            },
            error: function (xhr) {
                console.error("Lỗi tìm kiếm:", xhr.status, xhr.responseText);
                swalWithBootstrapButtons.fire({
                    title: "Lỗi!",
                    text: `Lỗi khi tìm kiếm: ${xhr.status} - ${xhr.responseText}`,
                    icon: "error"
                });
            }
        });
    }

    // Cập nhật breadcrumb
    function updateBreadcrumb(path) {
        path = normalizePath(path);
        const parts = path.split('\\').filter(p => p); // Tách các phần của đường dẫn
        let breadcrumbHtml = '<a href="#" data-path="D:\\DataCloud">Home</a>';
        let currentPath = 'D:\\DataCloud';

        if (path !== 'Search Results') {
            for (let i = 1; i < parts.length; i++) {
                if (parts[i] === 'D:') continue;
                currentPath = `${currentPath}\\${parts[i]}`;
                breadcrumbHtml += ` > <a href="#" data-path="${currentPath}">${parts[i]}</a>`;
            }
        } else {
            breadcrumbHtml += ` > <span>Search Results</span>`;
        }
        $('#breadcrumb').html(breadcrumbHtml);
    }

    // Hiển thị menu ngữ cảnh
    function showContextMenu(event, item) {
        event.preventDefault(); // Ngăn hành vi mặc định của click chuột phải
        selectedItem = item; // Lưu item được chọn

        const contextMenu = $('#context-menu');
        const menuWidth = contextMenu.outerWidth();
        const menuHeight = contextMenu.outerHeight();
        const windowWidth = $(window).width();
        const windowHeight = $(window).height();

        // Lấy vị trí click chuột dựa trên viewport
        let posX = event.clientX;
        let posY = event.clientY;

        // Điều chỉnh vị trí nếu menu vượt ra ngoài màn hình
        if (posX + menuWidth > windowWidth) {
            posX -= menuWidth; // Hiển thị menu bên trái điểm click
        }
        if (posY + menuHeight > windowHeight) {
            posY -= menuHeight; // Hiển thị menu phía trên điểm click
        }

        // Đảm bảo menu không bị lệch ra ngoài mép trái hoặc trên
        if (posX < 0) posX = 0;
        if (posY < 0) posY = 0;

        // Đặt vị trí cho menu và hiển thị với hiệu ứng
        contextMenu.css({
            top: posY + 'px',
            left: posX + 'px'
        }).show().addClass('show');
    }

    // Ẩn menu khi click bên ngoài
    $(document).click(function (e) {
        if (!$(e.target).closest('.context-menu').length && !$(e.target).closest('.data-item').length) {
            $('#context-menu').hide();
        }
    });

    //// Xử lý hành động từ menu ngữ cảnh
    //$('#context-menu').on('click', '.context-menu-item', function () {
    //    const action = $(this).data('action');
    //    if (selectedItem) {
    //        const path = selectedItem.attr('custom-path');
    //        const type = selectedItem.attr('custom-type');

    //        if (action === 'download') {
    //            if (type === 'File') {
    //                window.location.href = `http://10.220.130.119:8000/api/data/download-file?path=${encodeURIComponent(path)}`;
    //            } else if (type === 'Folder') {
    //                window.location.href = `http://10.220.130.119:8000/api/data/download-folder?path=${encodeURIComponent(path)}`;
    //            }
    //        } else if (action === 'delete') {
    //            swalWithBootstrapButtons.fire({
    //                title: "Xác nhận xóa?",
    //                text: "Hành động không thể hoàn tác!",
    //                icon: "warning",
    //                showCancelButton: true,
    //                confirmButtonText: "XÁC NHẬN",
    //                cancelButtonText: "HỦY"
    //            }).then((result) => {
    //                if (result.isConfirmed) {
    //                    swalWithBootstrapButtons.fire({
    //                        title: "Đang xóa...",
    //                        allowOutsideClick: false,
    //                        didOpen: () => {
    //                            Swal.showLoading();
    //                        }
    //                    });
    //                    $.ajax({
    //                        url: 'http://10.220.130.119:8000/api/data/delete-items',
    //                        type: 'POST',
    //                        contentType: 'application/json',
    //                        data: JSON.stringify([{ path: path, type: type }]),
    //                        success: function (response) {
    //                            Swal.close();
    //                            swalWithBootstrapButtons.fire({
    //                                title: "Đã xóa!",
    //                                text: response.Message || "",
    //                                icon: "success"
    //                            });
    //                            loadData(normalizePath(pathHistory[pathHistory.length - 1]));
    //                        },
    //                        error: function (xhr) {
    //                            Swal.close();
    //                            swalWithBootstrapButtons.fire({
    //                                title: "Lỗi!",
    //                                text: `Không thể xóa ${xhr.status} - ${xhr.responseText}`,
    //                                icon: "error"
    //                            });
    //                        }
    //                    });
    //                } else if (result.dismiss === Swal.DismissReason.cancel) {
    //                    swalWithBootstrapButtons.fire({
    //                        title: "Đã hủy",
    //                        text: "",
    //                        icon: "info"
    //                    });
    //                }
    //            });
    //        }
    //        $('#context-menu').hide(); // Ẩn menu sau khi thực hiện hành động
    //    }
    //});

    // Xử lý hành động từ menu ngữ cảnh

    $('#context-menu').on('click', '.context-menu-item', function () {
        const action = $(this).data('action');
        if (selectedItem) {
            const path = selectedItem.attr('custom-path');
            const type = selectedItem.attr('custom-type');
            const fileUrl = type === 'File'
                ? `${API_BASE}/download-file?path=${encodeURIComponent(path)}`
                : `${API_BASE}/download-folder?path=${encodeURIComponent(path)}`;

            if (action === 'download') {
                swalWithBootstrapButtons.fire({
                    title: "Đang tải xuống...",
                    text: `Đang chuẩn bị tải ${type === 'File' ? 'tệp' : 'thư mục'}...`,
                    allowOutsideClick: false,
                    didOpen: () => {
                        Swal.showLoading();
                    }
                });
                const iframe = $('<iframe>').css('display', 'none').attr('src', fileUrl);
                $('body').append(iframe);
                setTimeout(() => {
                    Swal.close();
                    swalWithBootstrapButtons.fire({
                        title: "Thành công!",
                        text: `${type === 'File' ? 'Tệp' : 'Thư mục'} đã được tải xuống.`,
                        icon: "success"
                    });
                    iframe.remove();
                }, 1000);
            } else if (action === 'delete') {
                swalWithBootstrapButtons.fire({
                    title: "Bạn có chắc chắn?",
                    text: "Hành động này không thể hoàn tác!",
                    icon: "warning",
                    showCancelButton: true,
                    confirmButtonText: "Vâng, xóa nó!",
                    cancelButtonText: "Không, hủy!",
                    reverseButtons: true
                }).then((result) => {
                    if (result.isConfirmed) {
                        $.ajax({
                            url: `${API_BASE}/delete-items`,
                            type: 'POST',
                            contentType: 'application/json',
                            data: JSON.stringify([{ path: path, type: type }]),
                            success: function (response) {
                                swalWithBootstrapButtons.fire({
                                    title: "Đã xóa!",
                                    text: response.Message || "Mục của bạn đã được xóa.",
                                    icon: "success"
                                });
                                loadData(normalizePath(pathHistory[pathHistory.length - 1]));
                            },
                            error: function (xhr) {
                                console.error("Lỗi xóa:", xhr.status, xhr.responseText);
                                swalWithBootstrapButtons.fire({
                                    title: "Lỗi!",
                                    text: `Không thể xóa mục: ${xhr.status} - ${xhr.responseText}`,
                                    icon: "error"
                                });
                            }
                        });
                    } else if (result.dismiss === Swal.DismissReason.cancel) {
                        swalWithBootstrapButtons.fire({
                            title: "Đã hủy",
                            text: ":) :)",
                            icon: "info"
                        });
                    }
                });
            } else if (action === 'preview') {
                previewFile(path, fileUrl);
            } else if (action === 'preview-new-tab') {
                openFileInNewTab(path, fileUrl);
            }
            $('#context-menu').hide();
        }
    });

    function previewFile(path, fileUrl) {
        const fileName = path.split('\\').pop().toLowerCase();

        swalWithBootstrapButtons.fire({
            title: "Đang tải...",
            text: "Vui lòng đợi trong khi tệp được tải để xem trước.",
            allowOutsideClick: false,
            didOpen: () => {
                Swal.showLoading();
            }
        });

        if (fileName.endsWith('.pdf')) {
            $('#pptViewer').hide();
            $('#pdfViewer').show();
            const viewerUrl = `/lib/pdfjs/web/viewer.html?file=${encodeURIComponent(fileUrl)}`;
            $('#pdfViewer').attr('src', viewerUrl);

            setTimeout(() => {
                Swal.close();
                $('#previewModal').modal('show');
            }, 1000);
        } else if (fileName.endsWith('.ppt') || fileName.endsWith('.pptx')) {
            $('#pdfViewer').hide();
            $('#pptViewer').show().empty();

            $.ajax({
                url: `${API_BASE}/render-pptx`,
                type: 'GET',
                data: { path: path },
                success: function (response) {
                    if (response && response.slides && response.slides.length > 0) {
                        response.slides.forEach((slideUrl, index) => {
                            const img = $('<img>')
                                .attr('src', slideUrl)
                                .css({
                                    'width': '100%',
                                    'margin-bottom': '10px',
                                    'display': 'block'
                                })
                                .attr('alt', `Slide ${index + 1}`);
                            $('#pptViewer').append(img);
                        });

                        $('#pptViewer').prepend(`
                            <div class="d-flex justify-content-between mb-2">
                                <button id="zoomIn" class="btn btn-secondary">Phóng to</button>
                                <button id="zoomOut" class="btn btn-secondary">Thu nhỏ</button>
                            </div>
                        `);

                        let zoomLevel = 1;
                        $('#zoomIn').click(function () {
                            zoomLevel += 0.1;
                            $('#pptViewer img').css('width', `${100 * zoomLevel}%`);
                        });
                        $('#zoomOut').click(function () {
                            if (zoomLevel > 0.5) {
                                zoomLevel -= 0.1;
                                $('#pptViewer img').css('width', `${100 * zoomLevel}%`);
                            }
                        });

                        Swal.close();
                        $('#previewModal').modal('show');
                    } else {
                        Swal.close();
                        swalWithBootstrapButtons.fire({
                            title: "Lỗi!",
                            text: "Không thể render tệp PPT/PPTX.",
                            icon: "error"
                        });
                    }
                },
                error: function (xhr) {
                    Swal.close();
                    swalWithBootstrapButtons.fire({
                        title: "Lỗi!",
                        text: `Không thể render tệp PPT/PPTX: ${xhr.status} - ${xhr.responseText}`,
                        icon: "error"
                    });
                }
            });
        } else {
            Swal.close();
            swalWithBootstrapButtons.fire({
                title: "Không hỗ trợ",
                text: "Chỉ hỗ trợ xem trước tệp PDF và PowerPoint (.ppt, .pptx).",
                icon: "warning"
            });
        }
    }

    function openFileInNewTab(path, fileUrl) {
        const fileName = path.split('\\').pop().toLowerCase();

        if (fileName.endsWith('.pdf')) {
            const viewerUrl = `/lib/pdfjs/web/viewer.html?file=${encodeURIComponent(fileUrl)}`;
            window.open(viewerUrl, '_blank');
        } else if (fileName.endsWith('.ppt') || fileName.endsWith('.pptx')) {
            const newWindow = window.open('', '_blank');
            swalWithBootstrapButtons.fire({
                title: "Đang tải...",
                text: "Vui lòng đợi trong khi tệp được tải để xem trước.",
                allowOutsideClick: false,
                didOpen: () => { Swal.showLoading(); }
            });
            $.ajax({
                url: `${API_BASE}/render-pptx`,
                type: 'GET',
                data: { path: path },
                success: function (response) {
                    Swal.close();
                    if (response && response.slides && response.slides.length > 0) {
                        let html = '<html><head><title>Preview PPT</title></head><body style="margin:0; padding:10px;">';
                        response.slides.forEach(slideUrl => {
                            html += `<img src="${slideUrl}" style="width:100%;margin-bottom:10px;display:block;"/>`;
                        });
                        html += '</body></html>';
                        newWindow.document.write(html);
                        newWindow.document.close();
                    } else {
                        newWindow.close();
                        swalWithBootstrapButtons.fire({
                            title: "Lỗi!",
                            text: "Không thể render tệp PPT/PPTX.",
                            icon: "error"
                        });
                    }
                },
                error: function (xhr) {
                    newWindow.close();
                    Swal.close();
                    swalWithBootstrapButtons.fire({
                        title: "Lỗi!",
                        text: `Không thể render tệp PPT/PPTX: ${xhr.status} - ${xhr.responseText}`,
                        icon: "error"
                    });
                }
            });
        } else {
            window.open(fileUrl, '_blank');
        }
    }


    $('#previewModal').on('hidden.bs.modal', function () {
        // Gửi yêu cầu xóa thư mục tạm (nếu cần)
        $.ajax({
            url: `${API_BASE}/cleanup-temp`,
            type: 'POST',
            data: JSON.stringify({ tempDir: $('#pptViewer img').first().attr('src')?.split('/').slice(0, -1).join('/') }),
            success: function () {
                console.log("Đã xóa thư mục tạm.");
            }
        });
    });


    // Sự kiện click chuột phải vào data-item
    $('#data-cloud-items').on('contextmenu', '.data-item', function (e) {
        showContextMenu(e, $(this));
    });

    // Sự kiện click vào breadcrumb
    $('#breadcrumb').on('click', 'a', function (e) {
        e.preventDefault();
        const path = normalizePath($(this).data('path'));
        const index = pathHistory.indexOf(path);
        if (index !== -1) {
            pathHistory = pathHistory.slice(0, index + 1);
        } else if (path !== pathHistory[0]) {
            pathHistory.push(path);
        }
        console.log("pathHistory sau khi click breadcrumb:", pathHistory);
        loadData(path);
    });

    // Sự kiện click vào data-item
    $('#data-cloud-items').on('click', '.data-item', function (e) {
        const path = normalizePath($(this).attr('custom-path'));
        const type = $(this).attr('custom-type');
        if (type === "Folder") {
            if (!pathHistory.includes(path) && path !== pathHistory[0]) {
                pathHistory.push(path);
            }
            console.log("pathHistory sau khi click data-item:", pathHistory);
            loadData(path);
        }
    });

    $('#data-cloud-items').on('dblclick', '.data-item', function () {
        const path = normalizePath($(this).attr('custom-path'));
        const type = $(this).attr('custom-type');
        if (type === 'File') {
            const fileUrl = `${API_BASE}/download-file?path=${encodeURIComponent(path)}`;
            previewFile(path, fileUrl);
        }
    });

    // Nút "Back"
    $('#back-btn').click(function () {
        if (pathHistory.length > 1) {
            pathHistory.pop();
            const previousPath = normalizePath(pathHistory[pathHistory.length - 1]);
            console.log("pathHistory sau khi back:", pathHistory);
            loadData(previousPath);
        } else {
            alert("Không có thư mục trước để quay lại!");
        }
    });


    $('#new-folder-btn').click(function () {
        const currentPath = normalizePath(pathHistory[pathHistory.length - 1]);
        swalWithBootstrapButtons.fire({
            title: "Tạo thư mục mới",
            input: "text",
            inputLabel: null,
            inputPlaceholder: "Tên thư mục",
            showCancelButton: true,
            confirmButtonText: "XÁC NHẬN",
            cancelButtonText: "HỦY"
        }).then((result) => {
            if (result.isConfirmed && result.value) {
                $.ajax({
                    url: `${API_BASE}/create-folder`,
                    type: 'POST',
                    data: { path: currentPath, folderName: result.value },
                    success: function (response) {
                        swalWithBootstrapButtons.fire({
                            title: "Thành công!",
                            text: response.Message || "Thư mục đã được tạo.",
                            icon: "success"
                        });
                        loadData(currentPath);
                    },
                    error: function (xhr) {
                        swalWithBootstrapButtons.fire({
                            title: "Lỗi!",
                            text: `Không thể tạo thư mục: ${xhr.status} - ${xhr.responseText}`,
                            icon: "error"
                        });
                    }
                });
            }
        });
    });

    // Tải lên tệp
    $('#upload-file-btn').click(function () {
        $('#file-input').click();
    });

    $('#file-input').change(function () {
        const files = this.files;
        if (!files.length) return;

        // Kiểm tra kích thước tệp
        const maxSize = 100 * 1024 * 1024; // 100MB
        for (let file of files) {
            if (file.size > maxSize) {
                swalWithBootstrapButtons.fire({
                    title: "Lỗi!",
                    text: `Tệp ${file.name} quá lớn. Kích thước tối đa là 100MB.`,
                    icon: "error"
                });
                return;
            }
        }

        const currentPath = normalizePath(pathHistory[pathHistory.length - 1]);
        const formData = new FormData();
        for (let file of files) {
            formData.append('files', file);
        }
        formData.append('path', currentPath);

        //Hiển thị loading
        swalWithBootstrapButtons.fire({
            title: "Đang tải lên...",
            text: "",
            allowOutsideClick: false,
            didOpen: () => {
                Swal.showLoading();
            }
        });
        $.ajax({
            url: `${API_BASE}/upload-file`,
            type: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            success: function (response) {
                Swal.close();
                swalWithBootstrapButtons.fire({
                    title: "Thành công!",
                    text: response.Message || "Tệp đã được tải lên!",
                    icon: "success"
                });
                loadData(currentPath);
            },
            error: function (xhr) {
                Swal.close();
                console.error("Lỗi tải lên tệp:", xhr.status, xhr.responseText);
                swalWithBootstrapButtons.fire({
                    title: "Lỗi!",
                    text: `Không thể tải lên tệp: ${xhr.status} - ${xhr.responseText}`,
                    icon: "error"
                });
            }
        });
    });

    // Tải lên thư mục
    $('#upload-folder-btn').click(function () {
        $('#folder-input').click();
    });

    $('#folder-input').change(function () {
        const files = this.files;
        if (!files.length) return;

        // Kiểm tra kích thước tệp
        const maxSize = 100 * 1024 * 1024; // 100MB
        for (let file of files) {
            if (file.size > maxSize) {
                swalWithBootstrapButtons.fire({
                    title: "Lỗi!",
                    text: `Tệp ${file.name} quá lớn. Kích thước tối đa là 100MB.`,
                    icon: "error"
                });
                return;
            }
        }

        const currentPath = normalizePath(pathHistory[pathHistory.length - 1]);
        const formData = new FormData();
        for (let file of files) {
            formData.append('files', file, file.webkitRelativePath || file.name);
        }
        formData.append('path', currentPath);

        swalWithBootstrapButtons.fire({
            title: "Đang tải lên...",
            text: "",
            allowOutsideClick: false,
            didOpen: () => {
                Swal.showLoading();
            }
        });

        $.ajax({
            url: `${API_BASE}/upload-folder`,
            type: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            success: function (response) {
                Swal.close();
                swalWithBootstrapButtons.fire({
                    title: "Thành công!",
                    text: response.Message || "Thư mục đã được tải lên.",
                    icon: "success"
                });
                loadData(currentPath);
            },
            error: function (xhr) {
                Swal.close();
                console.error("Lỗi tải lên thư mục:", xhr.status, xhr.responseText);
                swalWithBootstrapButtons.fire({
                    title: "Lỗi!",
                    text: `Không thể tải lên thư mục: ${xhr.status} - ${xhr.responseText}`,
                    icon: "error"
                });
            }
        });
    });

    // Hỗ trợ kéo-thả (vẫn áp dụng cho #data-cloud-results)
    $('#data-cloud-results')
        .on('dragover', function (e) {
            e.preventDefault();
            $(this).addClass('dragover');
        })
        .on('dragleave', function (e) {
            e.preventDefault();
            $(this).removeClass('dragover');
        })
        .on('drop', function (e) {
            e.preventDefault();
            $(this).removeClass('dragover');

            const files = e.originalEvent.dataTransfer.files;
            if (!files.length) return;
            // Kiểm tra kích thước tệp
            const maxSize = 400 * 1024 * 1024; // 100MB
            for (let file of files) {
                if (file.size > maxSize) {
                    swalWithBootstrapButtons.fire({
                        title: "Lỗi!",
                        text: `Tệp ${file.name} quá lớn. Kích thước tối đa là 400MB.`,
                        icon: "error"
                    });
                    return;
                }
            }


            const currentPath = normalizePath(pathHistory[pathHistory.length - 1]);
            const formData = new FormData();

            for (let file of files) {
                formData.append('files', file, file.webkitRelativePath || file.name);
            }
            formData.append('path', currentPath);

            swalWithBootstrapButtons.fire({
                title: "Đang tải lên...",
                text: "Vui lòng đợi trong khi dữ liệu được tải lên.",
                allowOutsideClick: false,
                didOpen: () => {
                    Swal.showLoading();
                }
            });

            $.ajax({
                url: `${API_BASE}/upload-folder`,
                type: 'POST',
                data: formData,
                processData: false,
                contentType: false,
                success: function (response) {
                    Swal.close();
                    swalWithBootstrapButtons.fire({
                        title: "Thành công",
                        text: response.Message || "Dữ liệu đã được tải lên.",
                        icon: "success"
                    });
                    loadData(currentPath);
                },
                error: function (xhr) {
                    Swal.close();
                    console.error("Lỗi kéo-thả:", xhr.status, xhr.responseText);
                    swalWithBootstrapButtons.fire({
                        title: "Lỗi!",
                        text: `Không thể tải lên dữ liệu: ${xhr.status} - ${xhr.responseText}`,
                        icon: "error"
                    });
                }
            });
        });

    // Chuyển đổi bố cục
    $('#grid-view-btn').click(function () {
        $('#data-cloud-items').removeClass('list-view');
    });

    $('#list-view-btn').click(function () {
        $('#data-cloud-items').addClass('list-view');
    });

    // Sự kiện tìm kiếm
    let debounceTimer;
    $('#search-input').on('input', function () {
        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(() => {
            const keyword = $(this).val().trim();
            searchData(keyword);
        }, 300); // Chờ 300ms sau khi ngừng nhập
    });

    // Tải dữ liệu ban đầu
    loadData('D:\\DataCloud');
});