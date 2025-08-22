document.addEventListener('DOMContentLoaded', function () {
    const boxes = document.querySelectorAll('.flowchart-box');

    if (boxes.length === 0) {
        console.warn('No flowchart boxes found.');
        return;
    }

    boxes.forEach(box => {
        const redirectUrl = box.getAttribute('data-url');

        // Chỉ áp dụng sự kiện click cho các khối 2, 5, và 8
        if (redirectUrl === '/Scrap/Function') {
            box.addEventListener('click', function () {
                // Xác định giá trị search-options dựa trên khối được click
                let searchOptionValue = '';
                if (box.textContent.includes('Load SN list into system')) { // Khối 2
                    searchOptionValue = 'INPUT_SN';
                } else if (box.textContent.includes('System save SN list to database')) { // Khối 5
                    searchOptionValue = 'CREATE_TASK_FORM';
                } else if (box.textContent.includes('After get task/PO from NV')) { // Khối 8
                    searchOptionValue = 'UPDATE_DATA';
                }

                // Lưu giá trị searchOptionValue vào localStorage
                localStorage.setItem('searchOptionValue', searchOptionValue);

                // Chuyển hướng đến trang /Scrap/Function
                window.location.href = redirectUrl;
            });
        }
        // Các khối khác không có sự kiện click
    });
});