// news.js
function initNewsWidget(wrapper) {
    if (!wrapper) return;

    const filter = wrapper.querySelector('#categoryFilter');
    const newsList = wrapper.querySelector('#newsList');
    const reloadBtn = wrapper.querySelector('#reloadNews');

    if (!filter || !newsList || !reloadBtn) return;

    // Funkce pro aplikaci filtru
    function applyFilter() {
        const selected = Array.from(filter.selectedOptions).map(o => o.value);
        const listItems = newsList.querySelectorAll('li');

        listItems.forEach(li => {
            const category = li.dataset.category;
            li.style.display = selected.length === 0 || selected.includes(category) ? '' : 'none';
        });
    }

    // Funkce pro reload widgetu
    function reloadNewsWidget() {
        location.reload();
    }

    // Připojení listenerů
    filter.addEventListener('change', applyFilter);
    reloadBtn.addEventListener('click', reloadNewsWidget);

    // Aplikovat filtr při inicializaci podle aktuálního výběru
    applyFilter();
}

// Pokud chceš, můžeš rovnou inicializovat všechny existující widgety při načtení
document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('.news-widget').forEach(initNewsWidget);
});
