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
    }

    // Funkce pro extrakci ISO kódu
    function extractIsoCode(value) {
        // 1. Zkusíme formát "Název (ISO)"
        const match = value.match(/\(([^)]+)\)$/);
        if (match) return match[1];

        // 2. Fallback: Pokud uživatel zadal přímo ISO kód (2 znaky)
        const trimmed = value.trim();
        if (trimmed.length === 2) return trimmed.toUpperCase();

        return null;
    }

    // Načtení detailů země
    async function fetchCountryDetails(isoCode) {
        if (!isoCode) return;

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

            // DŮLEŽITÉ: Aktualizujeme stav widgetu v DOM, aby ho site.js mohl uložit
            wrapper.dataset.location = isoCode;

            // Oznámíme změnu stavu (užitečné pro debug nebo reaktivní uložení)
            wrapper.dispatchEvent(new CustomEvent('widget-state-changed', { bubbles: true }));

        } catch (err) {
            console.error(err);
            resultContainer.innerHTML = `<p class="text-danger">@Localizer["Failed to load country details"].</p>`;
        }
    }

    // Event listenery
    searchBtn.addEventListener('click', () => {
        const isoCode = extractIsoCode(input.value);
        if (isoCode) fetchCountryDetails(isoCode);
    });

    input.addEventListener('keypress', e => {
        if (e.key === 'Enter') {
            e.preventDefault();
            searchBtn.click();
        }
    });

    // 🔹 OPRAVA: Inicializace stavu (Načtení a vykreslení)
    // Pokud má widget uloženou lokaci (z DB nebo předchozího uložení), načteme ji
    const savedIso = wrapper.dataset.location;

    if (savedIso) {
        // 1. Stáhneme a zobrazíme data o zemi (to chybělo)
        fetchCountryDetails(savedIso);

        // 2. Nastavíme input
        if (window.allCountries && window.allCountries[savedIso]) {
            input.value = `${window.allCountries[savedIso]} (${savedIso})`;
        } else {
            input.value = savedIso;
        }
    }
}

// Inicializuje všechny widgety na stránce (pro prvotní load dashboardu)
function initAllCountryWidgets() {
    document.querySelectorAll('.country-info-widget').forEach(widget => {
        if (!widget.dataset.initialized) {
            if (!widget.id) widget.id = `countryWidget_${Math.random().toString(36).substr(2, 9)}`;
            initCountryWidget(widget.id);
            widget.dataset.initialized = 'true';
        }
    });
}

document.addEventListener('DOMContentLoaded', initAllCountryWidgets);

// Export do globálního prostoru
window.initCountryWidget = initCountryWidget;
window.initAllCountryWidgets = initAllCountryWidgets;