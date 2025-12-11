import * as AuthRenderer from './discovery-render-auth.js';
import * as PublicRenderer from './discovery-render-public.js';

let currentPage = 1;
const token = localStorage.getItem('jwtToken');
const userEmail = localStorage.getItem('userEmail');
const Renderer = token ? AuthRenderer : PublicRenderer;

// AUTOMATICKY PŘIDÁVÁ TOKEN DO VŠECH VOLÁNÍ NA /api/ (včetně /api/auth/widgets)
const originalFetch = window.fetch;
window.fetch = async function (url, options = {}) {
    if (token && (url.includes('/api/'))) {
        if (!options.headers) options.headers = {};
        options.headers['Authorization'] = 'Bearer ' + token;
    }
    return originalFetch(url, options);
};

// DEBOUNCE
function debounce(func, wait) {
    let timeout;
    return function (...args) {
        clearTimeout(timeout);
        timeout = setTimeout(() => func.apply(this, args), wait);
    };
}

// DOM LOADED
document.addEventListener('DOMContentLoaded', async () => {
    const favSection = document.getElementById('favoritesSection');
    if (token && favSection) {
        favSection.classList.remove('d-none');
        await loadFavorites();
    }

    await loadPublicList(1);

    ['discoSearch', 'discoAuthor', 'discoType', 'discoSort'].forEach(id => {
        const el = document.getElementById(id);
        if (el) {
            if (el.tagName === 'INPUT') {
                el.addEventListener('input', debounce(() => loadPublicList(1), 300));
            }
            el.addEventListener('change', () => loadPublicList(1));
        }
    });
});

// STRÁNKOVÁNÍ
window.changePage = async (delta) => {
    const newPage = currentPage + delta;
    if (newPage < 1) return;
    currentPage = newPage;
    document.getElementById('pageIndicator').textContent = `${currentPage}`;
    await loadPublicList(currentPage);
};

// OBLÍBENÉ
async function loadFavorites() {
    const container = document.getElementById('favoritesContainer');
    if (!container) return;
    try {
        const resp = await fetch('/api/PublicWidgets/liked');
        if (resp.ok) {
            const data = await resp.json();
            // OPRAVA ZDE:
            container.innerHTML = data.length === 0
                ? (Renderer.renderEmptyState?.() || `<div class="col-12 text-muted fst-italic">${window.translations.noFavorites}</div>`)
                : data.map(w => Renderer.renderCard(w, userEmail)).join('');
        }
    } catch (e) {
        console.error('Load favorites error:', e);
    }
}

// VEŘEJNÝ SEZNAM
async function loadPublicList(page = 1) {
    const container = document.getElementById('publicContainer');
    if (!container) return;

    container.innerHTML = `
        <div class="col-12 text-center py-5">
            <div class="spinner-border text-primary" role="status"></div>
            <p class="mt-3 text-muted">${window.translations.loadingWidgets}...</p>
        </div>`;

    const filter = {
        searchName: document.getElementById('discoSearch')?.value.trim() || "",
        author: document.getElementById('discoAuthor')?.value.trim() || "",
        widgetType: document.getElementById('discoType')?.value || null,
        sortBy: document.getElementById('discoSort')?.value || "date",
        page: page,
        pageSize: 20
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
            container.innerHTML = data.map(w => Renderer.renderCard(w, userEmail)).join('');

            const hasMore = data.length === 20;
            const nextBtn = document.getElementById('nextBtn');
            const prevBtn = document.getElementById('prevBtn');
            if (nextBtn) nextBtn.disabled = !hasMore;
            if (prevBtn) prevBtn.disabled = page <= 1;
        } else {
            container.innerHTML = '<div class="col-12 text-danger text-center">${window.translations.errorLoading}</div>';
        }
    } catch (e) {
        console.error('Load public list error:', e);
        container.innerHTML = '<div class="col-12 text-danger text-center">${window.translations.serverError}</div>';
    }
}

// LIKE
window.toggleLike = async (id) => {
    if (!token) return alert("Nope");
    try {
        const resp = await fetch(`/api/PublicWidgets/${id}/like`, { method: 'POST' });
        if (resp.ok || resp.status === 400) {
            await loadFavorites();
            await loadPublicList(currentPage);
        }
    } catch (e) {
        console.error("Like error:", e);
    }
};

