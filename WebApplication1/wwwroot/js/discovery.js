let currentPage = 1;

document.addEventListener('DOMContentLoaded', async () => {
    const token = localStorage.getItem('jwtToken');
    const userEmail = localStorage.getItem('userEmail');

    // Řízení sekce Oblíbené
    const favSection = document.getElementById('favoritesSection');
    if (token) {
        favSection.classList.remove('d-none');
        await loadFavorites(token, userEmail);
    } else {
        favSection.classList.add('d-none');
    }

    // Načtení veřejného seznamu
    await loadPublicList(1);
});

async function changePage(delta) {
    if (currentPage + delta < 1) return;
    currentPage += delta;
    document.getElementById('pageIndicator').innerText = `Strana ${currentPage}`;
    await loadPublicList(currentPage);
}

// --- NAČÍTÁNÍ DAT ---

async function loadFavorites(token, userEmail) {
    const container = document.getElementById('favoritesContainer');
    try {
        const resp = await fetch('/api/PublicWidgets/liked', {
            headers: { 'Authorization': 'Bearer ' + token }
        });
        if (resp.ok) {
            const data = await resp.json();
            if (data.length === 0) container.innerHTML = '<div class="col-12 text-muted fst-italic">Zatím nemáte žádné oblíbené widgety.</div>';
            else container.innerHTML = data.map(w => renderCard(w, userEmail, token)).join('');
        }
    } catch (e) { console.error(e); }
}

async function loadPublicList(page) {
    const container = document.getElementById('publicContainer');
    const search = document.getElementById('discoSearch').value;
    const author = document.getElementById('discoAuthor').value;
    const token = localStorage.getItem('jwtToken');
    const userEmail = localStorage.getItem('userEmail');

    const filter = {
        searchName: search,
        author: author,
        page: page,
        pageSize: 12,
        sortBy: "date"
    };

    try {
        const resp = await fetch('/api/PublicWidgets/list', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(filter)
        });
        if (resp.ok) {
            const data = await resp.json();
            if (data.length === 0 && page > 1) { currentPage--; return; }
            container.innerHTML = data.map(w => renderCard(w, userEmail, token)).join('');
        }
    } catch (e) { console.error(e); }
}

// --- VYKRESLOVÁNÍ KARTIČKY ---

