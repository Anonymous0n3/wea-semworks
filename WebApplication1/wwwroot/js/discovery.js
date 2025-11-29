let currentPage = 1;

document.addEventListener('DOMContentLoaded', async () => {
    const token = localStorage.getItem('jwtToken');
    const userEmail = localStorage.getItem('userEmail');

    // Řízení sekce Oblíbené
    const favSection = document.getElementById('favoritesSection');
    if (token) {
        if (favSection) favSection.classList.remove('d-none');
        await loadFavorites(token, userEmail);
    } else {
        if (favSection) favSection.classList.add('d-none');
    }

    // Načtení veřejného seznamu
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
    const widgetType = w.widgetType; // Díky CamelCase nastavení v Program.cs

    // Logika pro Srdíčko (Like)
    let likeBtn = '';
    let heartIcon = '🤍';

    if (token) {
        const likedBy = w.likedBy || [];
        if (likedBy.includes(currentUserEmail)) {
            heartIcon = '❤️';
        }

        if (!isAuthor) {
            likeBtn = `<button class="btn btn-sm btn-light border position-absolute top-0 end-0 m-2" onclick="toggleLike('${w.id}')" title="Like">${heartIcon}</button>`;
        }
    }

    // Tlačítko Použít (nyní volá previewWidget místo adoptWidget)
    let addBtn = '';
    if (token) {
        addBtn = `<button class="btn btn-primary btn-sm w-100 mt-2" onclick="previewWidget('${widgetType}', ${widgetDataStr})">Vyzkoušet (Náhled)</button>`;
    } else {
        addBtn = `<small class="d-block mt-2 text-muted">Přihlaste se pro vyzkoušení</small>`;
    }

    return `
    <div class="col-md-4 col-lg-3">
        <div class="card h-100 shadow-sm position-relative">
            ${likeBtn}
            <div class="card-body">
                <h5 class="card-title text-truncate pe-4" title="${w.publicName}">${w.publicName}</h5>
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

    const userEmail = localStorage.getItem('userEmail');
    loadFavorites(token, userEmail);
    loadPublicList(currentPage);
}

// --- AKCE: POUŽÍT / NÁHLED (Změněná logika) ---

async function previewWidget(widgetName, widgetData) {
    const section = document.getElementById('activeWidgetSection');
    const container = document.getElementById('previewContainer');

    if (!section || !container) return;

    // 1. Zobrazíme sekci (odstraníme d-none)
    section.classList.remove('d-none');
    container.innerHTML = '<div class="text-center p-3 text-muted"><span class="spinner-border spinner-border-sm"></span> Načítám náhled...</div>';

    // Scroll na náhled, aby uživatel viděl, že se něco stalo
    section.scrollIntoView({ behavior: 'smooth', block: 'start' });

    try {
        // 2. Sestavíme URL pro načtení Partial View ze serveru
        let url = `/Widget/Load?name=${widgetName}`;

        if (widgetName === "CurrencyWidget") {
            const base = localStorage.getItem("baseCurrency") || "EUR";
            const quote = localStorage.getItem("quoteCurrency") || "USD";
            url += `&baseCurrency=${encodeURIComponent(base)}&quoteCurrency=${encodeURIComponent(quote)}`;
        }

        if (widgetData.location) {
            url += `&location=${encodeURIComponent(widgetData.location)}`;
        }

        // 3. Fetch HTML
        const resp = await fetch(url);
        if (!resp.ok) throw new Error("Chyba při načítání widgetu");

        const html = await resp.text();
        container.innerHTML = html;

        // 4. Inicializace interaktivity (Grafy, Tlačítka)
        // Toto je nutné, protože vložený HTML kód sám o sobě nespustí skripty ze site.js
        setTimeout(() => {
            initWidgetScripts(container, widgetName);
        }, 100);

    } catch (e) {
        console.error(e);
        container.innerHTML = `<div class="alert alert-danger">Nepodařilo se načíst náhled widgetu. Chyba: ${e.message}</div>`;
    }
}

function closePreview() {
    const section = document.getElementById('activeWidgetSection');
    if (section) section.classList.add('d-none');
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
            // Ověříme, zda máme data v datasetu
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
                const label = canvas.dataset.label || '';

                new Chart(canvas.getContext("2d"), {
                    type: 'line',
                    data: { labels, datasets: [{ label, data, fill: false, tension: 0.3 }] }
                });
            }
        }

        // Zabráníme odeslání formuláře v náhledu (jen pro demo)
        const currencyForm = wrapper.querySelector('#currencyForm');
        if (currencyForm) {
            currencyForm.addEventListener("submit", e => {
                e.preventDefault();
                alert("V režimu náhledu nelze měnit měnu (používá se nastavení z dashboardu).");
            });
        }
    }
}