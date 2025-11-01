// countryWidget.js
// ---------------------------------------------
// Dynamická inicializace všech Country Info widgetů
// ---------------------------------------------

function initCountryWidget(widgetId) {
    const wrapper = document.getElementById(widgetId);
    if (!wrapper) return;

    const input = wrapper.querySelector('.input-country');
    const searchBtn = wrapper.querySelector('.btn-search');
    const resultContainer = wrapper.querySelector('.country-result');

    // Vytvoříme nebo najdeme datalist
    let datalist = wrapper.querySelector('datalist');
    if (!datalist) {
        datalist = document.createElement('datalist');
        wrapper.querySelector('.input-group').appendChild(datalist);
    }

    const listId = `countries_${widgetId}`;
    datalist.id = listId;
    input.setAttribute('list', listId);

    // Naplníme datalist z window.allCountries
    if (window.allCountries && Object.keys(window.allCountries).length > 0) {
        datalist.innerHTML = Object.entries(window.allCountries)
            .map(([key, value]) => `<option value="${value} (${key})"></option>`)
            .join('');
    } else {
        console.warn('⚠️ window.allCountries není inicializován – datalist zůstane prázdný.');
    }

    // Funkce pro extrakci ISO kódu
    function extractIsoCode(value) {
        const match = value.match(/\(([^)]+)\)$/);
        return match ? match[1] : null;
    }

    // Načtení detailů země
    async function fetchCountryDetails(isoCode) {
        if (!isoCode) {
            resultContainer.innerHTML = `<p class="text-muted">Select a valid country.</p>`;
            return;
        }

        try {
            const resp = await fetch(`/Country/Details?isoCode=${encodeURIComponent(isoCode)}`, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });

            if (!resp.ok) {
                resultContainer.innerHTML = `<p class="text-danger">@Localizer["Failed to load country details"].</p>`;
                return;
            }

            const html = await resp.text();
            resultContainer.innerHTML = html;
        } catch (err) {
            console.error(err);
            resultContainer.innerHTML = `<p class="text-danger">@Localizer["Error fetching country details"].</p>`;
        }
    }

    // Event listenery
    searchBtn.addEventListener('click', () => {
        const isoCode = extractIsoCode(input.value);
        fetchCountryDetails(isoCode);
    });

    input.addEventListener('keypress', e => {
        if (e.key === 'Enter') {
            e.preventDefault();
            searchBtn.click();
        }
    });
}

// Inicializuje všechny widgety na stránce
function initAllCountryWidgets() {
    document.querySelectorAll('.country-info-widget').forEach(widget => {
        if (!widget.dataset.initialized) {
            if (!widget.id) widget.id = `countryWidget_${Math.random().toString(36).substr(2, 9)}`;
            initCountryWidget(widget.id);
            widget.dataset.initialized = 'true';
        }
    });
}

// Při načtení stránky
document.addEventListener('DOMContentLoaded', initAllCountryWidgets);

// Export do globálního prostoru (pro dynamické přidávání)
window.initCountryWidget = initCountryWidget;
window.initAllCountryWidgets = initAllCountryWidgets;