function renderCard(w, currentUserEmail, token) {
    const isAuthor = w.authorEmail === currentUserEmail;
    // Escapování dat pro vložení do onclick
    const widgetDataStr = JSON.stringify(w.widgetData).replace(/"/g, '&quot;');
    const widgetType = w.widgetType;

    // Logika pro Srdíčko (Like)
    let likeBtn = '';
    let heartIcon = '🤍'; // Prázdné srdce defaultně

    // Pokud je uživatel přihlášen, zkontrolujeme, jestli už lajkoval
    if (token) {
        // w.likedBy může být null, pokud je pole prázdné v DB
        const likedBy = w.likedBy || [];
        if (likedBy.includes(currentUserEmail)) {
            heartIcon = '❤️'; // Plné srdce
        }

        if (!isAuthor) {
            likeBtn = `<button class="btn btn-sm btn-light border position-absolute top-0 end-0 m-2" onclick="toggleLike('${w.id}')" title="Like">${heartIcon}</button>`;
        }
    }

    // Tlačítko Použít
    let addBtn = '';
    if (token) {
        addBtn = `<button class="btn btn-primary btn-sm w-100 mt-2" onclick="previewWidget('${widgetType}', ${widgetDataStr})">Použít (Náhled)</button>`;
    } else {
        addBtn = `<small class="d-block mt-2 text-muted">Přihlaste se pro vyzkoušení</small>`;
    }

    return `
    <div class="col-md-4 col-lg-3">
        <div class="card h-100 shadow-sm position-relative">
            ${likeBtn}
            <div class="card-body">
                <h5 class="card-title text-truncate pe-4">${w.publicName}</h5>
                <span class="badge bg-light text-dark border mb-2">${w.widgetType}</span>
                <p class="card-text small text-muted mb-0">Autor: ${w.authorName}</p>
                <p class="card-text small text-muted">Lokalita: ${w.widgetData.location || "N/A"}</p>
                ${addBtn}
            </div>
            <div class="card-footer bg-white border-top-0 text-muted small d-flex justify-content-between">
                <span>❤️ ${w.likesCount}</span>
                <span>${new Date(w.createdAt).toLocaleDateString()}</span>
            </div>
        </div>
    </div>
    `;
}

// --- AKCE: LIKE ---

async function toggleLike(id) {
    const token = localStorage.getItem('jwtToken');
    if (!token) return;

    await fetch(`/api/PublicWidgets/${id}/like`, {
        method: 'POST',
        headers: { 'Authorization': 'Bearer ' + token }
    });

    // Obnovíme seznamy, aby se přebarvilo srdíčko a změnil počet
    const userEmail = localStorage.getItem('userEmail');
    loadFavorites(token, userEmail);
    loadPublicList(currentPage);
}

// --- AKCE: POUŽÍT (NÁHLED) ---

async function previewWidget(widgetName, widgetData) {
    const section = document.getElementById('activeWidgetSection');
    const container = document.getElementById('previewContainer');

    // Zobrazíme sekci
    section.classList.remove('d-none');
    container.innerHTML = '<div class="text-center p-3">Načítám náhled...</div>';

    // Scroll na náhled
    section.scrollIntoView({ behavior: 'smooth' });

    try {
        // 1. Sestavíme URL pro načtení partial view
        let url = `/Widget/Load?name=${widgetName}`;
        if (widgetName === "CurrencyWidget") {
            // Pro měny použijeme default nebo uložené
            const base = localStorage.getItem("baseCurrency") || "EUR";
            const quote = localStorage.getItem("quoteCurrency") || "USD";
            url += `&baseCurrency=${encodeURIComponent(base)}&quoteCurrency=${encodeURIComponent(quote)}`;
        }
        if (widgetData.location) {
            url += `&location=${encodeURIComponent(widgetData.location)}`;
        }

        // 2. Načteme HTML ze serveru
        const resp = await fetch(url);
        if (!resp.ok) throw new Error("Chyba načítání");

        const html = await resp.text();
        container.innerHTML = html;

        // 3. Inicializace interaktivity (Grafy, Přepínače)
        // Musíme to udělat ručně, protože site.js běží jen při startu stránky
        initWidgetScripts(container, widgetName);

    } catch (e) {
        console.error(e);
        container.innerHTML = '<div class="text-danger">Nepodařilo se načíst náhled widgetu.</div>';
    }
}

function closePreview() {
    document.getElementById('activeWidgetSection').classList.add('d-none');
}

// --- POMOCNÉ FUNKCE PRO INICIALIZACI WIDGETU V NÁHLEDU ---
// (Tyto funkce kopírují logiku ze site.js, aby fungovala i zde)

function initWidgetScripts(wrapper, widgetName) {
    // 1. Toggle C/F
    const toggleBtn = wrapper.querySelector('#toggleUnit');
    toggleBtn?.addEventListener('click', () => {
        const tempEl = wrapper.querySelector('h2');
        if (!tempEl) return;
        tempEl.textContent = tempEl.textContent.includes('°C') ? tempEl.dataset.fahrenheit + '°F' : tempEl.dataset.celsius + '°C';
    });

    // 2. Grafy pro Počasí
    if (widgetName === "ForecastWeather") {
        const charts = wrapper.querySelectorAll('.temperatureChart');
        charts.forEach(canvas => {
            const ctx = canvas.getContext('2d');
            if (!ctx) return;
            const labels = JSON.parse(canvas.dataset.labels || '[]');
            const values = JSON.parse(canvas.dataset.values || '[]');

            // Pokud Chart.js není načtený, nic neuděláme
            if (typeof Chart === 'undefined') return;

            new Chart(ctx, {
                type: 'line',
                data: { labels, datasets: [{ label: 'Teplota (°C)', data: values, fill: true, tension: 0.4 }] },
                options: { responsive: true, maintainAspectRatio: false }
            });
        });
    }

    // 3. Grafy pro Měny
    if (widgetName === "CurrencyWidget") {
        const canvas = wrapper.querySelector("#rateChart");
        if (canvas && typeof Chart !== 'undefined') {
            const labels = JSON.parse(canvas.dataset.labels || '[]');
            const data = JSON.parse(canvas.dataset.rates || '[]');
            const label = canvas.dataset.label || '';
            if (labels.length && data.length) {
                new Chart(canvas.getContext("2d"), {
                    type: 'line',
                    data: { labels, datasets: [{ label, data, fill: false, tension: 0.3 }] }
                });
            }
        }

        // Funkční formulář pro měny v náhledu
        const currencyForm = wrapper.querySelector('#currencyForm');
        currencyForm?.addEventListener("submit", async e => {
            e.preventDefault();
            const base = wrapper.querySelector('#baseCurrency').value;
            const quote = wrapper.querySelector('#quoteCurrency').value;
            // Reload preview s novými parametry
            previewWidget(widgetName, { location: "" }); // Parametry se vezmou z form logic nebo se předají
            // Poznámka: Plná interaktivita změny měny v náhledu by vyžadovala složitější logiku, 
            // pro základní "Použít" stačí zobrazit to, co bylo uloženo.
        });
    }
}