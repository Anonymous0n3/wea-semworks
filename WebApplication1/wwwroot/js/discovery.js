let currentPage = 1;

document.addEventListener('DOMContentLoaded', async () => {
    const token = localStorage.getItem('jwtToken');
    const userEmail = localStorage.getItem('userEmail');

    // Řízení sekce Oblíbené (jen pro přihlášené)
    const favSection = document.getElementById('favoritesSection');
    if (token) {
        if (favSection) favSection.classList.remove('d-none');
        await loadFavorites(token, userEmail);
    } else {
        if (favSection) favSection.classList.add('d-none');
    }

    // Načtení veřejného seznamu (pro všechny)
    await loadPublicList(1);
});

async function changePage(delta) {
    if (currentPage + delta < 1) return;
    currentPage += delta;
    const indicator = document.getElementById('pageIndicator');
    if (indicator) indicator.innerText = `Strana ${currentPage}`;
    await loadPublicList(currentPage);
}

// --- NAČÍTÁNÍ DAT ---

async function loadFavorites(token, userEmail) {
    const container = document.getElementById('favoritesContainer');
    if (!container) return;

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
    if (!container) return;

    const searchInput = document.getElementById('discoSearch');
    const authorInput = document.getElementById('discoAuthor');

    const search = searchInput ? searchInput.value : "";
    const author = authorInput ? authorInput.value : "";
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
    // Escapování názvu pro bezpečné vložení do JS
    const safeName = w.publicName.replace(/'/g, "\\'");
    const widgetType = w.widgetType;

    let likesCount = w.likesCount || 0;

    // --- 1. LIKE TLAČÍTKO ---
    let likeSection = '';
    if (token) {
        const likedBy = w.likedBy || [];
        const isLiked = currentUserEmail && likedBy.some(e => e.toLowerCase() === currentUserEmail.toLowerCase());
        const btnClass = isLiked ? 'btn-danger' : 'btn-outline-danger';
        const icon = isLiked ? '❤️' : '🤍';

        if (!isAuthor) {
            likeSection = `
            <button type="button" class="btn btn-sm ${btnClass} position-relative" 
                    style="z-index: 5;"
                    onclick="event.stopPropagation(); window.toggleLike('${w.id}')">
                ${icon} ${likesCount}
            </button>`;
        } else {
            likeSection = `<span class="badge bg-light text-dark border" title="Vlastní widget">❤️ ${likesCount}</span>`;
        }
    } else {
        likeSection = `<span class="text-muted fw-bold">❤️ ${likesCount}</span>`;
    }

    // --- 2. TLAČÍTKO POUŽÍT (NÁHLED) ---
    let actionButtons = `
        <button class="btn btn-primary btn-sm w-100 mt-3" onclick="window.previewWidget('${widgetType}', ${widgetDataStr})">
            👁️ Vyzkoušet (Náhled)
        </button>
    `;

    // --- 3. NOVÉ TLAČÍTKO ZVLASTNIT (JEN PŘIHLÁŠENÍ) ---
    if (token) {
        actionButtons += `
        <button class="btn btn-success btn-sm w-100 mt-2" onclick="window.adoptWidget('${widgetType}', ${widgetDataStr}, '${safeName}')">
            💾 Zvlastnit (Uložit)
        </button>
        `;
    }

    return `
    <div class="col-md-4 col-lg-3">
        <div class="card h-100 shadow-sm">
            <div class="card-body d-flex flex-column">
                <h5 class="card-title text-truncate" title="${w.publicName}">${w.publicName}</h5>
                <div class="mb-2">
                    <span class="badge bg-info text-dark">${w.widgetType}</span>
                </div>
                <p class="card-text small text-muted mb-1">Autor: ${w.authorName}</p>
                <p class="card-text small text-muted mb-auto">Lokalita: ${w.widgetData.location || "N/A"}</p>
                
                ${actionButtons}
            </div>
            <div class="card-footer bg-white border-top-0 d-flex justify-content-between align-items-center py-2">
                ${likeSection}
                <small class="text-muted">${new Date(w.createdAt).toLocaleDateString()}</small>
            </div>
        </div>
    </div>
    `;
}

// --- AKCE: LIKE ---

async function toggleLike(id) {
    const token = localStorage.getItem('jwtToken');
    if (!token) return alert("Pro hodnocení se musíte přihlásit.");

    try {
        const resp = await fetch(`/api/PublicWidgets/${id}/like`, {
            method: 'POST',
            headers: { 'Authorization': 'Bearer ' + token }
        });

        if (resp.ok) {
            const userEmail = localStorage.getItem('userEmail');
            loadFavorites(token, userEmail);
            loadPublicList(currentPage);
        } else {
            console.error("Like failed", await resp.text());
        }
    } catch (e) {
        console.error("Like network error", e);
    }
}

// --- AKCE: POUŽÍT / NÁHLED ---

async function previewWidget(widgetName, widgetData) {
    const section = document.getElementById('activeWidgetSection');
    const container = document.getElementById('previewContainer');

    if (!section || !container) return;

    section.classList.remove('d-none');
    container.innerHTML = '<div class="text-center p-3 text-muted"><span class="spinner-border spinner-border-sm"></span> Načítám náhled...</div>';

    section.scrollIntoView({ behavior: 'smooth', block: 'start' });

    try {
        // Poznámka: Endpoint /Widget/Load musí být přístupný i pro nepřihlášené (bez [Authorize])
        let url = `/Widget/Load?name=${widgetName}`;

        if (widgetName === "CurrencyWidget") {
            const base = localStorage.getItem("baseCurrency") || "EUR";
            const quote = localStorage.getItem("quoteCurrency") || "USD";
            url += `&baseCurrency=${encodeURIComponent(base)}&quoteCurrency=${encodeURIComponent(quote)}`;
        }

        if (widgetData.location) {
            url += `&location=${encodeURIComponent(widgetData.location)}`;
        }

        const resp = await fetch(url);
        if (!resp.ok) throw new Error("Chyba při načítání widgetu");

        const html = await resp.text();
        container.innerHTML = html;

        // Nastavíme dataset pro případnou inicializaci v initWidgetScripts
        container.dataset.location = widgetData.location || "";

        setTimeout(() => {
            initWidgetScripts(container, widgetName);
        }, 100);

    } catch (e) {
        console.error(e);
        container.innerHTML = `<div class="alert alert-danger">Nepodařilo se načíst náhled widgetu. Chyba: ${e.message}</div>`;
    }
}

function adoptWidget(widgetType, widgetData) {
    // 1. PŘESNÝ KLÍČ PRO LOCALSTORAGE
    const STORAGE_KEY = 'openWidgets';

    // 2. Načtení současných widgetů
    let currentWidgets = [];
    try {
        const stored = localStorage.getItem(STORAGE_KEY);
        if (stored) {
            currentWidgets = JSON.parse(stored);
            // Pojistka, aby to bylo vždy pole
            if (!Array.isArray(currentWidgets)) currentWidgets = [];
        }
    } catch (e) {
        console.error("Chyba při čtení localStorage", e);
        currentWidgets = [];
    }

    // 3. Vytvoření objektu ve správné struktuře: {name: "...", location: "..."}
    const newWidget = {
        name: widgetType,
        location: widgetData.location || "" // Pokud location chybí, uloží prázdný string
    };

    // 4. Přidání do pole
    currentWidgets.push(newWidget);

    // 5. Uložení zpět do localStorage pod klíčem 'openWidgets'
    localStorage.setItem(STORAGE_KEY, JSON.stringify(currentWidgets));

    // 6. Potvrzení
    alert("Widget byl uložen! Po návratu na Dashboard se načte.");
}

function closePreview() {
    const section = document.getElementById('activeWidgetSection');
    if (section) section.classList.add('d-none');
    // Vyčistit obsah, aby se zastavily případné intervaly nebo listenery
    const container = document.getElementById('previewContainer');
    if (container) container.innerHTML = '';
}

// --- POMOCNÉ FUNKCE PRO OŽIVENÍ WIDGETU V NÁHLEDU ---

function initWidgetScripts(wrapper, widgetName) {
    // 1. Toggle C/F
    const toggleBtn = wrapper.querySelector('#toggleUnit');
    if (toggleBtn) {
        toggleBtn.addEventListener('click', () => {
            const tempEl = wrapper.querySelector('h2');
            if (!tempEl) return;
            const isC = tempEl.textContent.includes('°C');
            tempEl.textContent = isC ? tempEl.dataset.fahrenheit + '°F' : tempEl.dataset.celsius + '°C';
        });
    }

    // 2. Grafy pro Počasí
    if (widgetName === "ForecastWeather") {
        const charts = wrapper.querySelectorAll('.temperatureChart');
        charts.forEach(canvas => {
            const ctx = canvas.getContext('2d');
            if (!ctx) return;
            const labelsRaw = canvas.dataset.labels;
            const valuesRaw = canvas.dataset.values;

            if (!labelsRaw || !valuesRaw) return;

            const labels = JSON.parse(labelsRaw);
            const values = JSON.parse(valuesRaw);

            if (typeof Chart !== 'undefined') {
                new Chart(ctx, {
                    type: 'line',
                    data: { labels, datasets: [{ label: 'Teplota (°C)', data: values, fill: true, tension: 0.4 }] },
                    options: { responsive: true, maintainAspectRatio: false }
                });
            }
        });
    }

    // 3. Grafy pro Měny
    if (widgetName === "CurrencyWidget") {
        const canvas = wrapper.querySelector("#rateChart");
        if (canvas && typeof Chart !== 'undefined') {
            const labelsRaw = canvas.dataset.labels;
            const dataRaw = canvas.dataset.rates;

            if (labelsRaw && dataRaw) {
                const labels = JSON.parse(labelsRaw);
                const data = JSON.parse(dataRaw);

                new Chart(canvas.getContext("2d"), {
                    type: 'line',
                    data: { labels, datasets: [{ label: canvas.dataset.label || '', data, fill: false, tension: 0.3 }] }
                });
            }
        }

        const currencyForm = wrapper.querySelector('#currencyForm');
        if (currencyForm) {
            currencyForm.addEventListener("submit", e => {
                e.preventDefault();
                alert("V režimu náhledu nelze měnit měnu (používá se nastavení z dashboardu).");
            });
        }
    }

    // 4. Inicializace Country Widgetu
    if (widgetName === "CountryInfoWidget") {
        const widgetEl = wrapper.querySelector('.country-info-widget');
        if (widgetEl && typeof window.initCountryWidget === 'function') {
            if (!widgetEl.id) {
                widgetEl.id = `preview_country_${Math.random().toString(36).substr(2, 9)}`;
            }

            window.initCountryWidget(widgetEl.id);

            const loc = wrapper.dataset.location;
            if (loc) {
                const input = widgetEl.querySelector('.input-country');
                if (input) {
                    widgetEl.dataset.location = loc;
                    window.initCountryWidget(widgetEl.id);
                }
            }
        }
    }
}

// 🔹 EXPORT FUNKCÍ DO GLOBAL SCOPE
window.toggleLike = toggleLike;
window.previewWidget = previewWidget;
window.closePreview = closePreview;
window.adoptWidget = adoptWidget; // <--- PŘIDÁNO