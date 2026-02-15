// Функция для переключения деталей
function toggleDetails(button, id) {
    const detailsRow = document.getElementById(`details-${id}`);
    const isExpanded = detailsRow.style.display !== 'none';

    if (isExpanded) {
        // Скрываем
        detailsRow.style.display = 'none';
        button.textContent = button.dataset.textShow || 'Показать детали';
        button.classList.remove('active');
    } else {
        // Показываем текущие детали (не закрывая другие)
        detailsRow.style.display = 'table-row';
        button.textContent = button.dataset.textHide || 'Скрыть детали';
        button.classList.add('active');

        // Плавный скролл к деталям (опционально)
        setTimeout(() => {
            detailsRow.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        }, 100);
    }
}

// Функция для открытия всех деталей (опционально)
function expandAll() {
    document.querySelectorAll('.details-row').forEach(row => {
        const id = row.id.replace('details-', '');
        const button = document.querySelector(`[onclick*="toggleDetails(this, ${id})"]`);
        if (button && row.style.display === 'none') {
            row.style.display = 'table-row';
            button.textContent = button.dataset.textHide || 'Скрыть детали';
            button.classList.add('active');
        }
    });
}

// Функция для закрытия всех деталей
function collapseAll() {
    document.querySelectorAll('.details-row').forEach(row => {
        const id = row.id.replace('details-', '');
        const button = document.querySelector(`[onclick*="toggleDetails(this, ${id})"]`);
        if (button && row.style.display !== 'none') {
            row.style.display = 'none';
            button.textContent = button.dataset.textShow || 'Показать детали';
            button.classList.remove('active');
        }
    });
}

// Инициализация при загрузке страницы
document.addEventListener('DOMContentLoaded', () => {
    // Убеждаемся, что все детали скрыты
    document.querySelectorAll('.details-row').forEach(row => {
        row.style.display = 'none';
    });
});