(function () {
    document.addEventListener('DOMContentLoaded', () => {
        setupSidebar();
    });

    function setupSidebar() {
        const sidebar = document.getElementById('sidebar');
        const mainContent = document.getElementById('mainContent');
       const header = document.querySelector('.header');
        const toggleCheckbox = document.getElementById('toggleSidebarFixed');
        const sidebarToggleBtn = document.getElementById('sidebarToggle');

        if (!sidebar || !mainContent || !header || !toggleCheckbox) {
            console.error('Không tìm thấy một trong các phần tử: sidebar, mainContent, header, toggleCheckbox');
            return;
        }
        const isMobile = () => window.innerWidth < 768;

        if (sidebarToggleBtn) {
            sidebarToggleBtn.addEventListener('click', () => {
                sidebar.classList.toggle('mobile-show');
            });
        }

        // Khởi tạo trạng thái từ localStorage
        const isSidebarFixed = localStorage.getItem('isSidebarFixed') === 'true';
        toggleCheckbox.checked = isSidebarFixed;
        setSidebarState(isSidebarFixed);

        // Checkbox thay đổi
        toggleCheckbox.addEventListener('change', (event) => {
            const isChecked = event.target.checked;
            localStorage.setItem('isSidebarFixed', isChecked);
            setSidebarState(isChecked);
        });

        // Hover effect khi không cố định
        sidebar.addEventListener('mouseenter', () => {
            if (!toggleCheckbox.checked && !isMobile()) {
                sidebar.classList.add('sidebar-hover'); // Chỉ mở rộng sidebar
                document.querySelectorAll('#sidebar .nav-link span, #sidebar .dashboard-container span').forEach(span => {
                    span.style.opacity = '1';
                });
            }
        });

        sidebar.addEventListener('mouseleave', () => {
            if (!toggleCheckbox.checked && !isMobile()) {
                sidebar.classList.remove('sidebar-hover');
                document.querySelectorAll('#sidebar .nav-link span, #sidebar .dashboard-container span').forEach(span => {
                    span.style.opacity = '0';
                });
            }
        });

        window.addEventListener('resize', () => {
            setSidebarState(toggleCheckbox.checked);
        });

        function setSidebarState(isFixed) {
            if (isMobile()) {
                sidebar.classList.remove('open', 'sidebar-hover');
                document.documentElement.classList.remove('sidebar-expanded');
                sidebar.style.width = '';
                mainContent.style.marginLeft = '0';
                header.style.left = '0';
                header.style.width = '100%';
                return;
            }
            sidebar.classList.remove('mobile-show');
            if (isFixed) {
                sidebar.classList.remove('sidebar-hover'); // Xóa trạng thái hover nếu có
                sidebar.classList.add('open');
                document.documentElement.classList.add('sidebar-expanded');
                sidebar.style.width = '260px';
                mainContent.style.marginLeft = '260px';
                header.style.left = '260px';
                header.style.width = 'calc(100% - 260px)';
                document.querySelectorAll('#sidebar .nav-link span, #sidebar .dashboard-container span').forEach(span => {
                    span.style.opacity = '1';
                });
            } else {
                sidebar.classList.remove('open');
                document.documentElement.classList.remove('sidebar-expanded');
                sidebar.style.width = '70px';
                mainContent.style.marginLeft = '70px';
                header.style.left = '70px';
                header.style.width = 'calc(100% - 70px)';
                document.querySelectorAll('#sidebar .nav-link span, #sidebar .dashboard-container span').forEach(span => {
                    span.style.opacity = '0';
                });
            }
            // Đảm bảo transition hoạt động sau khi thay đổi trạng thái
            sidebar.style.transition = 'width 0.25s ease';
            mainContent.style.transition = 'margin-left 0.25s ease';
            header.style.transition = 'left 0.25s ease, width 0.25s ease';
            document.querySelectorAll('#sidebar .nav-link span, #sidebar .dashboard-container span').forEach(span => {
                span.style.transition = 'opacity 0.25s ease';
            });
        }
    }

    // Hàm toggle search (giữ nguyên)
    window.toggleSearch = function () {
        const searchContainer = document.getElementById('searchContainer');
        searchContainer.classList.toggle('expanded');
    };
})();

// Sử dụng SweetAlert2 cho thông báo
function showError(message) {
    Swal.fire({
        icon: 'error',
        title: 'Lỗi',
        text: message,
        confirmButtonText: 'OK',
        confirmButtonColor: '#3085d6',
    });
}

//function showSuccess(message) {
//    Swal.fire({
//        icon: 'success',
//        title: 'Thành công',
//        text: message,
//        confirmButtonText: 'OK',
//        confirmButtonColor: '#3085d6',
//    });
//}

function showSuccess(message) {
    Swal.fire({
        title: message,
        icon: "success",
        draggable: true
    });
}
function showWarning(message) {
    Swal.fire({
        title: message,
        icon: "warning",
        draggable: true
    });
}
function showInfo(message) {
    Swal.fire({
        title: message,
        icon: "info",
        draggable: true
    });
}

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

