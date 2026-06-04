document.addEventListener("DOMContentLoaded", function () {
    const dropZone = document.getElementById('dropZone');
    const fileInput = document.getElementById('uploadFile');
    const previewContainer = document.getElementById('previewContainer');
    const imagePreview = document.getElementById('imagePreview');
    const uploadPlaceholder = document.getElementById('uploadPlaceholder');

    if (!dropZone || !fileInput) return;
    dropZone.addEventListener('click', () => fileInput.click());
    fileInput.addEventListener('change', handleFiles);

    dropZone.addEventListener('dragover', (e) => {
        e.preventDefault();
        dropZone.style.borderColor = 'var(--primary)';
        dropZone.style.backgroundColor = 'rgba(99, 102, 241, 0.1)';
    });

    dropZone.addEventListener('dragleave', (e) => {
        e.preventDefault();
        dropZone.style.borderColor = 'var(--border-color)';
        dropZone.style.backgroundColor = 'rgba(255,255,255,0.01)';
    });

    dropZone.addEventListener('drop', (e) => {
        e.preventDefault();
        dropZone.style.borderColor = 'var(--border-color)';
        dropZone.style.backgroundColor = 'rgba(255,255,255,0.01)';

        if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
            fileInput.files = e.dataTransfer.files;
            handleFiles();
        }
    });

    document.addEventListener('paste', (e) => {
        if (e.clipboardData.files && e.clipboardData.files.length > 0) {
            const file = e.clipboardData.files[0];
            if (file.type.startsWith('image/')) {
                fileInput.files = e.clipboardData.files;
                handleFiles();

                dropZone.style.borderColor = '#10b981';
                setTimeout(() => dropZone.style.borderColor = 'var(--border-color)', 500);
            }
        }
    });

    function handleFiles() {
        if (fileInput.files && fileInput.files[0]) {
            const reader = new FileReader();
            reader.onload = function (e) {
                if (imagePreview) imagePreview.src = e.target.result;
                if (previewContainer) previewContainer.classList.remove('d-none');
                if (uploadPlaceholder) {
                    const p = uploadPlaceholder.querySelector('p');
                    if (p) p.innerText = 'Зображення готове! Натисніть, щоб змінити.';
                }
            }
            reader.readAsDataURL(fileInput.files[0]);
        }
    }
});