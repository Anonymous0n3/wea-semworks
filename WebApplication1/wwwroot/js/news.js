document.addEventListener('DOMContentLoaded', () => {

    function applyFilter() {
        const filter = document.getElementById('categoryFilter');
        if (!filter) return;

        const selected = Array.from(filter.selectedOptions).map(o => o.value);
        const listItems = document.querySelectorAll('#newsList li');

        listItems.forEach(li => {
            const category = li.dataset.category;
            li.style.display = selected.length === 0 || selected.includes(category) ? '' : 'none';
        });
    }

    function reloadNewsWidget() {
        const container = document.querySelector('.news-widget');
        if (!container) return;

        fetch('/Widget/Load?name=NewsWidget')
            .then(res => res.text())
            .then(html => {
                container.innerHTML = html;

                // Znovu připoj listener pro filtr
                const filter = container.querySelector('#categoryFilter');
                if (filter) filter.addEventListener('change', applyFilter);

                // Znovu připoj listener pro reload
                const reloadBtn = container.querySelector('#reloadNews');
                if (reloadBtn) reloadBtn.addEventListener('click', reloadNewsWidget);

                // Filtruj podle aktuálního výběru
                applyFilter();
            })
            .catch(err => console.error('Chyba při načítání widgetu:', err));
    }

    // Inicializace listenerů při prvním načtení
    const filter = document.getElementById('categoryFilter');
    if (filter) filter.addEventListener('change', applyFilter);

    const reloadBtn = document.getElementById('reloadNews');
    if (reloadBtn) reloadBtn.addEventListener('click', reloadNewsWidget);
});