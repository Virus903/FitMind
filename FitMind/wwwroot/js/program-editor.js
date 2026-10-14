(() => {
    const form = document.querySelector('[data-program-editor]');
    if (!form) return;
    const days = form.querySelector('[data-days]');
    const status = form.querySelector('[data-editor-status]');
    function reindex() {
        [...days.children].forEach((day, d) => {
            day.querySelector('[data-day-heading]').textContent = `Тренировочный день ${d + 1}`;
            day.querySelectorAll('input, select').forEach(input => {
                const row = input.closest('[data-exercise]');
                const field = input.dataset.field || input.name.split('.').pop();
                input.dataset.field = field;
                const e = row ? [...day.querySelector('[data-exercises]').children].indexOf(row) : -1;
                input.name = `Days[${d}].${row ? `Exercises[${e}].` : ''}${field}`;
                input.id = input.name.replace(/[.\[\]]/g, '_');
            });
        });
        form.querySelector('[aria-describedby="days-help"]').value = days.children.length;
    }
    form.addEventListener('click', event => {
        const button = event.target.closest('button');
        if (!button) return;
        const day = button.closest('[data-day]');
        const row = button.closest('[data-exercise]');
        if (button.hasAttribute('data-add-day')) {
            if (days.children.length >= 7) { status.textContent = 'Можно добавить до 7 дней.'; return; }
            const fragment = document.querySelector('#program-day-template').content.cloneNode(true);
            fragment.querySelector('[data-field="Name"]').value = `День ${days.children.length + 1}`;
            const used = new Set([...days.querySelectorAll('select[name$=".DayOfWeek"]')].map(x => x.value));
            fragment.querySelector('[data-field="DayOfWeek"]').value = [1,2,3,4,5,6,0].find(x => !used.has(String(x)));
            days.append(fragment);
        } else if (button.hasAttribute('data-add-exercise')) {
            const rows = day.querySelector('[data-exercises]');
            if (rows.children.length >= 50) { status.textContent = 'Можно добавить до 50 упражнений в день.'; return; }
            rows.append(document.querySelector('#program-exercise-template').content.cloneNode(true));
        } else if (button.hasAttribute('data-remove-day')) {
            if (days.children.length <= 1) { status.textContent = 'Оставьте хотя бы один тренировочный день.'; return; }
            if (!window.confirm('Убрать этот день из редактируемой программы?')) return;
            day.remove();
        } else if (button.hasAttribute('data-remove-exercise')) {
            if (!window.confirm('Убрать упражнение из этого дня?')) return;
            row.remove();
        } else if (button.hasAttribute('data-up') && row.previousElementSibling) {
            row.previousElementSibling.before(row);
        } else if (button.hasAttribute('data-down') && row.nextElementSibling) {
            row.nextElementSibling.after(row);
        } else return;
        reindex();
        status.textContent = 'Изменения внесены в форму. Нажмите «Сохранить программу», чтобы записать их.';
    });
    form.addEventListener('submit', reindex);
    // Поиск: скрывает в списках упражнений варианты, не подходящие под введённый текст (выбранные остаются).
    const search = form.querySelector('[data-exercise-search]');
    function filter() {
        const q = (search?.value || '').trim().toLowerCase();
        form.querySelectorAll('select[name$=".ExerciseId"], select[data-field="ExerciseId"]').forEach(select => {
            select.querySelectorAll('option').forEach(option => {
                option.hidden = !!q && !!option.value && !option.selected && !option.textContent.toLowerCase().includes(q);
            });
        });
    }
    search?.addEventListener('input', filter);
    form.addEventListener('click', () => setTimeout(filter));
    // Предпросмотр обложки: выбранная из списка или загруженная.
    const preview = form.querySelector('[data-cover-preview]');
    form.querySelector('[data-cover-select]')?.addEventListener('change', e => { if (preview) preview.src = e.target.value || '/images/covers/unavailable.svg'; });
    form.querySelector('input[type="file"]')?.addEventListener('change', e => { const f = e.target.files?.[0]; if (f && preview) preview.src = URL.createObjectURL(f); });
    reindex();
})();
