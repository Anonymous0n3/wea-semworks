import * as AuthRenderer from './discovery-render-auth.js';
import * as PublicRenderer from './discovery-render-public.js';

let currentPage = 1;

// --- 1. INITIAL SETUP & RENDER STRATEGY ---
const token = localStorage.getItem('jwtToken');
const userEmail = localStorage.getItem('userEmail');

// Zde se rozhodne, který soubor s HTML šablonami se použije
const Renderer = token ? AuthRenderer : PublicRenderer;

document.addEventListener('DOMContentLoaded', async () => {
    // Řízení sekce Oblíbené
    const favSection = document.getElementById('favoritesSection');
    if (token) {
        if (favSection) favSection.classList.remove('d-none');
        await loadFavorites();
    } else {
        if (favSection) favSection.classList.add('d-none');
    }

    // Načtení veřejného seznamu
    await loadPublicList(1);
});

// Zpřístupnění změny stránky pro HTML
window.changePage = async (delta) => {
    if (currentPage + delta < 1) return;
    currentPage += delta;
    const indicator = document.getElementById('pageIndicator');
    if (indicator) indicator.innerText = `Strana ${currentPage}`;
    await loadPublicList(currentPage);
};

// --- 2. NAČÍTÁNÍ DAT ---

async function loadFavorites() {
    const container = document.getElementById('favoritesContainer');
    if (!container) return;

    try {
        const resp = await fetch('/api/PublicWidgets/liked', {
            headers: { 'Authorization': 'Bearer ' + token }
        });
        if (resp.ok) {
            const data = await resp.json();
            if (data.length === 0) {
                container.innerHTML = Renderer.renderEmptyState();
            } else {
                // Voláme renderCard z vybraného modulu
                container.innerHTML = data.map(w => Renderer.renderCard(w, userEmail)).join('');
            }
        }
    } catch (e) { console.error(e); }
}

async function loadPublicList(page) {
    const container = document.getElementById('publicContainer');
    if (!container) return;

    const searchInput = document.getElementById('discoSearch');
    const authorInput = document.getElementById('discoAuthor');

    const filter = {
        searchName: searchInput ? searchInput.value : "",
        author: authorInput ? authorInput.value : "",
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
            if (data.length === 0 && page > 1) {
                window.changePage(-1);
                return;
            }
            // Znovu voláme renderCard z vybraného modulu
            container.innerHTML = data.map(w => Renderer.renderCard(w, userEmail)).join('');
        }
    } catch (e) { console.error(e); }
}

// --- 3. AKCE (GLOBAL SCOPE) ---

window.toggleLike = async (id) => {
    if (!token) return alert("Pro hodnocení se musíte přihlásit.");

    try {
        const resp = await fetch(`/api/PublicWidgets/${id}/like`, {
            method: 'POST',
            headers: { 'Authorization': 'Bearer ' + token }
        });

        if (resp.ok) {
            loadFavorites();
            loadPublicList(currentPage);
        } else {
            console.error("Like failed", await resp.text());
        }
    } catch (e) { console.error("Like network error", e); }
};

window.previewWidget = async (widgetName, widgetData) => {
    const section = document.getElementById('activeWidgetSection');
    const container = document.getElementById('previewContainer');

    if (!section || !container) return;

    section.classList.remove('d-none');
    container.innerHTML = '<div class="text-center p-3 text-muted"><span class="spinner-border spinner-border-sm"></span> Načítám náhled...</div>';
    section.scrollIntoView({ behavior: 'smooth', block: 'start' });

    try {
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
        container.dataset.location = widgetData.location || "";

        setTimeout(() => {
            initWidgetScripts(container, widgetName);
        }, 100);

    } catch (e) {
        console.error(e);
        container.innerHTML = `<div class="alert alert-danger">Nepodařilo se načíst náhled widgetu. Chyba: ${e.message}</div>`;
    }
};

window.adoptWidget = (widgetType, widgetData) => {
    const STORAGE_KEY = 'openWidgets';
    let currentWidgets = [];
    try {
        const stored = localStorage.getItem(STORAGE_KEY);
        if (stored) {
            currentWidgets = JSON.parse(stored);
            if (!Array.isArray(currentWidgets)) currentWidgets = [];
        }
    } catch (e) {
        console.error("Chyba při čtení localStorage", e);
        currentWidgets = [];
    }

    const newWidget = {
        name: widgetType,
        location: widgetData.location || ""
    };

    currentWidgets.push(newWidget);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(currentWidgets));
    alert("Widget byl uložen! Po návratu na Dashboard se načte.");
};

window.closePreview = () => {
    const section = document.getElementById('activeWidgetSection');
    if (section) section.classList.add('d-none');
    const container = document.getElementById('previewContainer');
    if (container) container.innerHTML = '';
};

// --- 4. POMOCNÉ FUNKCE (INIT SCRIPTS) ---

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