// ZVLASTNĚNÍ – 100% funkční
window.adoptWidget = async (widgetType, widgetData, publicName = "") => {
    if (!token) {
        alert("Nope");
        return;
    }
    try {
        const response = await fetch('/api/PublicWidgets/adopt', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                widgetType: widgetType,
                settings: widgetData
            })
        });

        if (response.ok) {
            alert(`Ok`);
        } else {
            const err = await response.text();
            alert("Error: " + err);
        }
    } catch (e) {
        console.error("Adopt error:", e);
        alert("Error");
    }
};

// NÁHLED
window.previewWidget = async (widgetName, widgetData) => {
    const section = document.getElementById('activeWidgetSection');
    const container = document.getElementById('previewContainer');
    if (!section || !container) return;
    section.classList.remove('d-none');
    container.innerHTML = '<div class="text-center p-3 text-muted"><span class="spinner-border spinner-border-sm"></span> ${window.translations.loadingPreview}...</div>';
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
        setTimeout(() => initWidgetScripts(container, widgetName), 100);
    } catch (e) {
        console.error(e);
        container.innerHTML = `<div class="alert alert-danger">${window.translations.previewError}: ${e.message}</div>`;
    }
};

// ZAVŘENÍ NÁHLEDU
window.closePreview = () => {
    const section = document.getElementById('activeWidgetSection');
    if (section) section.classList.add('d-none');
    const container = document.getElementById('previewContainer');
    if (container) container.innerHTML = '';
};

// INICIALIZACE SKRIPTŮ VE WIDGETU
function initWidgetScripts(wrapper, widgetName) {
    const toggleBtn = wrapper.querySelector('#toggleUnit');
    if (toggleBtn) {
        toggleBtn.addEventListener('click', () => {
            const tempEl = wrapper.querySelector('h2');
            if (!tempEl) return;
            const isC = tempEl.textContent.includes('°C');
            tempEl.textContent = isC ? tempEl.dataset.fahrenheit + '°F' : tempEl.dataset.celsius + '°C';
        });
    }

    if (widgetName === "ForecastWeather") {
        wrapper.querySelectorAll('.temperatureChart').forEach(canvas => {
            const ctx = canvas.getContext('2d');
            if (!ctx) return;
            const labels = JSON.parse(canvas.dataset.labels || '[]');
            const values = JSON.parse(canvas.dataset.values || '[]');
            if (typeof Chart !== 'undefined') {
                new Chart(ctx, {
                    type: 'line',
                    data: { labels, datasets: [{ label: 'Teplota (°C)', data: values, fill: true, tension: 0.4 }] },
                    options: { responsive: true, maintainAspectRatio: false }
                });
            }
        });
    }

    if (widgetName === "CurrencyWidget") {
        const canvas = wrapper.querySelector("#rateChart");
        if (canvas && typeof Chart !== 'undefined') {
            const labels = JSON.parse(canvas.dataset.labels || '[]');
            const data = JSON.parse(canvas.dataset.rates || '[]');
            new Chart(canvas.getContext("2d"), {
                type: 'line',
                data: { labels, datasets: [{ label: canvas.dataset.label || '', data, fill: false, tension: 0.3 }] }
            });
        }
        wrapper.querySelector('#currencyForm')?.addEventListener("submit", e => {
            e.preventDefault();
            alert("Nope");
        });
    }

    if (widgetName === "CountryInfoWidget" && typeof window.initCountryWidget === 'function') {
        let el = wrapper.querySelector('.country-info-widget');
        if (el && !el.id) el.id = `preview_country_${Math.random().toString(36).substr(2, 9)}`;
        window.initCountryWidget(el?.id);
        if (wrapper.dataset.location) {
            el.dataset.location = wrapper.dataset.location;
            window.initCountryWidget(el.id);
        }
    }
}

// EXPORT
window.changePage = window.changePage;
window.toggleLike = window.toggleLike;
window.previewWidget = window.previewWidget;
window.adoptWidget = window.adoptWidget;
window.closePreview = window.closePreview;

console.log("discovery.js načteno – token se automaticky posílá na všechny /api/ volání");